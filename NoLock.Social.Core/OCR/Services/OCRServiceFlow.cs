using Microsoft.Extensions.Logging;
using NoLock.Social.Core.OCR.Generated;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Models;
using NoLock.Social.Core.Storage.Interfaces;

namespace NoLock.Social.Core.OCR.Services
{
    /// <summary>
    /// OCR service specifically for processing receipt documents with CAS integration
    /// </summary>
    public sealed class OCRServiceFlow<T>(
        Func<byte[], CancellationToken, Task<T>> invokeOcrEndpoint,
        ICASService casService,
        ILogger<ReceiptOCRService> logger
    ) : IOCRService
    where T : IModelOcrResponse
    {
        private readonly Func<byte[], CancellationToken, Task<T>> _invokeOcrEndpoint = invokeOcrEndpoint ?? throw new ArgumentNullException(nameof(_invokeOcrEndpoint));
        private readonly ICASService _casService = casService ?? throw new ArgumentNullException(nameof(casService));
        private readonly ILogger<ReceiptOCRService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        public async Task<OCRSubmissionResponse> SubmitDocumentAsync(
            OCRSubmissionRequest request, 
            CancellationToken ct = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrWhiteSpace(request.ImageData))
                throw new ArgumentException("Image data cannot be empty", nameof(request));

            try
            {
                _logger.LogInformation("Processing receipt document. ClientRequestId: {ClientRequestId}",
                    request.ClientRequestId);

                // Store image in CAS
                var imageHash = await _casService.StoreAsync(
                    request.ImageData,
                    ct);

                _logger.LogDebug("Stored receipt image in CAS with hash: {Hash}", imageHash);

                // Convert base64 to byte array for API call
                byte[] imageBytes;
                try
                {
                    imageBytes = Convert.FromBase64String(request.ImageData);
                }
                catch (FormatException ex)
                {
                    throw new ArgumentException("Invalid base64 image data", nameof(request), ex);
                }

                var receiptResult = await _invokeOcrEndpoint(imageBytes, ct);

                // Process the result
                var response = new OCRSubmissionResponse
                {
                    SubmittedAt = DateTime.UtcNow,
                    TrackingId = Guid.NewGuid().ToString("N"),
                    EstimatedCompletionTime = DateTime.UtcNow
                };

                if (receiptResult?.Error != null)
                {
                    _logger.LogError("OCR processing failed for receipt. Error: {Error}",
                        receiptResult.Error);
                    response.Status = OCRProcessingStatus.Failed;
                }
                else if (receiptResult?.IsSuccess ?? false)
                {
                    _logger.LogInformation("OCR processing completed for image: {receiptResult}", receiptResult);

                    response.Status = OCRProcessingStatus.Complete;

                    // Store OCR result in CAS
                    var resultHash = await _casService.StoreAsync(receiptResult, ct);

                    _logger.LogDebug("Stored OCR result in CAS with hash: {Hash}", resultHash);
                }
                else
                {
                    _logger.LogWarning("Received empty result from OCR API for receipt");
                    response.Status = OCRProcessingStatus.Failed;
                }

                return response;
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