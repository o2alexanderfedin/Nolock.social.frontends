using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NoLock.Social.Core.OCR.Generated;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Services
{
    /// <summary>
    /// OCR service specifically for processing receipt documents with CAS integration
    /// </summary>
    public class ReceiptOCRService : IOCRService
    {
        private readonly MistralOCRClient _ocrClient;
        private readonly ILogger<ReceiptOCRService> _logger;

        public ReceiptOCRService(
            MistralOCRClient ocrClient,
            ILogger<ReceiptOCRService> logger)
        {
            _ocrClient = ocrClient ?? throw new ArgumentNullException(nameof(ocrClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SubmitDocumentAsync(
            OCRSubmissionRequest request, 
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (request.ImageData is not { Length: > 0})
                throw new ArgumentException("Image data cannot be empty", nameof(request));

            if (request.DocumentType != DocumentType.Receipt)
                throw new ArgumentException($"ReceiptOCRService can only process Receipt documents, got {request.DocumentType}", nameof(request));

            try
            {
                _logger.LogInformation("Processing receipt document");

                // Image storage removed - handled externally if needed
                _logger.LogDebug("Processing receipt image");

                // Create file parameter for the API
                using var stream = new MemoryStream(request.ImageData);
                var fileParam = new FileParameter(stream, "document");

                // Call Mistral OCR API for receipt processing
                var receiptResult = await _ocrClient.ProcessReceiptOcrAsync(fileParam, ct);
                
                if (receiptResult?.Error != null)
                {
                    _logger.LogError("OCR processing failed for receipt. Error: {Error}", receiptResult.Error);
                }
                else if (receiptResult?.ModelData != null)
                {
                    var receipt = receiptResult.ModelData;
                    _logger.LogInformation("OCR processing completed for receipt. Merchant: {Merchant}, Total: {Total}, Processing Time: {Time}", 
                        receipt.Merchant?.Name ?? "Unknown",
                        receipt.Totals?.Total ?? 0,
                        receiptResult.ProcessingTime);

                    // Result storage removed - handled externally if needed
                    _logger.LogDebug("OCR processing completed successfully");
                }
                else
                {
                    _logger.LogWarning("Received empty result from OCR API for receipt");
                }
            }
            catch (MistralOCRException ex)
            {
                _logger.LogError(ex, "Mistral OCR API error. Status: {StatusCode}, Response: {Response}", 
                    ex.StatusCode, ex.Response);
                
                throw new InvalidOperationException(
                    $"OCR API error: {ex.Message}", 
                    ex);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error calling OCR API");
                throw new InvalidOperationException(
                    "Network error occurred while submitting document", 
                    ex);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "OCR submission cancelled or timed out");
                throw new InvalidOperationException(
                    "OCR submission was cancelled or timed out", 
                    ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during OCR submission");
                throw new InvalidOperationException(
                    "An unexpected error occurred during OCR submission", 
                    ex);
            }
        }
    }
}