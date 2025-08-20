using System;
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
    /// Thin processor layer for check documents that delegates to backend OCR service.
    /// The backend service handles all parsing, extraction, and validation logic.
    /// </summary>
    public class CheckProcessor : IDocumentProcessor
    {
        private readonly ILogger<CheckProcessor> _logger;
        private readonly IOCRService _ocrService;

        /// <summary>
        /// Initializes a new instance of the CheckProcessor class.
        /// </summary>
        /// <param name="logger">Logger for diagnostic output.</param>
        /// <param name="ocrService">Backend OCR service for processing.</param>
        public CheckProcessor(ILogger<CheckProcessor> logger, IOCRService ocrService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _ocrService = ocrService ?? throw new ArgumentNullException(nameof(ocrService));
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

            _logger.LogInformation("Sending check document to backend OCR service");

            try
            {
                // Submit document to backend OCR service
                // Note: rawOcrData should be base64 image data from the camera/scanner
                var submissionRequest = new OCRSubmissionRequest
                {
                    ImageData = rawOcrData,
                    DocumentType = Models.DocumentType.Form, // Checks are a type of form
                    Metadata = new System.Collections.Generic.Dictionary<string, string>
                    {
                        { "ProcessorType", "Check" },
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

                // Backend returns CheckData as JSON in StructuredData field
                CheckData checkData = null;
                if (!string.IsNullOrWhiteSpace(statusResponse.ResultData?.StructuredData))
                {
                    try
                    {
                        checkData = JsonSerializer.Deserialize<CheckData>(
                            statusResponse.ResultData.StructuredData,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Failed to deserialize CheckData from backend response");
                        checkData = new CheckData();
                    }
                }
                else
                {
                    // Fallback: If no structured data, at least populate with extracted text
                    checkData = new CheckData();
                    _logger.LogWarning("No structured data received from backend OCR service");
                }

                var processedCheck = new ProcessedCheck
                {
                    RawOcrText = rawOcrData,
                    CheckData = checkData,
                    ConfidenceScore = statusResponse.ResultData?.ConfidenceScore / 100.0 ?? 0,
                    ProcessedAt = DateTime.UtcNow
                };
                
                // Post-process the check data for business logic
                PostProcessCheckData(processedCheck, rawOcrData);
                
                // Basic validation of the response
                processedCheck.Validate();
                
                _logger.LogInformation("Check processed successfully with confidence score: {ConfidenceScore}", 
                    processedCheck.ConfidenceScore);
                
                return processedCheck;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing check document");
                
                // Return error result
                var errorResult = new ProcessedCheck
                {
                    RawOcrText = rawOcrData,
                    CheckData = new CheckData(),
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

            // Check for MICR patterns first (strong indicators of checks)
            if (ContainsMicrPattern(rawOcrData))
            {
                return true;
            }

            var lowerText = rawOcrData.ToLowerInvariant();

            // Simple keyword detection to identify check documents
            // The backend will do the actual validation
            var checkKeywords = new[]
            {
                "pay to the order of", "routing", "account", "check", "memo",
                "dollars", "bank", "void", "endorse", "signature"
            };

            var keywordCount = checkKeywords.Count(keyword => lowerText.Contains(keyword));

            // Consider it a check if we find at least 3 keywords
            return keywordCount >= 3;
        }

        /// <summary>
        /// Checks if the text contains MICR (Magnetic Ink Character Recognition) patterns
        /// commonly found on checks.
        /// </summary>
        /// <param name="text">The text to analyze.</param>
        /// <returns>True if MICR patterns are detected.</returns>
        private bool ContainsMicrPattern(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            // MICR patterns use special characters ⑆ (U+2446) or : as separators
            // Format: ⑆routing⑆account⑆check or :routing:account:check
            // Routing numbers are 9 digits, account numbers vary, check numbers vary

            // Check for ⑆ separated MICR pattern
            if (text.Contains("⑆") && Regex.IsMatch(text, @"⑆\d{9}⑆\d+⑆\d+"))
            {
                return true;
            }

            // Check for : separated MICR pattern  
            if (Regex.IsMatch(text, @":\d{9}:\d+:\d+"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Post-processes check data to handle business logic calculations and validation.
        /// This includes signature detection, confidence scoring, and enhanced validation.
        /// </summary>
        /// <param name="processedCheck">The processed check to enhance.</param>
        /// <param name="rawOcrData">The raw OCR data for signature detection.</param>
        private void PostProcessCheckData(ProcessedCheck processedCheck, string rawOcrData)
        {
            if (processedCheck?.CheckData == null)
            {
                return;
            }

            var checkData = processedCheck.CheckData;

            // Detect signature indicators in the raw OCR text
            DetectSignatureIndicators(checkData, rawOcrData);

            // Calculate confidence score based on data completeness
            var confidenceScore = CalculateConfidenceScore(checkData);
            // Always use the calculated confidence score for business logic requirements
            processedCheck.ConfidenceScore = confidenceScore;
            _logger.LogDebug("Set confidence score to: {ConfidenceScore} (from calculated score)", confidenceScore);

            // Enhanced validation for check-specific business rules
            ValidateCheckData(checkData);
        }

        /// <summary>
        /// Detects signature indicators in the raw OCR text and updates check data.
        /// </summary>
        /// <param name="checkData">The check data to update.</param>
        /// <param name="rawOcrData">The raw OCR text to analyze.</param>
        private void DetectSignatureIndicators(CheckData checkData, string rawOcrData)
        {
            if (string.IsNullOrWhiteSpace(rawOcrData))
            {
                checkData.SignatureDetected = false;
                checkData.SignatureConfidence = 0;
                return;
            }

            var lowerText = rawOcrData.ToLowerInvariant();
            var signatureIndicators = new[]
            {
                "signature", "sign", "authorized signature", "signature line",
                "_____", "___", "__", "x____", "signature here"
            };

            var signatureCount = signatureIndicators.Count(indicator => lowerText.Contains(indicator));
            
            checkData.SignatureDetected = signatureCount > 0;
            
            // Calculate signature confidence based on number of indicators found
            if (signatureCount > 0)
            {
                checkData.SignatureConfidence = Math.Min(1.0, signatureCount * 0.3);
                _logger.LogDebug("Detected {Count} signature indicators, confidence: {Confidence}", 
                    signatureCount, checkData.SignatureConfidence);
            }
            else
            {
                checkData.SignatureConfidence = 0;
            }
        }

        /// <summary>
        /// Calculates confidence score based on the completeness of check data.
        /// Weighting: routing number (30%), account number (25%), amount (25%), date (10%), signature indicators (10%)
        /// </summary>
        /// <param name="checkData">The check data to evaluate.</param>
        /// <returns>A confidence score between 0.0 and 1.0.</returns>
        private double CalculateConfidenceScore(CheckData checkData)
        {
            var score = 0.0;
            var maxScore = 1.0;

            // Routing number (weight: 0.3)
            if (!string.IsNullOrWhiteSpace(checkData.RoutingNumber) && checkData.RoutingNumber.Length == 9)
                score += 0.3;

            // Account number (weight: 0.25)
            if (!string.IsNullOrWhiteSpace(checkData.AccountNumber))
                score += 0.25;

            // Amount (weight: 0.25)
            if (checkData.AmountNumeric.HasValue && checkData.AmountNumeric.Value > 0)
                score += 0.25;

            // Date (weight: 0.1)
            if (checkData.Date.HasValue)
                score += 0.1;

            // Signature indicators (weight: 0.1)
            if (checkData.SignatureDetected)
                score += 0.1;

            return Math.Min(maxScore, score);
        }

        /// <summary>
        /// Performs enhanced validation of check data for business rules.
        /// </summary>
        /// <param name="checkData">The check data to validate.</param>
        private void ValidateCheckData(CheckData checkData)
        {
            // Add specific validation errors for invalid check data
            if (string.IsNullOrWhiteSpace(checkData.RoutingNumber))
            {
                checkData.ValidationErrors.Add("Routing number is required for check processing");
            }

            if (string.IsNullOrWhiteSpace(checkData.AccountNumber))
            {
                checkData.ValidationErrors.Add("Account number is required for check processing");
            }

            if (string.IsNullOrWhiteSpace(checkData.CheckNumber))
            {
                checkData.ValidationErrors.Add("Check number is required for identification");
            }

            if (!checkData.AmountNumeric.HasValue || checkData.AmountNumeric.Value <= 0)
            {
                checkData.ValidationErrors.Add("Valid numeric amount is required");
            }

            if (string.IsNullOrWhiteSpace(checkData.Payee))
            {
                checkData.ValidationErrors.Add("Payee information is required");
            }

            _logger.LogDebug("Check validation completed with {ErrorCount} errors", 
                checkData.ValidationErrors.Count);
        }
    }
}