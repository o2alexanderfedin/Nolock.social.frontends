using System;
using System.Linq;
using System.Text.Json;
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
    }
}