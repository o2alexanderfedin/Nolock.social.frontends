using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Services
{
    /// <summary>
    /// Decorator for IOCRService that provides transparent caching of OCR results.
    /// Intercepts OCR operations and uses cache when available to improve performance.
    /// </summary>
    public class OCRServiceWithCache : IOCRService
    {
        private readonly IOCRService _innerService;
        private readonly IOCRResultCache _cache;
        private readonly ILogger<OCRServiceWithCache> _logger;
        private readonly bool _cacheOnlyCompleteResults;
        private readonly int _cacheExpirationMinutes;

        /// <summary>
        /// Initializes a new instance of the OCRServiceWithCache class.
        /// </summary>
        /// <param name="innerService">The underlying OCR service to decorate.</param>
        /// <param name="cache">The OCR result cache.</param>
        /// <param name="logger">Logger for service operations.</param>
        /// <param name="cacheOnlyCompleteResults">Whether to cache only completed results (default: true).</param>
        /// <param name="cacheExpirationMinutes">Cache expiration in minutes (default: 60).</param>
        public OCRServiceWithCache(
            IOCRService innerService,
            IOCRResultCache cache,
            ILogger<OCRServiceWithCache> logger,
            bool cacheOnlyCompleteResults = true,
            int cacheExpirationMinutes = 60)
        {
            _innerService = innerService ?? throw new ArgumentNullException(nameof(innerService));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cacheOnlyCompleteResults = cacheOnlyCompleteResults;
            _cacheExpirationMinutes = cacheExpirationMinutes;
            
            _logger.LogInformation(
                "OCRServiceWithCache initialized. CacheOnlyComplete: {CacheOnlyComplete}, ExpirationMinutes: {ExpirationMinutes}",
                _cacheOnlyCompleteResults, _cacheExpirationMinutes);
        }

        /// <inheritdoc />
        public async Task<OCRSubmissionResponse> SubmitDocumentAsync(
            OCRSubmissionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            try
            {
                // Convert image data to bytes for cache key generation
                var documentBytes = ConvertImageDataToBytes(request.ImageData);
                
                // Check cache first
                var cachedResult = await _cache.GetResultAsync(documentBytes, cancellationToken);
                if (cachedResult != null)
                {
                    _logger.LogInformation(
                        "Cache hit for document submission. Returning cached result with TrackingId: {TrackingId}",
                        cachedResult.TrackingId);
                    
                    // Convert cached status response to submission response
                    return ConvertToSubmissionResponse(cachedResult);
                }

                _logger.LogDebug("Cache miss for document submission. Forwarding to OCR service");
                
                // Cache miss - forward to inner service
                var response = await _innerService.SubmitDocumentAsync(request, cancellationToken);
                
                // Optionally cache the initial submission response if it's already complete
                if (response.Status == OCRProcessingStatus.Complete)
                {
                    await CacheStatusResponseAsync(documentBytes, ConvertToStatusResponse(response), cancellationToken);
                }
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OCRServiceWithCache.SubmitDocumentAsync");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<OCRStatusResponse> GetStatusAsync(
            string trackingId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(trackingId))
            {
                throw new ArgumentNullException(nameof(trackingId));
            }

            try
            {
                // For status checks, we don't have the original document content,
                // so we can't use content-based caching. We'll pass through to the inner service.
                // However, we could implement tracking ID-based caching if needed.
                
                var response = await _innerService.GetStatusAsync(trackingId, cancellationToken);
                
                // If the result is complete and we have a way to associate it with document content,
                // we could cache it here. For now, we'll just return the response.
                
                _logger.LogDebug(
                    "Status check for TrackingId: {TrackingId}, Status: {Status}",
                    trackingId, response.Status);
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OCRServiceWithCache.GetStatusAsync for TrackingId: {TrackingId}", trackingId);
                throw;
            }
        }

        /// <summary>
        /// Caches an OCR status response if it meets caching criteria.
        /// </summary>
        private async Task CacheStatusResponseAsync(
            byte[] documentBytes,
            OCRStatusResponse response,
            CancellationToken cancellationToken)
        {
            try
            {
                // Only cache if configured to cache all results or if result is complete
                if (!_cacheOnlyCompleteResults || response.Status == OCRProcessingStatus.Complete)
                {
                    var cacheKey = await _cache.StoreResultAsync(
                        documentBytes,
                        response,
                        _cacheExpirationMinutes,
                        cancellationToken);
                    
                    _logger.LogInformation(
                        "Cached OCR result with key {CacheKey}, Status: {Status}",
                        cacheKey, response.Status);
                }
            }
            catch (Exception ex)
            {
                // Don't fail the operation if caching fails
                _logger.LogWarning(ex, "Failed to cache OCR result, continuing without cache");
            }
        }

        /// <summary>
        /// Converts base64 image data to byte array.
        /// </summary>
        private byte[] ConvertImageDataToBytes(string imageData)
        {
            if (string.IsNullOrWhiteSpace(imageData))
            {
                return Array.Empty<byte>();
            }

            // Remove data URL prefix if present
            if (imageData.Contains(","))
            {
                imageData = imageData.Substring(imageData.IndexOf(',') + 1);
            }

            try
            {
                return Convert.FromBase64String(imageData);
            }
            catch
            {
                // If not base64, treat as UTF-8 string
                return Encoding.UTF8.GetBytes(imageData);
            }
        }

        /// <summary>
        /// Converts a cached OCR status response to a submission response.
        /// </summary>
        private OCRSubmissionResponse ConvertToSubmissionResponse(OCRStatusResponse statusResponse)
        {
            return new OCRSubmissionResponse
            {
                TrackingId = statusResponse.TrackingId,
                Status = statusResponse.Status,
                SubmittedAt = statusResponse.SubmittedAt,
                EstimatedCompletionTime = statusResponse.CompletedAt ?? 
                    statusResponse.SubmittedAt.AddSeconds(statusResponse.EstimatedSecondsRemaining ?? 0)
            };
        }

        /// <summary>
        /// Converts a submission response to a status response for caching.
        /// </summary>
        private OCRStatusResponse ConvertToStatusResponse(OCRSubmissionResponse submissionResponse)
        {
            var estimatedSeconds = submissionResponse.EstimatedCompletionTime.HasValue
                ? (int)(submissionResponse.EstimatedCompletionTime.Value - DateTime.UtcNow).TotalSeconds
                : 0;
            
            return new OCRStatusResponse
            {
                TrackingId = submissionResponse.TrackingId,
                Status = submissionResponse.Status,
                ProgressPercentage = submissionResponse.Status == OCRProcessingStatus.Complete ? 100 : 0,
                SubmittedAt = submissionResponse.SubmittedAt,
                StartedAt = submissionResponse.Status == OCRProcessingStatus.Processing ? submissionResponse.SubmittedAt : null,
                CompletedAt = submissionResponse.Status == OCRProcessingStatus.Complete ? DateTime.UtcNow : null,
                EstimatedSecondsRemaining = submissionResponse.Status == OCRProcessingStatus.Complete ? 0 : estimatedSeconds,
                StatusMessage = GetStatusMessage(submissionResponse.Status),
                ResultData = null // Will be populated when actual OCR processing completes
            };
        }

        /// <summary>
        /// Extracts document type from OCR status response.
        /// </summary>
        private string? ExtractDocumentType(OCRStatusResponse response)
        {
            if (response.ResultData?.ExtractedFields != null)
            {
                foreach (var field in response.ResultData.ExtractedFields)
                {
                    if (field.FieldName?.Equals("DocumentType", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        return field.Value;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Gets a status message based on processing status.
        /// </summary>
        private string GetStatusMessage(OCRProcessingStatus status)
        {
            return status switch
            {
                OCRProcessingStatus.Queued => "Document is queued for processing",
                OCRProcessingStatus.Processing => "Document is being processed",
                OCRProcessingStatus.Complete => "Document processing completed successfully",
                OCRProcessingStatus.Failed => "Document processing failed",
                _ => "Unknown status"
            };
        }
    }
}