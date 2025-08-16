using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Processors
{
    /// <summary>
    /// Processes check documents to extract structured data including MICR line information,
    /// amounts, payee details, and validates routing numbers.
    /// Implements the IDocumentProcessor interface for the plugin architecture.
    /// </summary>
    public class CheckProcessor : IDocumentProcessor
    {
        private readonly ILogger<CheckProcessor> _logger;

        /// <summary>
        /// Regular expressions for check-specific patterns.
        /// </summary>
        private static class Patterns
        {
            // MICR line patterns (typically at the bottom of checks)
            public static readonly Regex MicrLine = new Regex(
                @"[⑆⑇⑈⑉]?\s*(\d{9})[⑆⑇]?\s*(\d+)[⑆⑇]?\s*(\d+)",
                RegexOptions.Compiled);

            // Alternative MICR pattern with common OCR substitutions
            public static readonly Regex MicrLineAlternative = new Regex(
                @"[:|\|]?\s*(\d{9})[:|\|]?\s*(\d+)[:|\|]?\s*(\d+)",
                RegexOptions.Compiled);

            // Routing number pattern (9 digits)
            public static readonly Regex RoutingNumber = new Regex(
                @"\b(\d{9})\b",
                RegexOptions.Compiled);

            // Check number patterns
            public static readonly Regex CheckNumber = new Regex(
                @"(?:CHECK\s*#?|NO\.?|NUMBER)[:\s]*(\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            // Amount patterns
            public static readonly Regex NumericAmount = new Regex(
                @"\$\s*(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)",
                RegexOptions.Compiled);

            // Date patterns
            public static readonly Regex Date = new Regex(
                @"(\d{1,2}[/-]\d{1,2}[/-]\d{2,4})",
                RegexOptions.Compiled);

            // Payee pattern (after "PAY TO THE ORDER OF" or similar)
            public static readonly Regex Payee = new Regex(
                @"(?:PAY\s+TO\s+THE\s+ORDER\s+OF|PAY\s+TO|PAYEE)[:\s]*([^\n\r]+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            // Memo/For pattern
            public static readonly Regex Memo = new Regex(
                @"(?:MEMO|FOR)[:\s]*([^\n\r]+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            // Bank name patterns
            public static readonly Regex BankName = new Regex(
                @"(?:BANK|CREDIT\s+UNION|FCU|CU)\s*(?:OF\s+)?([^\n\r]+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            // Written amount patterns
            public static readonly Regex WrittenAmount = new Regex(
                @"(?:DOLLARS?|AND\s+\d{2}/100)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        /// <summary>
        /// Initializes a new instance of the CheckProcessor class.
        /// </summary>
        /// <param name="logger">Logger for diagnostic output.</param>
        public CheckProcessor(ILogger<CheckProcessor> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public string DocumentType => "Check";

        /// <inheritdoc />
        public async Task<object> ProcessAsync(string rawOcrData, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(rawOcrData))
            {
                throw new ArgumentException("Raw OCR data cannot be null or empty.", nameof(rawOcrData));
            }

            _logger.LogInformation("Processing check document");

            var processedCheck = new ProcessedCheck
            {
                RawOcrText = rawOcrData,
                ProcessedAt = DateTime.UtcNow
            };

            try
            {
                // Extract check data from raw OCR text
                processedCheck.CheckData = await ExtractCheckDataAsync(rawOcrData, cancellationToken);
                
                // Calculate confidence score based on extracted fields
                processedCheck.ConfidenceScore = CalculateConfidenceScore(processedCheck.CheckData);
                
                // Validate the processed data
                processedCheck.Validate();
                
                _logger.LogInformation("Check processed successfully with confidence score: {ConfidenceScore}", 
                    processedCheck.ConfidenceScore);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing check document");
                processedCheck.ValidationErrors.Add($"Processing error: {ex.Message}");
            }

            return processedCheck;
        }

        /// <inheritdoc />
        public bool CanProcess(string rawOcrData)
        {
            if (string.IsNullOrWhiteSpace(rawOcrData))
            {
                return false;
            }

            var lowerText = rawOcrData.ToLowerInvariant();

            // Check for check-specific keywords
            var checkKeywords = new[]
            {
                "pay to the order of", "routing", "account", "check", "memo",
                "dollars", "bank", "void", "endorse", "signature"
            };

            var keywordCount = checkKeywords.Count(keyword => lowerText.Contains(keyword));

            // Also check for MICR line patterns
            var hasMicrPattern = Patterns.MicrLine.IsMatch(rawOcrData) || 
                                Patterns.MicrLineAlternative.IsMatch(rawOcrData);

            // Consider it a check if we find at least 3 keywords or a MICR pattern
            return keywordCount >= 3 || hasMicrPattern;
        }

        /// <summary>
        /// Extracts check data from raw OCR text.
        /// </summary>
        private async Task<CheckData> ExtractCheckDataAsync(string rawText, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var checkData = new CheckData();

                // Extract MICR line data first (most reliable for routing/account/check numbers)
                ExtractMicrData(rawText, checkData);

                // Extract other check details
                ExtractBankInformation(rawText, checkData);
                ExtractPayeeAndPayer(rawText, checkData);
                ExtractAmounts(rawText, checkData);
                ExtractDateAndMemo(rawText, checkData);

                // Validate routing number
                if (!string.IsNullOrEmpty(checkData.RoutingNumber))
                {
                    checkData.IsRoutingNumberValid = ValidateRoutingNumber(checkData.RoutingNumber);
                }

                // Check for signature (placeholder for now)
                DetectSignature(rawText, checkData);

                // Verify amount consistency
                VerifyAmountConsistency(checkData);

                return checkData;
            }, cancellationToken);
        }

        /// <summary>
        /// Extracts MICR line data including routing, account, and check numbers.
        /// </summary>
        private void ExtractMicrData(string rawText, CheckData checkData)
        {
            _logger.LogDebug("Extracting MICR data");

            // Try standard MICR pattern first
            var micrMatch = Patterns.MicrLine.Match(rawText);
            if (!micrMatch.Success)
            {
                // Try alternative pattern (common OCR substitutions)
                micrMatch = Patterns.MicrLineAlternative.Match(rawText);
            }

            if (micrMatch.Success && micrMatch.Groups.Count >= 4)
            {
                // Group 1: Routing number (9 digits)
                var routing = micrMatch.Groups[1].Value;
                if (routing.Length == 9)
                {
                    checkData.RoutingNumber = routing;
                    checkData.MicrLine = micrMatch.Value;
                    _logger.LogDebug("Found routing number from MICR: {RoutingNumber}", routing);
                }

                // Group 2: Account number
                var account = micrMatch.Groups[2].Value;
                if (!string.IsNullOrEmpty(account))
                {
                    checkData.AccountNumber = account;
                    _logger.LogDebug("Found account number from MICR: {AccountNumber}", account);
                }

                // Group 3: Check number
                var checkNum = micrMatch.Groups[3].Value;
                if (!string.IsNullOrEmpty(checkNum))
                {
                    checkData.CheckNumber = checkNum;
                    _logger.LogDebug("Found check number from MICR: {CheckNumber}", checkNum);
                }
            }
            else
            {
                // Fall back to individual pattern matching
                ExtractMicrDataFallback(rawText, checkData);
            }
        }

        /// <summary>
        /// Fallback method to extract MICR data using individual patterns.
        /// </summary>
        private void ExtractMicrDataFallback(string rawText, CheckData checkData)
        {
            // Look for routing number (9-digit pattern)
            var lines = rawText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            // MICR line is typically at the bottom of the check
            var bottomLines = lines.Reverse().Take(5).Reverse();
            
            foreach (var line in bottomLines)
            {
                // Look for 9-digit routing number
                var routingMatches = Patterns.RoutingNumber.Matches(line);
                foreach (Match match in routingMatches)
                {
                    var potentialRouting = match.Groups[1].Value;
                    if (potentialRouting.Length == 9 && string.IsNullOrEmpty(checkData.RoutingNumber))
                    {
                        checkData.RoutingNumber = potentialRouting;
                        _logger.LogDebug("Found potential routing number: {RoutingNumber}", potentialRouting);
                        
                        // Try to extract account and check numbers from the same line
                        ExtractAccountAndCheckFromLine(line, checkData, potentialRouting);
                        break;
                    }
                }
            }

            // If no check number found in MICR, try check number pattern
            if (string.IsNullOrEmpty(checkData.CheckNumber))
            {
                var checkNumMatch = Patterns.CheckNumber.Match(rawText);
                if (checkNumMatch.Success)
                {
                    checkData.CheckNumber = checkNumMatch.Groups[1].Value;
                    _logger.LogDebug("Found check number from pattern: {CheckNumber}", checkData.CheckNumber);
                }
            }
        }

        /// <summary>
        /// Extracts account and check numbers from a line containing the routing number.
        /// </summary>
        private void ExtractAccountAndCheckFromLine(string line, CheckData checkData, string routingNumber)
        {
            // Remove the routing number from the line
            var remainingLine = line.Replace(routingNumber, " ");
            
            // Look for other number sequences
            var numberPattern = new Regex(@"\b(\d{4,20})\b");
            var matches = numberPattern.Matches(remainingLine);
            
            var numbers = new List<string>();
            foreach (Match match in matches)
            {
                numbers.Add(match.Groups[1].Value);
            }

            if (numbers.Count > 0)
            {
                // Typically, account number comes before check number
                // Account numbers are usually longer than check numbers
                if (numbers.Count >= 2)
                {
                    checkData.AccountNumber = numbers[0];
                    checkData.CheckNumber = numbers[1];
                }
                else if (numbers.Count == 1)
                {
                    // If only one number, determine if it's account or check based on length
                    if (numbers[0].Length >= 8)
                    {
                        checkData.AccountNumber = numbers[0];
                    }
                    else
                    {
                        checkData.CheckNumber = numbers[0];
                    }
                }
            }
        }

        /// <summary>
        /// Extracts bank information from the raw text.
        /// </summary>
        private void ExtractBankInformation(string rawText, CheckData checkData)
        {
            var bankMatch = Patterns.BankName.Match(rawText);
            if (bankMatch.Success)
            {
                checkData.BankName = bankMatch.Groups[1].Value.Trim();
            }
        }

        /// <summary>
        /// Extracts payee and payer information.
        /// </summary>
        private void ExtractPayeeAndPayer(string rawText, CheckData checkData)
        {
            // Extract payee
            var payeeMatch = Patterns.Payee.Match(rawText);
            if (payeeMatch.Success)
            {
                checkData.Payee = payeeMatch.Groups[1].Value.Trim();
            }

            // Extract payer (usually in the top left corner)
            var lines = rawText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 0)
            {
                // First non-empty line often contains the payer name
                checkData.PayerName = lines[0].Trim();
                
                // Next lines might contain address
                if (lines.Length > 1)
                {
                    var addressLines = lines.Skip(1).Take(2)
                        .Where(l => !l.Contains("PAY TO", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    if (addressLines.Any())
                    {
                        checkData.PayerAddress = string.Join(", ", addressLines);
                    }
                }
            }
        }

        /// <summary>
        /// Extracts numeric and written amounts.
        /// </summary>
        private void ExtractAmounts(string rawText, CheckData checkData)
        {
            // Extract numeric amount
            var numericMatch = Patterns.NumericAmount.Match(rawText);
            if (numericMatch.Success)
            {
                var amountStr = numericMatch.Groups[1].Value.Replace(",", "");
                if (decimal.TryParse(amountStr, NumberStyles.Currency, CultureInfo.InvariantCulture, out var amount))
                {
                    checkData.AmountNumeric = amount;
                    _logger.LogDebug("Found numeric amount: {Amount}", amount);
                }
            }

            // Extract written amount
            ExtractWrittenAmount(rawText, checkData);
        }

        /// <summary>
        /// Extracts and parses the written amount from the check.
        /// </summary>
        private void ExtractWrittenAmount(string rawText, CheckData checkData)
        {
            // Look for common written amount patterns
            // Pattern: "... and XX/100" or "... dollars"
            var writtenAmountPattern = new Regex(
                @"([A-Za-z\s-]+)(?:\s+and\s+(\d{1,2})/100|\s+DOLLARS?)",
                RegexOptions.IgnoreCase);

            var match = writtenAmountPattern.Match(rawText);
            if (match.Success)
            {
                var writtenPart = match.Groups[1].Value.Trim();
                var centsPart = match.Groups[2].Success ? match.Groups[2].Value : "00";
                
                checkData.AmountWritten = $"{writtenPart} and {centsPart}/100";
                
                // Parse the written amount to decimal
                var parsedAmount = ParseWrittenAmount(writtenPart, centsPart);
                if (parsedAmount.HasValue)
                {
                    checkData.AmountWrittenParsed = parsedAmount;
                    _logger.LogDebug("Parsed written amount: {Amount} from '{Written}'", 
                        parsedAmount, checkData.AmountWritten);
                }
            }
            else
            {
                // Try simpler pattern for just finding text before "dollars"
                var simplifiedPattern = new Regex(
                    @"([\w\s-]+)\s+(?:dollars?|and\s+\d{1,2}/100)",
                    RegexOptions.IgnoreCase);
                
                var simpleMatch = simplifiedPattern.Match(rawText);
                if (simpleMatch.Success)
                {
                    checkData.AmountWritten = simpleMatch.Groups[1].Value.Trim();
                    var parsedAmount = ParseWrittenAmount(checkData.AmountWritten, "00");
                    if (parsedAmount.HasValue)
                    {
                        checkData.AmountWrittenParsed = parsedAmount;
                    }
                }
            }
        }

        /// <summary>
        /// Parses written amount text to decimal value.
        /// </summary>
        private decimal? ParseWrittenAmount(string writtenAmount, string cents)
        {
            try
            {
                // Dictionary for number word conversion
                var numberWords = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["zero"] = 0, ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4,
                    ["five"] = 5, ["six"] = 6, ["seven"] = 7, ["eight"] = 8, ["nine"] = 9,
                    ["ten"] = 10, ["eleven"] = 11, ["twelve"] = 12, ["thirteen"] = 13,
                    ["fourteen"] = 14, ["fifteen"] = 15, ["sixteen"] = 16, ["seventeen"] = 17,
                    ["eighteen"] = 18, ["nineteen"] = 19, ["twenty"] = 20, ["thirty"] = 30,
                    ["forty"] = 40, ["fifty"] = 50, ["sixty"] = 60, ["seventy"] = 70,
                    ["eighty"] = 80, ["ninety"] = 90
                };

                var multipliers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["hundred"] = 100, ["thousand"] = 1000, ["million"] = 1000000
                };

                // Clean and normalize the written amount
                writtenAmount = writtenAmount.Trim().ToLowerInvariant();
                writtenAmount = Regex.Replace(writtenAmount, @"[^\w\s-]", " ");
                writtenAmount = Regex.Replace(writtenAmount, @"\s+", " ");

                // Handle hyphenated numbers (e.g., "twenty-one")
                writtenAmount = writtenAmount.Replace("-", " ");

                var words = writtenAmount.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                decimal total = 0;
                decimal current = 0;

                foreach (var word in words)
                {
                    if (numberWords.ContainsKey(word))
                    {
                        current += numberWords[word];
                    }
                    else if (multipliers.ContainsKey(word))
                    {
                        if (word.Equals("thousand", StringComparison.OrdinalIgnoreCase) || 
                            word.Equals("million", StringComparison.OrdinalIgnoreCase))
                        {
                            if (current == 0) current = 1; // Handle cases like "thousand" without a preceding number
                            total += current * multipliers[word];
                            current = 0;
                        }
                        else // hundred
                        {
                            if (current == 0) current = 1;
                            current *= multipliers[word];
                        }
                    }
                    else if (word == "and")
                    {
                        // "and" is often used as a separator, ignore it
                        continue;
                    }
                }

                total += current;

                // Add cents if provided
                if (!string.IsNullOrEmpty(cents) && int.TryParse(cents, out var centsValue))
                {
                    total += centsValue / 100m;
                }

                return total > 0 ? total : (decimal?)null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing written amount: {WrittenAmount}", writtenAmount);
                return null;
            }
        }

        /// <summary>
        /// Extracts date and memo fields.
        /// </summary>
        private void ExtractDateAndMemo(string rawText, CheckData checkData)
        {
            // Extract date
            var dateMatch = Patterns.Date.Match(rawText);
            if (dateMatch.Success)
            {
                if (DateTime.TryParse(dateMatch.Groups[1].Value, out var date))
                {
                    checkData.Date = date;
                }
            }

            // Extract memo
            var memoMatch = Patterns.Memo.Match(rawText);
            if (memoMatch.Success)
            {
                checkData.Memo = memoMatch.Groups[1].Value.Trim();
            }
        }

        /// <summary>
        /// Validates a routing number using the check digit algorithm.
        /// The ABA routing number check digit is calculated using modulo 10.
        /// </summary>
        private bool ValidateRoutingNumber(string routingNumber)
        {
            // First check basic format
            if (string.IsNullOrEmpty(routingNumber) || routingNumber.Length != 9)
            {
                return false;
            }

            if (!routingNumber.All(char.IsDigit))
            {
                return false;
            }

            // Apply the ABA check digit algorithm
            // The formula is: 3(d1 + d4 + d7) + 7(d2 + d5 + d8) + (d3 + d6 + d9) mod 10 = 0
            try
            {
                int[] digits = routingNumber.Select(c => c - '0').ToArray();
                
                int checksum = 3 * (digits[0] + digits[3] + digits[6]) +
                              7 * (digits[1] + digits[4] + digits[7]) +
                              1 * (digits[2] + digits[5] + digits[8]);
                
                bool isValid = (checksum % 10) == 0;
                
                if (!isValid)
                {
                    _logger.LogWarning("Invalid routing number checksum for: {RoutingNumber}", routingNumber);
                }
                else
                {
                    _logger.LogDebug("Valid routing number checksum for: {RoutingNumber}", routingNumber);
                }
                
                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating routing number: {RoutingNumber}", routingNumber);
                return false;
            }
        }

        /// <summary>
        /// Detects if a signature is present on the check.
        /// This is a placeholder implementation for future enhancement with ML-based signature detection.
        /// </summary>
        private void DetectSignature(string rawText, CheckData checkData)
        {
            // Look for signature-related keywords as a basic heuristic
            var signatureKeywords = new[]
            {
                "signature", "authorized", "endorsement", "sign", "signed",
                "x_", "_x", "___", "______" // Common signature line indicators
            };

            var lowerText = rawText.ToLowerInvariant();
            var keywordFound = signatureKeywords.Any(keyword => lowerText.Contains(keyword));

            // Look for signature line patterns
            var signatureLinePattern = new Regex(@"[_X]{3,}|(?:signature|sign|authorized)[:\s]*[_\s]{3,}", 
                RegexOptions.IgnoreCase);
            var hasSignatureLine = signatureLinePattern.IsMatch(rawText);

            // Basic heuristic: if we find signature-related patterns, assume low confidence signature
            if (keywordFound || hasSignatureLine)
            {
                checkData.SignatureDetected = true;
                checkData.SignatureConfidence = 0.3; // Low confidence without actual signature analysis
                _logger.LogDebug("Signature line detected with low confidence");
            }
            else
            {
                checkData.SignatureDetected = false;
                checkData.SignatureConfidence = 0.0;
                _logger.LogDebug("No signature indicators found");
            }

            // TODO: Future enhancement opportunities:
            // 1. Integrate with ML-based signature detection service
            // 2. Analyze image regions for handwriting patterns
            // 3. Compare against known signature samples
            // 4. Detect pressure patterns and stroke characteristics
            // 5. Validate signature position relative to signature line
            
            if (!checkData.SignatureDetected)
            {
                checkData.Notes += "Signature verification pending. ";
            }
        }

        /// <summary>
        /// Verifies consistency between written and numeric amounts.
        /// </summary>
        private void VerifyAmountConsistency(CheckData checkData)
        {
            if (checkData.AmountNumeric.HasValue && checkData.AmountWrittenParsed.HasValue)
            {
                var numeric = checkData.AmountNumeric.Value;
                var written = checkData.AmountWrittenParsed.Value;
                
                // Check if amounts match within a small tolerance (1 cent)
                checkData.AmountsMatch = Math.Abs(numeric - written) < 0.01m;
                
                if (!checkData.AmountsMatch)
                {
                    var difference = Math.Abs(numeric - written);
                    var percentDiff = (difference / Math.Max(numeric, written)) * 100;
                    
                    _logger.LogWarning(
                        "Amount mismatch detected: Numeric=${Numeric:F2}, Written=${Written:F2}, Difference=${Difference:F2} ({PercentDiff:F1}%)",
                        numeric, written, difference, percentDiff);
                    
                    // Add validation error with details
                    checkData.ValidationErrors.Add(
                        $"Amount verification failed: Numeric amount (${numeric:F2}) does not match written amount (${written:F2})");
                    
                    // Determine which amount to trust based on confidence heuristics
                    if (percentDiff > 10)
                    {
                        checkData.Notes += "Significant amount discrepancy detected. Manual review required. ";
                    }
                }
                else
                {
                    _logger.LogDebug("Amounts verified successfully: ${Amount:F2}", numeric);
                }
            }
            else if (checkData.AmountNumeric.HasValue && !checkData.AmountWrittenParsed.HasValue)
            {
                _logger.LogWarning("Could not verify amount - written amount not parsed");
                checkData.ValidationErrors.Add("Written amount could not be parsed for verification");
                checkData.AmountsMatch = false;
            }
            else if (!checkData.AmountNumeric.HasValue && checkData.AmountWrittenParsed.HasValue)
            {
                _logger.LogWarning("Could not verify amount - numeric amount not found");
                checkData.ValidationErrors.Add("Numeric amount not found for verification");
                checkData.AmountsMatch = false;
            }
            else
            {
                _logger.LogWarning("Could not verify amount - both amounts missing");
                checkData.ValidationErrors.Add("Both numeric and written amounts are missing");
                checkData.AmountsMatch = false;
            }
        }

        /// <summary>
        /// Calculates a confidence score based on the completeness of extracted data.
        /// </summary>
        private double CalculateConfidenceScore(CheckData checkData)
        {
            var score = 0.0;
            var maxScore = 10.0;

            // Check for essential fields
            if (!string.IsNullOrWhiteSpace(checkData.RoutingNumber)) score += 2.0;
            if (!string.IsNullOrWhiteSpace(checkData.AccountNumber)) score += 1.5;
            if (!string.IsNullOrWhiteSpace(checkData.CheckNumber)) score += 1.0;
            if (checkData.AmountNumeric.HasValue) score += 2.0;
            if (!string.IsNullOrWhiteSpace(checkData.Payee)) score += 1.5;
            if (checkData.Date.HasValue) score += 1.0;
            if (checkData.IsRoutingNumberValid) score += 0.5;
            if (checkData.AmountsMatch) score += 0.5;

            return Math.Min(score / maxScore, 1.0);
        }
    }
}