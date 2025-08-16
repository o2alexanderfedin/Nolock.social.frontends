using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Processors
{
    /// <summary>
    /// Processes receipt documents to extract structured data.
    /// Implements the IDocumentProcessor interface for the plugin architecture.
    /// </summary>
    public class ReceiptProcessor : IDocumentProcessor
    {
        private readonly ILogger<ReceiptProcessor> _logger;

        /// <summary>
        /// Regular expressions for common receipt patterns.
        /// </summary>
        private static class Patterns
        {
            public static readonly Regex Total = new Regex(
                @"(?:TOTAL|AMOUNT\s+DUE|GRAND\s+TOTAL)[:\s]*\$?(\d+\.?\d*)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            public static readonly Regex Subtotal = new Regex(
                @"(?:SUBTOTAL|SUB\s+TOTAL)[:\s]*\$?(\d+\.?\d*)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            public static readonly Regex Tax = new Regex(
                @"(?:TAX|SALES\s+TAX|GST|VAT)[:\s]*\$?(\d+\.?\d*)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            public static readonly Regex Date = new Regex(
                @"(\d{1,2}[/-]\d{1,2}[/-]\d{2,4})",
                RegexOptions.Compiled);

            public static readonly Regex Time = new Regex(
                @"(\d{1,2}:\d{2}(?::\d{2})?(?:\s*[AP]M)?)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            public static readonly Regex ReceiptNumber = new Regex(
                @"(?:RECEIPT|TRANS|TRANSACTION|REF)[#\s]*([A-Z0-9-]+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            public static readonly Regex ItemLine = new Regex(
                @"^(.+?)\s+(\d+\.?\d*)\s*$",
                RegexOptions.Multiline | RegexOptions.Compiled);

            public static readonly Regex Currency = new Regex(
                @"(?:USD|EUR|GBP|CAD|AUD|JPY)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        /// <summary>
        /// Initializes a new instance of the ReceiptProcessor class.
        /// </summary>
        /// <param name="logger">Logger for diagnostic output.</param>
        public ReceiptProcessor(ILogger<ReceiptProcessor> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public string DocumentType => "Receipt";

        /// <inheritdoc />
        public async Task<object> ProcessAsync(string rawOcrData, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(rawOcrData))
            {
                throw new ArgumentException("Raw OCR data cannot be null or empty.", nameof(rawOcrData));
            }

            _logger.LogInformation("Processing receipt document");

            var processedReceipt = new ProcessedReceipt
            {
                RawOcrText = rawOcrData,
                ProcessedAt = DateTime.UtcNow
            };

            try
            {
                // Extract receipt data from raw OCR text
                processedReceipt.ReceiptData = await ExtractReceiptDataAsync(rawOcrData, cancellationToken);
                
                // Calculate confidence score based on extracted fields
                processedReceipt.ConfidenceScore = CalculateConfidenceScore(processedReceipt.ReceiptData);
                
                // Validate the processed data
                processedReceipt.Validate();
                
                _logger.LogInformation("Receipt processed successfully with confidence score: {ConfidenceScore}", 
                    processedReceipt.ConfidenceScore);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing receipt document");
                processedReceipt.ValidationErrors.Add($"Processing error: {ex.Message}");
            }

            return processedReceipt;
        }

        /// <inheritdoc />
        public bool CanProcess(string rawOcrData)
        {
            if (string.IsNullOrWhiteSpace(rawOcrData))
            {
                return false;
            }

            var lowerText = rawOcrData.ToLowerInvariant();

            // Check for receipt-specific keywords
            var receiptKeywords = new[]
            {
                "total", "subtotal", "tax", "receipt", "invoice",
                "amount due", "payment", "thank you", "items", "qty"
            };

            var keywordCount = receiptKeywords.Count(keyword => lowerText.Contains(keyword));

            // Consider it a receipt if we find at least 3 keywords
            return keywordCount >= 3;
        }

        /// <summary>
        /// Extracts receipt data from raw OCR text.
        /// </summary>
        private async Task<ReceiptData> ExtractReceiptDataAsync(string rawText, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var receiptData = new ReceiptData();

                // Extract store information
                ExtractStoreInformation(rawText, receiptData);

                // Extract transaction details
                ExtractTransactionDetails(rawText, receiptData);

                // Extract items
                ExtractItems(rawText, receiptData);

                // Extract monetary amounts
                ExtractMonetaryAmounts(rawText, receiptData);

                // Verify and calculate totals
                VerifyAndCalculateTotals(receiptData);

                return receiptData;
            }, cancellationToken);
        }

        /// <summary>
        /// Extracts store information from the raw text.
        /// </summary>
        private void ExtractStoreInformation(string rawText, ReceiptData receiptData)
        {
            var lines = rawText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            // Usually store name is in the first few lines
            if (lines.Length > 0)
            {
                receiptData.StoreName = lines[0].Trim();
            }

            // Look for phone pattern
            var phonePattern = new Regex(@"(\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4})");
            var phoneMatch = phonePattern.Match(rawText);
            if (phoneMatch.Success)
            {
                receiptData.StorePhone = phoneMatch.Groups[1].Value;
            }

            // Extract currency if present
            var currencyMatch = Patterns.Currency.Match(rawText);
            if (currencyMatch.Success)
            {
                receiptData.Currency = currencyMatch.Value.ToUpperInvariant();
            }
        }

        /// <summary>
        /// Extracts transaction details from the raw text.
        /// </summary>
        private void ExtractTransactionDetails(string rawText, ReceiptData receiptData)
        {
            // Extract date
            var dateMatch = Patterns.Date.Match(rawText);
            if (dateMatch.Success)
            {
                if (DateTime.TryParse(dateMatch.Groups[1].Value, out var date))
                {
                    receiptData.TransactionDate = date;

                    // Try to find and add time
                    var timeMatch = Patterns.Time.Match(rawText);
                    if (timeMatch.Success && DateTime.TryParse($"{date:yyyy-MM-dd} {timeMatch.Groups[1].Value}", out var dateTime))
                    {
                        receiptData.TransactionDate = dateTime;
                    }
                }
            }

            // Extract receipt number
            var receiptMatch = Patterns.ReceiptNumber.Match(rawText);
            if (receiptMatch.Success)
            {
                receiptData.ReceiptNumber = receiptMatch.Groups[1].Value;
            }
        }

        /// <summary>
        /// Extracts line items from the raw text.
        /// </summary>
        private void ExtractItems(string rawText, ReceiptData receiptData)
        {
            var lines = rawText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var items = new List<ReceiptItem>();

            foreach (var line in lines)
            {
                // Look for lines with prices (basic pattern: text followed by number)
                var itemMatch = Patterns.ItemLine.Match(line);
                if (itemMatch.Success)
                {
                    var description = itemMatch.Groups[1].Value.Trim();
                    
                    // Skip lines that are likely totals or tax
                    if (description.IndexOf("TOTAL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        description.IndexOf("TAX", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        description.IndexOf("SUBTOTAL", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        continue;
                    }

                    if (decimal.TryParse(itemMatch.Groups[2].Value, NumberStyles.Currency, CultureInfo.InvariantCulture, out var price))
                    {
                        items.Add(new ReceiptItem
                        {
                            Description = description,
                            Quantity = 1,
                            UnitPrice = price,
                            TotalPrice = price
                        });
                    }
                }
            }

            receiptData.Items = items;
        }

        /// <summary>
        /// Extracts monetary amounts from the raw text.
        /// </summary>
        private void ExtractMonetaryAmounts(string rawText, ReceiptData receiptData)
        {
            // Extract total
            var totalMatch = Patterns.Total.Match(rawText);
            if (totalMatch.Success && decimal.TryParse(totalMatch.Groups[1].Value, NumberStyles.Currency, CultureInfo.InvariantCulture, out var total))
            {
                receiptData.Total = total;
            }

            // Extract subtotal
            var subtotalMatch = Patterns.Subtotal.Match(rawText);
            if (subtotalMatch.Success && decimal.TryParse(subtotalMatch.Groups[1].Value, NumberStyles.Currency, CultureInfo.InvariantCulture, out var subtotal))
            {
                receiptData.Subtotal = subtotal;
            }

            // Extract tax
            var taxMatch = Patterns.Tax.Match(rawText);
            if (taxMatch.Success && decimal.TryParse(taxMatch.Groups[1].Value, NumberStyles.Currency, CultureInfo.InvariantCulture, out var tax))
            {
                receiptData.TaxAmount = tax;
                
                // Calculate tax rate if we have subtotal
                if (receiptData.Subtotal > 0)
                {
                    receiptData.TaxRate = (tax / receiptData.Subtotal) * 100;
                }
            }
        }

        /// <summary>
        /// Verifies and calculates totals for consistency.
        /// </summary>
        private void VerifyAndCalculateTotals(ReceiptData receiptData)
        {
            // If we have items but no subtotal, calculate it
            if (receiptData.Items.Any() && receiptData.Subtotal == 0)
            {
                receiptData.Subtotal = receiptData.Items.Sum(item => item.TotalPrice);
            }

            // If we have subtotal and tax but no total, calculate it
            if (receiptData.Subtotal > 0 && receiptData.Total == 0)
            {
                receiptData.Total = receiptData.Subtotal + receiptData.TaxAmount;
            }

            // If we have total and subtotal but no tax, calculate it
            if (receiptData.Total > 0 && receiptData.Subtotal > 0 && receiptData.TaxAmount == 0)
            {
                receiptData.TaxAmount = receiptData.Total - receiptData.Subtotal;
                
                if (receiptData.Subtotal > 0)
                {
                    receiptData.TaxRate = (receiptData.TaxAmount / receiptData.Subtotal) * 100;
                }
            }
        }

        /// <summary>
        /// Calculates a confidence score based on the completeness of extracted data.
        /// </summary>
        private double CalculateConfidenceScore(ReceiptData receiptData)
        {
            var score = 0.0;
            var maxScore = 10.0;

            // Check for essential fields
            if (!string.IsNullOrWhiteSpace(receiptData.StoreName)) score += 2.0;
            if (receiptData.TransactionDate.HasValue) score += 1.5;
            if (!string.IsNullOrWhiteSpace(receiptData.ReceiptNumber)) score += 1.0;
            if (receiptData.Items.Any()) score += 2.0;
            if (receiptData.Total > 0) score += 2.0;
            if (receiptData.Subtotal > 0) score += 1.0;
            if (receiptData.TaxAmount > 0) score += 0.5;

            return Math.Min(score / maxScore, 1.0);
        }
    }
}