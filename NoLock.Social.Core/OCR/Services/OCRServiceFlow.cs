using Microsoft.Extensions.Logging;
using NoLock.Social.Core.OCR.Generated;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Services
{
    /// <summary>
    /// OCR service flow for processing documents
    /// </summary>
    public sealed class OCRServiceFlow<T>(
        Func<byte[], CancellationToken, Task<T>> invokeOcrEndpoint,
        ILogger<ReceiptOCRService> logger
    ) : IOCRService
    where T : IModelOcrResponse
    {
        private readonly Func<byte[], CancellationToken, Task<T>> _invokeOcrEndpoint = invokeOcrEndpoint ?? throw new ArgumentNullException(nameof(invokeOcrEndpoint));
        private readonly ILogger<ReceiptOCRService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        public async Task SubmitDocumentAsync(
            OCRSubmissionRequest request, 
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (request.ImageData is not { Length: > 0 })
                throw new ArgumentException("Image data cannot be empty", nameof(request));

            try
            {
                _logger.LogInformation("Processing document {DocumentType}", request.DocumentType);

                // Image storage removed - handled externally if needed
                _logger.LogDebug("Processing image");

                var receiptResult = await _invokeOcrEndpoint(request.ImageData, ct);

                if (receiptResult?.Error != null)
                {
                    _logger.LogError("OCR processing failed for receipt. Error: {Error}", receiptResult.Error);
                }
                else if (receiptResult?.IsSuccess ?? false)
                {
                    _logger.LogInformation("OCR processing completed for image: {receiptResult}", receiptResult);

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