using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NoLock.Social.Core.Extensions;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Models;
using NoLock.Social.Core.Common.Results;
using NoLock.Social.Core.Common.Extensions;

namespace NoLock.Social.Core.OCR.Processors
{
    /// <summary>
    /// Thin processor layer for receipt documents that delegates to backend OCR service.
    /// The backend service handles all parsing, extraction, and validation logic.
    /// </summary>
    public class ReceiptProcessor : IDocumentProcessor
    {
        private readonly ILogger<ReceiptProcessor> _logger;
        private readonly IOCRService _ocrService;

        /// <summary>
        /// Initializes a new instance of the ReceiptProcessor class.
        /// </summary>
        /// <param name="logger">Logger for diagnostic output.</param>
        /// <param name="ocrService">Backend OCR service for processing.</param>
        public ReceiptProcessor(ILogger<ReceiptProcessor> logger, IOCRService ocrService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _ocrService = ocrService ?? throw new ArgumentNullException(nameof(ocrService));
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

            _logger.LogInformation("Sending receipt document to backend OCR service");

            try
            {
                // Submit document to backend OCR service
                // Note: rawOcrData should be base64 image data from the camera/scanner
                var submissionRequest = new OCRSubmissionRequest
                {
                    ImageData = rawOcrData,
                    DocumentType = Models.DocumentType.Receipt,
                    Metadata = new System.Collections.Generic.Dictionary<string, string>
                    {
                        { "ProcessorType", "Receipt" },
                        { "RequestTime", DateTime.UtcNow.ToString("O") }
                    }
                };

                var submissionResponse = await _ocrService.SubmitDocumentAsync(
                    submissionRequest, 
                    cancellationToken);

                // Poll for completion using the existing polling service
                // For now, we'll do a simple wait - in production this should use PollingService
                OCRStatusResponse statusResponse = null;
                var maxAttempts = 60; // 60 attempts * 2 seconds = 2 minutes max
                var attempt = 0;

                while (attempt < maxAttempts)
                {
                    await Task.Delay(2000, cancellationToken); // 2 second delay
                    
                    statusResponse = await _ocrService.GetStatusAsync(
                        submissionResponse.TrackingId, 
                        cancellationToken);

                    if (statusResponse.Status == OCRProcessingStatus.Complete ||
                        statusResponse.Status == OCRProcessingStatus.Failed)
                    {
                        break;
                    }

                    attempt++;
                    _logger.LogDebug("Polling attempt {Attempt}/{MaxAttempts} for tracking ID {TrackingId}", 
                        attempt, maxAttempts, submissionResponse.TrackingId);
                }

                if (statusResponse?.Status != OCRProcessingStatus.Complete)
                {
                    throw new InvalidOperationException(
                        $"OCR processing failed or timed out. Status: {statusResponse?.Status}, " +
                        $"Error: {statusResponse?.ErrorMessage}");
                }

                // Backend returns ReceiptData as JSON in StructuredData field
                ReceiptData receiptData;
                if (!string.IsNullOrWhiteSpace(statusResponse.ResultData?.StructuredData))
                {
                    var deserializationResult = _logger.ExecuteWithLogging(
                        () => JsonSerializer.Deserialize<ReceiptData>(
                            statusResponse.ResultData.StructuredData,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }),
                        "Failed to deserialize ReceiptData from backend response");
                    
                    receiptData = deserializationResult.IsSuccess 
                        ? deserializationResult.Value 
                        : new ReceiptData();
                }
                else
                {
                    // Fallback: If no structured data, at least populate with extracted text
                    receiptData = new ReceiptData();
                    _logger.LogWarning("No structured data received from backend OCR service");
                }

                var processedReceipt = new ProcessedReceipt
                {
                    RawOcrText = rawOcrData,
                    ReceiptData = receiptData,
                    ConfidenceScore = statusResponse.ResultData?.ConfidenceScore / 100.0 ?? 0,
                    ProcessedAt = DateTime.UtcNow
                };
                
                // Post-process the receipt data for business logic
                PostProcessReceiptData(processedReceipt);
                
                // Basic validation of the response
                processedReceipt.Validate();
                
                _logger.LogInformation("Receipt processed successfully with confidence score: {ConfidenceScore}", 
                    processedReceipt.ConfidenceScore);
                
                return processedReceipt;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing receipt document");
                
                // Return error result
                var errorResult = new ProcessedReceipt
                {
                    RawOcrText = rawOcrData,
                    ReceiptData = new ReceiptData(),
                    ConfidenceScore = 0,
                    ProcessedAt = DateTime.UtcNow
                };
                errorResult.ValidationErrors.Add($"Processing error: {ex.Message}");
                return errorResult;
            }
        }

