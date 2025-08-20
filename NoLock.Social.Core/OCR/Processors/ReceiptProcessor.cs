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
                ReceiptData receiptData = null;
                if (!string.IsNullOrWhiteSpace(statusResponse.ResultData?.StructuredData))
                {
                    try
                    {
                        receiptData = JsonSerializer.Deserialize<ReceiptData>(
                            statusResponse.ResultData.StructuredData,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Failed to deserialize ReceiptData from backend response");
                        receiptData = new ReceiptData();
                    }
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
    }
}