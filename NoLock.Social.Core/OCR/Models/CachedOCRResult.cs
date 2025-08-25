using System.Text.Json.Serialization;

namespace NoLock.Social.Core.OCR.Models
{
    /// <summary>
    /// Represents a cached OCR processing result with metadata for cache management.
    /// </summary>
    public class CachedOCRResult
    {
        /// <summary>
        /// The unique cache key (SHA-256 hash of document content).
        /// </summary>
        [JsonPropertyName("cacheKey")]
        public string CacheKey { get; set; } = string.Empty;

        /// <summary>
        /// The cached OCR processing result.
        /// </summary>
        [JsonPropertyName("result")]
        public OCRStatusResponse Result { get; set; } = new();

        /// <summary>
        /// The document type (e.g., "Receipt", "Check", "W4").
        /// </summary>
        [JsonPropertyName("documentType")]
        public string? DocumentType { get; set; }

        /// <summary>
        /// The timestamp when this result was cached.
        /// </summary>
        [JsonPropertyName("cachedAt")]
        public DateTime CachedAt { get; set; }

        /// <summary>
        /// The timestamp when this cache entry expires.
        /// </summary>
        [JsonPropertyName("expiresAt")]
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// The number of times this cached result has been accessed.
        /// </summary>
        [JsonPropertyName("accessCount")]
        public int AccessCount { get; set; }

        /// <summary>
        /// The timestamp of the last access to this cached result.
        /// </summary>
        [JsonPropertyName("lastAccessedAt")]
        public DateTime? LastAccessedAt { get; set; }

        /// <summary>
        /// The size of the cached data in bytes (serialized size).
        /// </summary>
        [JsonPropertyName("sizeBytes")]
        public long SizeBytes { get; set; }

        /// <summary>
        /// Optional metadata for cache management and debugging.
        /// </summary>
        [JsonPropertyName("metadata")]
        public CacheMetadata? Metadata { get; set; }

        /// <summary>
        /// Indicates whether this cache entry has expired.
        /// </summary>
        [JsonIgnore]
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;

        /// <summary>
        /// Gets the remaining time until expiration.
        /// </summary>
        [JsonIgnore]
        public TimeSpan? TimeToLive => 
            IsExpired ? null : ExpiresAt - DateTime.UtcNow;

        /// <summary>
        /// Creates a new CachedOCRResult with the specified parameters.
        /// </summary>
        /// <param name="cacheKey">The cache key (document hash).</param>
        /// <param name="result">The OCR processing result to cache.</param>
        /// <param name="expirationMinutes">Expiration time in minutes.</param>
        /// <param name="documentType">Optional document type.</param>
        /// <returns>A new CachedOCRResult instance.</returns>
        public static CachedOCRResult Create(
            string cacheKey,
            OCRStatusResponse result,
            int expirationMinutes,
            string? documentType = null)
        {
            var now = DateTime.UtcNow;
            return new CachedOCRResult
            {
                CacheKey = cacheKey,
                Result = result,
                DocumentType = documentType ?? DetermineDocumentType(result),
                CachedAt = now,
                ExpiresAt = now.AddMinutes(expirationMinutes),
                AccessCount = 0,
                LastAccessedAt = null,
                SizeBytes = 0, // Will be calculated during serialization
                Metadata = new CacheMetadata
                {
                    SourceTrackingId = result.TrackingId,
                    ProcessingStatus = result.Status.ToString(),
                    ConfidenceScore = result.ResultData?.ConfidenceScore,
                    ProcessingTimeMs = result.ResultData?.Metrics?.ProcessingTimeMs
                }
            };
        }

        /// <summary>
        /// Updates access tracking information.
        /// </summary>
        public void RecordAccess()
        {
            AccessCount++;
            LastAccessedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Determines the document type from the OCR result if not explicitly provided.
        /// </summary>
        private static string? DetermineDocumentType(OCRStatusResponse result)
        {
            // Try to extract document type from result data
            if (result.ResultData?.ExtractedFields != null)
            {
                foreach (var field in result.ResultData.ExtractedFields)
                {
                    if (field.FieldName?.Equals("DocumentType", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        return field.Value;
                    }
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Additional metadata for cache management and debugging.
    /// </summary>
    public class CacheMetadata
    {
        /// <summary>
        /// The original tracking ID from the OCR service.
        /// </summary>
        [JsonPropertyName("sourceTrackingId")]
        public string? SourceTrackingId { get; set; }

        /// <summary>
        /// The processing status when cached.
        /// </summary>
        [JsonPropertyName("processingStatus")]
        public string? ProcessingStatus { get; set; }

        /// <summary>
        /// The confidence score of the OCR result.
        /// </summary>
        [JsonPropertyName("confidenceScore")]
        public double? ConfidenceScore { get; set; }

        /// <summary>
        /// The processing time in milliseconds.
        /// </summary>
        [JsonPropertyName("processingTimeMs")]
        public long? ProcessingTimeMs { get; set; }

        /// <summary>
        /// Custom tags for filtering or categorization.
        /// </summary>
        [JsonPropertyName("tags")]
        public string[]? Tags { get; set; }

        /// <summary>
        /// The version of the cache format.
        /// </summary>
        [JsonPropertyName("cacheVersion")]
        public string CacheVersion { get; set; } = "1.0";
    }
}