        /// <inheritdoc />
        public bool CanProcess(string rawOcrData)
        {
            if (string.IsNullOrWhiteSpace(rawOcrData))
            {
                return false;
            }

            var lowerText = rawOcrData.ToLowerInvariant();

            // Simple keyword detection to identify receipt documents
            // The backend will do the actual validation
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
        /// Post-processes receipt data to handle business logic calculations and validation.
        /// This includes tax calculations, date parsing, and confidence scoring.
        /// </summary>
        /// <param name="processedReceipt">The processed receipt to enhance.</param>
        private void PostProcessReceiptData(ProcessedReceipt processedReceipt)
        {
            if (processedReceipt?.ReceiptData == null)
            {
                return;
            }

            var receiptData = processedReceipt.ReceiptData;

            // Calculate missing tax amount if we have total and subtotal
            if (receiptData.TaxAmount == 0 && receiptData.Total > 0 && receiptData.Subtotal > 0)
            {
                receiptData.TaxAmount = receiptData.Total - receiptData.Subtotal;
                _logger.LogDebug("Calculated tax amount: {TaxAmount}", receiptData.TaxAmount);
            }

            // Calculate tax rate if we have tax amount and subtotal
            if (receiptData.TaxRate == 0 && receiptData.TaxAmount > 0 && receiptData.Subtotal > 0)
            {
                receiptData.TaxRate = Math.Round((receiptData.TaxAmount / receiptData.Subtotal) * 100, 2);
                _logger.LogDebug("Calculated tax rate: {TaxRate}%", receiptData.TaxRate);
            }

            // Calculate confidence score based on data completeness
            var confidenceScore = CalculateConfidenceScore(receiptData);
            // Always use the calculated confidence score for business logic requirements
            processedReceipt.ConfidenceScore = confidenceScore;
            _logger.LogDebug("Set confidence score to: {ConfidenceScore} (from calculated score)", confidenceScore);

            // Parse and validate transaction date from various formats
            ParseTransactionDate(receiptData, processedReceipt.RawOcrText);
        }

        /// <summary>
        /// Calculates confidence score based on the completeness of receipt data.
        /// </summary>
        /// <param name="receiptData">The receipt data to evaluate.</param>
        /// <returns>A confidence score between 0.0 and 1.0.</returns>
        private double CalculateConfidenceScore(ReceiptData receiptData)
        {
            var score = 0.0;
            var maxScore = 0.0;

            // Store name (weight: 0.1)
            maxScore += 0.1;
            if (!string.IsNullOrWhiteSpace(receiptData.StoreName))
                score += 0.1;

            // Transaction date (weight: 0.1)
            maxScore += 0.1;
            if (receiptData.TransactionDate.HasValue)
                score += 0.1;

            // Financial amounts (weight: 0.5 total)
            maxScore += 0.5;
            if (receiptData.Total > 0)
                score += 0.2;
            if (receiptData.Subtotal > 0)
                score += 0.15;
            if (receiptData.TaxAmount >= 0) // Tax can be 0
                score += 0.15;

            // Receipt identification (weight: 0.2)
            maxScore += 0.2;
            if (!string.IsNullOrWhiteSpace(receiptData.ReceiptNumber))
                score += 0.1;
            if (receiptData.Items?.Count > 0)
                score += 0.1;

            // Payment method (weight: 0.1)
            maxScore += 0.1;
            if (!string.IsNullOrWhiteSpace(receiptData.PaymentMethod))
                score += 0.1;

            return maxScore > 0 ? Math.Min(1.0, score / maxScore) : 0.0;
        }

        /// <summary>
        /// Attempts to parse transaction date from various date formats found in receipt text.
        /// </summary>
        /// <param name="receiptData">The receipt data to update.</param>
        /// <param name="rawOcrText">The raw OCR text to search for dates.</param>
        private void ParseTransactionDate(ReceiptData receiptData, string rawOcrText)
        {
            if (receiptData.TransactionDate.HasValue || string.IsNullOrWhiteSpace(rawOcrText))
            {
                return; // Already has a date or no text to parse
            }

            var dateFormats = new[]
            {
                "MM/dd/yyyy", "M/d/yyyy", "MM/dd/yy", "M/d/yy",    // US formats
                "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "d/M/yy",    // European formats
                "yyyy-MM-dd", "yyyy-M-d",                          // ISO formats
                "MM-dd-yyyy", "M-d-yyyy", "MM-dd-yy", "M-d-yy",    // US dash formats
                "dd-MM-yyyy", "d-M-yyyy", "dd-MM-yy", "d-M-yy",    // European dash formats
                "MMM dd, yyyy", "MMM d, yyyy",                     // Text month formats
                "dd MMM yyyy", "d MMM yyyy"                        // European text formats
            };

            // Look for date patterns in the text
            var datePatterns = new[]
            {
                @"\b\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4}\b",  // MM/dd/yyyy, MM-dd-yyyy, dd/MM/yyyy, dd-MM-yyyy
                @"\b\d{4}[\/\-]\d{1,2}[\/\-]\d{1,2}\b",    // yyyy-MM-dd, yyyy/MM/dd
                @"\b\w{3}\s+\d{1,2},?\s+\d{4}\b",          // MMM dd, yyyy
                @"\b\d{1,2}\s+\w{3}\s+\d{4}\b"             // dd MMM yyyy
            };

            foreach (var pattern in datePatterns)
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(rawOcrText, pattern);
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    var dateText = match.Value;
                    
                    foreach (var format in dateFormats)
                    {
                        if (DateTime.TryParseExact(dateText, format, 
                            System.Globalization.CultureInfo.InvariantCulture, 
                            System.Globalization.DateTimeStyles.None, out var parsedDate))
                        {
                            // Reasonable date validation (not too far in future or past)
                            if (parsedDate >= DateTime.Now.AddYears(-10) && 
                                parsedDate <= DateTime.Now.AddDays(1))
                            {
                                receiptData.TransactionDate = parsedDate;
                                _logger.LogDebug("Parsed transaction date: {Date} from text: {DateText}", 
                                    parsedDate, dateText);
                                return;
                            }
                        }
                    }
                }
            }

            _logger.LogDebug("Could not parse transaction date from receipt text");
        }
    }
}