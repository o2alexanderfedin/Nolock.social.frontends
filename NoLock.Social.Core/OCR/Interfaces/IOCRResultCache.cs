using System;
using System.Threading;
using System.Threading.Tasks;
using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Interfaces
{
    /// <summary>
    /// Defines the contract for OCR result caching operations.
    /// Provides methods to store, retrieve, and manage cached OCR processing results.
    /// </summary>
    public interface IOCRResultCache
    {
        /// <summary>
        /// Stores an OCR processing result in the cache.
        /// </summary>
        /// <param name="documentContent">The original document content (used to generate cache key).</param>
        /// <param name="result">The OCR processing result to cache.</param>
        /// <param name="expirationMinutes">Optional expiration time in minutes. If not specified, uses default expiration.</param>
        /// <param name="cancellationToken">Cancellation token for the async operation.</param>
        /// <returns>The cache key (hash) for the stored result.</returns>
        ValueTask<string> StoreResultAsync(
            byte[] documentContent,
            OCRStatusResponse result,
            int? expirationMinutes = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a cached OCR processing result by document content.
        /// </summary>
        /// <param name="documentContent">The original document content (used to generate cache key).</param>
        /// <param name="cancellationToken">Cancellation token for the async operation.</param>
        /// <returns>The cached OCR result if found and not expired; otherwise, null.</returns>
        ValueTask<OCRStatusResponse?> GetResultAsync(
            byte[] documentContent,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a cached OCR processing result by cache key.
        /// </summary>
        /// <param name="cacheKey">The cache key (hash) for the result.</param>
        /// <param name="cancellationToken">Cancellation token for the async operation.</param>
        /// <returns>The cached OCR result if found and not expired; otherwise, null.</returns>
        ValueTask<OCRStatusResponse?> GetResultByKeyAsync(
            string cacheKey,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a cached result exists for the given document content.
        /// </summary>
        /// <param name="documentContent">The original document content (used to generate cache key).</param>
        /// <param name="cancellationToken">Cancellation token for the async operation.</param>
        /// <returns>True if a valid (non-expired) cached result exists; otherwise, false.</returns>
        ValueTask<bool> ExistsAsync(
            byte[] documentContent,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidates (removes) a cached result by document content.
        /// </summary>
        /// <param name="documentContent">The original document content (used to generate cache key).</param>
        /// <param name="cancellationToken">Cancellation token for the async operation.</param>
        /// <returns>True if the cached result was removed; false if it didn't exist.</returns>
        ValueTask<bool> InvalidateAsync(
            byte[] documentContent,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidates (removes) a cached result by cache key.
        /// </summary>
        /// <param name="cacheKey">The cache key (hash) for the result.</param>
        /// <param name="cancellationToken">Cancellation token for the async operation.</param>
        /// <returns>True if the cached result was removed; false if it didn't exist.</returns>
        ValueTask<bool> InvalidateByKeyAsync(
            string cacheKey,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidates all cached results of a specific document type.
        /// </summary>
        /// <param name="documentType">The document type to invalidate (e.g., "Receipt", "Check", "W4").</param>
        /// <param name="cancellationToken">Cancellation token for the async operation.</param>
        /// <returns>The number of cached results that were invalidated.</returns>
        ValueTask<int> InvalidateByTypeAsync(
            string documentType,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears all cached OCR results.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the async operation.</param>
        /// <returns>The number of cached results that were cleared.</returns>
        ValueTask<int> ClearAllAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes expired cached results.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the async operation.</param>
        /// <returns>The number of expired results that were removed.</returns>
        ValueTask<int> CleanupExpiredAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets cache statistics.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the async operation.</param>
        /// <returns>Cache statistics including hit rate, size, and entry count.</returns>
        ValueTask<CacheStatistics> GetStatisticsAsync(
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents cache statistics for monitoring and diagnostics.
    /// </summary>
    public class CacheStatistics
    {
        /// <summary>
        /// Total number of cache hits.
        /// </summary>
        public long HitCount { get; set; }

        /// <summary>
        /// Total number of cache misses.
        /// </summary>
        public long MissCount { get; set; }

        /// <summary>
        /// Cache hit rate percentage (0-100).
        /// </summary>
        public double HitRate => 
            (HitCount + MissCount) > 0 
                ? (double)HitCount / (HitCount + MissCount) * 100 
                : 0;

        /// <summary>
        /// Total number of entries in the cache.
        /// </summary>
        public int EntryCount { get; set; }

        /// <summary>
        /// Total size of cached data in bytes.
        /// </summary>
        public long TotalSizeBytes { get; set; }

        /// <summary>
        /// Number of expired entries removed.
        /// </summary>
        public long ExpiredEntriesRemoved { get; set; }

        /// <summary>
        /// Last cleanup timestamp.
        /// </summary>
        public DateTime? LastCleanupTime { get; set; }

        /// <summary>
        /// Average cached entry size in bytes.
        /// </summary>
        public double AverageEntrySizeBytes => 
            EntryCount > 0 
                ? (double)TotalSizeBytes / EntryCount 
                : 0;
    }
}