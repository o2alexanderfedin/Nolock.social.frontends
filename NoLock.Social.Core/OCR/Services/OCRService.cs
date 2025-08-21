using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NoLock.Social.Core.OCR.Generated;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Services
{
    /// <summary>
    /// OCR service that calls the Mistral OCR API using a swagger-generated client
    /// </summary>
    public class OCRService : IOCRService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OCRService> _logger;
        private const string BaseUrl = "https://nolock-ocr-services-qbhx5.ondigitalocean.app";

        public OCRService(HttpClient httpClient, ILogger<OCRService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<OCRSubmissionResponse> SubmitDocumentAsync(
            OCRSubmissionRequest request, 
            CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrWhiteSpace(request.ImageData))
                throw new ArgumentException("Image data cannot be empty", nameof(request));

            try
            {
                _logger.LogInformation("Submitting document for OCR processing. Type: {DocumentType}, ClientRequestId: {ClientRequestId}", 
                    request.DocumentType, request.ClientRequestId);

                // Initialize the generated client
                var client = new MistralOCRClient(BaseUrl, _httpClient);

                // Convert base64 to byte array
                byte[] imageBytes;
                try
                {
                    imageBytes = Convert.FromBase64String(request.ImageData);
                }
                catch (FormatException ex)
                {
                    throw new ArgumentException("Invalid base64 image data", nameof(request), ex);
                }

                // Create file parameter for the API
                using var stream = new MemoryStream(imageBytes);
                var fileParam = new FileParameter(stream, "document.jpg");

                // Route to the appropriate endpoint based on document type
                string trackingId;
                switch (request.DocumentType)
                {
                    case DocumentType.Receipt:
                        var receiptResult = await client.ProcessReceiptOcrAsync(fileParam, cancellationToken);
                        trackingId = ExtractTrackingId(receiptResult);
                        break;

                    case DocumentType.Invoice:
                        // Route invoice to receipt endpoint as they're similar
                        _logger.LogInformation("Routing {DocumentType} to receipt endpoint", request.DocumentType);
                        var invoiceResult = await client.ProcessReceiptOcrAsync(fileParam, cancellationToken);
                        trackingId = ExtractTrackingId(invoiceResult);
                        break;

                    default:
                        // For unsupported types, route to receipt endpoint as default
                        _logger.LogWarning("Document type {DocumentType} not directly supported, routing to receipt endpoint", 
                            request.DocumentType);
                        var defaultResult = await client.ProcessReceiptOcrAsync(fileParam, cancellationToken);
                        trackingId = ExtractTrackingId(defaultResult);
                        break;
                }

                _logger.LogInformation("Document submitted successfully. TrackingId: {TrackingId}", trackingId);

                return new OCRSubmissionResponse
                {
                    TrackingId = trackingId,
                    Status = OCRProcessingStatus.Queued,
                    SubmittedAt = DateTime.UtcNow,
                    EstimatedCompletionTime = DateTime.UtcNow.AddMinutes(2)
                };
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

        public async Task<OCRStatusResponse> GetStatusAsync(
            string trackingId, 
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(trackingId))
                throw new ArgumentNullException(nameof(trackingId));

            try
            {
                _logger.LogDebug("Checking OCR status for TrackingId: {TrackingId}", trackingId);

                // Initialize the generated client
                var client = new MistralOCRClient(BaseUrl, _httpClient);

                // Note: The swagger spec doesn't show a status endpoint
                // This might need to be implemented differently based on actual API
                // For now, returning a mock response
                
                // TODO: Replace with actual status check when endpoint is available
                _logger.LogWarning("Status endpoint not available in current API spec, returning mock status");
                
                return new OCRStatusResponse
                {
                    TrackingId = trackingId,
                    Status = OCRProcessingStatus.Processing,
                    ProgressPercentage = 50,
                    SubmittedAt = DateTime.UtcNow.AddMinutes(-1),
                    StatusMessage = "Processing document",
                    QueuePosition = null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking OCR status for TrackingId: {TrackingId}", trackingId);
                throw new InvalidOperationException(
                    $"Failed to check status for tracking ID: {trackingId}", 
                    ex);
            }
        }

        private string ExtractTrackingId(AcceptedResult? result)
        {
            // Extract tracking ID from the AcceptedResult
            // Check if the result contains a tracking ID in its Value property
            if (result?.Value != null)
            {
                // Try to extract tracking ID from the value
                var valueStr = result.Value.ToString();
                if (!string.IsNullOrWhiteSpace(valueStr))
                {
                    _logger.LogDebug("Extracted tracking ID from response: {TrackingId}", valueStr);
                    return valueStr;
                }
            }
            
            // If no tracking ID in response, generate a unique ID
            var trackingId = Guid.NewGuid().ToString("N");
            _logger.LogDebug("Generated tracking ID: {TrackingId}", trackingId);
            
            return trackingId;
        }

        private string DetectImageContentType(byte[] imageBytes)
        {
            // Simple image type detection based on file headers
            if (imageBytes.Length < 4)
                return "application/octet-stream";

            // JPEG
            if (imageBytes[0] == 0xFF && imageBytes[1] == 0xD8)
                return "image/jpeg";

            // PNG
            if (imageBytes[0] == 0x89 && imageBytes[1] == 0x50 && imageBytes[2] == 0x4E && imageBytes[3] == 0x47)
                return "image/png";

            // GIF
            if (imageBytes[0] == 0x47 && imageBytes[1] == 0x49 && imageBytes[2] == 0x46)
                return "image/gif";

            // BMP
            if (imageBytes[0] == 0x42 && imageBytes[1] == 0x4D)
                return "image/bmp";

            // WebP
            if (imageBytes.Length > 12 &&
                imageBytes[0] == 0x52 && imageBytes[1] == 0x49 && imageBytes[2] == 0x46 && imageBytes[3] == 0x46 &&
                imageBytes[8] == 0x57 && imageBytes[9] == 0x45 && imageBytes[10] == 0x42 && imageBytes[11] == 0x50)
                return "image/webp";

            // TIFF
            if ((imageBytes[0] == 0x49 && imageBytes[1] == 0x49 && imageBytes[2] == 0x2A && imageBytes[3] == 0x00) ||
                (imageBytes[0] == 0x4D && imageBytes[1] == 0x4D && imageBytes[2] == 0x00 && imageBytes[3] == 0x2A))
                return "image/tiff";

            // Default
            return "application/octet-stream";
        }
    }
}