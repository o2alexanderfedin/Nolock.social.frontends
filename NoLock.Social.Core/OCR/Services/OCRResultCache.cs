using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NoLock.Social.Core.Hashing;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Models;
using NoLock.Social.Core.Storage;

namespace NoLock.Social.Core.OCR.Services
{
    /// <summary>
    /// Implementation of OCR result caching using Content-Addressable Storage.
    /// Provides efficient caching of OCR processing results with expiration and statistics.
    /// </summary>
    public class OCRResultCache : IOCRResultCache
    {
        private readonly IContentAddressableStorage _storage;
        private readonly IHashAlgorithm _hashAlgorithm;
        private readonly ILogger<OCRResultCache> _logger;
        private readonly SemaphoreSlim _statisticsLock;
        private readonly int _defaultExpirationMinutes;
        
        // In-memory statistics (could be persisted to storage if needed)
        private long _hitCount;
        private long _missCount;
        private long _expiredEntriesRemoved;
        private DateTime? _lastCleanupTime;

        /// <summary>
        /// Initializes a new instance of the OCRResultCache class.
        /// </summary>
        /// <param name="storage">The content-addressable storage backend.</param>
        /// <param name="hashAlgorithm">The hash algorithm for generating cache keys.</param>
        /// <param name="logger">Logger for cache operations.</param>
        /// <param name="defaultExpirationMinutes">Default cache expiration in minutes (default: 60).</param>
        public OCRResultCache(
            IContentAddressableStorage storage,
            IHashAlgorithm hashAlgorithm,
            ILogger<OCRResultCache> logger,
            int defaultExpirationMinutes = 60)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _hashAlgorithm = hashAlgorithm ?? throw new ArgumentNullException(nameof(hashAlgorithm));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _defaultExpirationMinutes = defaultExpirationMinutes > 0 ? defaultExpirationMinutes : 60;
            _statisticsLock = new SemaphoreSlim(1, 1);
            
            _logger.LogInformation(
                "OCRResultCache initialized with default expiration of {ExpirationMinutes} minutes",
                _defaultExpirationMinutes);
        }

        /// <inheritdoc />
        public async ValueTask<string> StoreResultAsync(
            byte[] documentContent,
            OCRStatusResponse result,
            int? expirationMinutes = null,
            CancellationToken cancellationToken = default)
        {
            if (documentContent == null || documentContent.Length == 0)
            {
                throw new ArgumentNullException(nameof(documentContent));
            }
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            try
            {
                // Generate cache key from document content
                var cacheKey = GenerateCacheKey(documentContent);
                var expiration = expirationMinutes ?? _defaultExpirationMinutes;
                
                // Create cached result with metadata
                var cachedResult = CachedOCRResult.Create(
                    cacheKey,
                    result,
                    expiration,
                    ExtractDocumentType(result));

                // Serialize the cached result
                var json = JsonSerializer.Serialize(cachedResult);
                var bytes = Encoding.UTF8.GetBytes(json);
                
                // Update size information
                cachedResult.SizeBytes = bytes.Length;
                
                // Re-serialize with updated size
                json = JsonSerializer.Serialize(cachedResult);
                bytes = Encoding.UTF8.GetBytes(json);

                // Store in CAS
                await _storage.StoreAsync(bytes);
                
                _logger.LogInformation(
                    "Cached OCR result with key {CacheKey}, expires at {ExpiresAt}, size {SizeBytes} bytes",
                    cacheKey, cachedResult.ExpiresAt, bytes.Length);

                return cacheKey;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store OCR result in cache");
                throw;
            }
        }

        /// <inheritdoc />
        public async ValueTask<OCRStatusResponse?> GetResultAsync(
            byte[] documentContent,
            CancellationToken cancellationToken = default)
        {
            if (documentContent == null || documentContent.Length == 0)
            {
                return null;
            }

            var cacheKey = GenerateCacheKey(documentContent);
            return await GetResultByKeyAsync(cacheKey, cancellationToken);
        }

        /// <inheritdoc />
        public async ValueTask<OCRStatusResponse?> GetResultByKeyAsync(
            string cacheKey,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                return null;
            }

            try
            {
                // Retrieve from CAS
                var bytes = await _storage.GetAsync(cacheKey);
                if (bytes == null)
                {
                    await IncrementMissCountAsync();
                    _logger.LogDebug("Cache miss for key {CacheKey}", cacheKey);
                    return null;
                }

                // Deserialize cached result
                var json = Encoding.UTF8.GetString(bytes);
                var cachedResult = JsonSerializer.Deserialize<CachedOCRResult>(json);
                
                if (cachedResult == null)
                {
                    await IncrementMissCountAsync();
                    _logger.LogWarning("Failed to deserialize cached result for key {CacheKey}", cacheKey);
                    return null;
                }

                // Check expiration
                if (cachedResult.IsExpired)
                {
                    await IncrementMissCountAsync();
                    _logger.LogDebug("Cached result for key {CacheKey} has expired", cacheKey);
                    
                    // Remove expired entry
                    await _storage.DeleteAsync(cacheKey);
                    await IncrementExpiredCountAsync();
                    
                    return null;
                }

                // Update access tracking
                cachedResult.RecordAccess();
                
                // Re-serialize with updated access info (optional, could be skipped for performance)
                json = JsonSerializer.Serialize(cachedResult);
                bytes = Encoding.UTF8.GetBytes(json);
                await _storage.StoreAsync(bytes);

                await IncrementHitCountAsync();
                _logger.LogDebug(
                    "Cache hit for key {CacheKey}, accessed {AccessCount} times",
                    cacheKey, cachedResult.AccessCount);

                return cachedResult.Result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve cached result for key {CacheKey}", cacheKey);
                await IncrementMissCountAsync();
                return null;
            }
        }

        /// <inheritdoc />
        public async ValueTask<bool> ExistsAsync(
            byte[] documentContent,
            CancellationToken cancellationToken = default)
        {
            if (documentContent == null || documentContent.Length == 0)
            {
                return false;
            }

            var cacheKey = GenerateCacheKey(documentContent);
            
            // Check if exists and not expired
            var result = await GetResultByKeyAsync(cacheKey, cancellationToken);
            return result != null;
        }

        /// <inheritdoc />
        public async ValueTask<bool> InvalidateAsync(
            byte[] documentContent,
            CancellationToken cancellationToken = default)
        {
            if (documentContent == null || documentContent.Length == 0)
            {
                return false;
            }

            var cacheKey = GenerateCacheKey(documentContent);
            return await InvalidateByKeyAsync(cacheKey, cancellationToken);
        }

        /// <inheritdoc />
        public async ValueTask<bool> InvalidateByKeyAsync(
            string cacheKey,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                return false;
            }

            try
            {
                var deleted = await _storage.DeleteAsync(cacheKey);
                if (deleted)
                {
                    _logger.LogInformation("Invalidated cache entry with key {CacheKey}", cacheKey);
                }
                return deleted;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to invalidate cache entry with key {CacheKey}", cacheKey);
                return false;
            }
        }

        /// <inheritdoc />
        public async ValueTask<int> InvalidateByTypeAsync(
            string documentType,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(documentType))
            {
                return 0;
            }

            int invalidatedCount = 0;
            
            try
            {
                // Get all cache keys
                var keys = new List<string>();
                await foreach (var key in _storage.GetAllHashesAsync())
                {
                    keys.Add(key);
                }

                // Check each cached item
                foreach (var key in keys)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var bytes = await _storage.GetAsync(key);
                    if (bytes != null)
                    {
                        var json = Encoding.UTF8.GetString(bytes);
                        var cachedResult = JsonSerializer.Deserialize<CachedOCRResult>(json);
                        
                        if (cachedResult?.DocumentType?.Equals(documentType, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            if (await _storage.DeleteAsync(key))
                            {
                                invalidatedCount++;
                            }
                        }
                    }
                }

                _logger.LogInformation(
                    "Invalidated {Count} cache entries of type {DocumentType}",
                    invalidatedCount, documentType);
                
                return invalidatedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to invalidate cache entries of type {DocumentType}",
                    documentType);
                return invalidatedCount;
            }
        }

        /// <inheritdoc />
        public async ValueTask<int> ClearAllAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Count entries before clearing
                int count = 0;
                await foreach (var _ in _storage.GetAllHashesAsync())
                {
                    count++;
                }

                // Clear storage
                await _storage.ClearAsync();
                
                // Reset statistics
                await _statisticsLock.WaitAsync(cancellationToken);
                try
                {
                    _hitCount = 0;
                    _missCount = 0;
                    _expiredEntriesRemoved = 0;
                    _lastCleanupTime = DateTime.UtcNow;
                }
                finally
                {
                    _statisticsLock.Release();
                }

                _logger.LogInformation("Cleared all {Count} cached OCR results", count);
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear all cached OCR results");
                throw;
            }
        }

        /// <inheritdoc />
        public async ValueTask<int> CleanupExpiredAsync(
            CancellationToken cancellationToken = default)
        {
            int removedCount = 0;
            
            try
            {
                // Get all cache keys
                var keys = new List<string>();
                await foreach (var key in _storage.GetAllHashesAsync())
                {
                    keys.Add(key);
                }

                // Check each cached item for expiration
                foreach (var key in keys)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var bytes = await _storage.GetAsync(key);
                    if (bytes != null)
                    {
                        var json = Encoding.UTF8.GetString(bytes);
                        var cachedResult = JsonSerializer.Deserialize<CachedOCRResult>(json);
                        
                        if (cachedResult?.IsExpired == true)
                        {
                            if (await _storage.DeleteAsync(key))
                            {
                                removedCount++;
                                await IncrementExpiredCountAsync();
                            }
                        }
                    }
                }

                await _statisticsLock.WaitAsync(cancellationToken);
                try
                {
                    _lastCleanupTime = DateTime.UtcNow;
                }
                finally
                {
                    _statisticsLock.Release();
                }

                _logger.LogInformation(
                    "Cleanup removed {Count} expired cache entries",
                    removedCount);
                
                return removedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup expired cache entries");
                return removedCount;
            }
        }

        /// <inheritdoc />
        public async ValueTask<CacheStatistics> GetStatisticsAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Count entries and calculate total size
                int entryCount = 0;
                long totalSize = 0;
                
                await foreach (var key in _storage.GetAllHashesAsync())
                {
                    entryCount++;
                    totalSize += await _storage.GetSizeAsync(key);
                }

                await _statisticsLock.WaitAsync(cancellationToken);
                try
                {
                    return new CacheStatistics
                    {
                        HitCount = _hitCount,
                        MissCount = _missCount,
                        EntryCount = entryCount,
                        TotalSizeBytes = totalSize,
                        ExpiredEntriesRemoved = _expiredEntriesRemoved,
                        LastCleanupTime = _lastCleanupTime
                    };
                }
                finally
                {
                    _statisticsLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get cache statistics");
                return new CacheStatistics();
            }
        }

        /// <summary>
        /// Generates a cache key from document content using SHA-256.
        /// </summary>
        private string GenerateCacheKey(byte[] documentContent)
        {
            var hashBytes = _hashAlgorithm.ComputeHash(documentContent);
            return Convert.ToBase64String(hashBytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        /// <summary>
        /// Extracts document type from OCR result.
        /// </summary>
        private string? ExtractDocumentType(OCRStatusResponse result)
        {
            if (result.ResultData?.ExtractedFields != null)
            {
                var docTypeField = result.ResultData.ExtractedFields
                    .FirstOrDefault(f => f.FieldName?.Equals("DocumentType", StringComparison.OrdinalIgnoreCase) == true);
                return docTypeField?.Value;
            }
            return null;
        }

        /// <summary>
        /// Thread-safe increment of hit count.
        /// </summary>
        private async Task IncrementHitCountAsync()
        {
            await _statisticsLock.WaitAsync();
            try
            {
                _hitCount++;
            }
            finally
            {
                _statisticsLock.Release();
            }
        }

        /// <summary>
        /// Thread-safe increment of miss count.
        /// </summary>
        private async Task IncrementMissCountAsync()
        {
            await _statisticsLock.WaitAsync();
            try
            {
                _missCount++;
            }
            finally
            {
                _statisticsLock.Release();
            }
        }

        /// <summary>
        /// Thread-safe increment of expired entries count.
        /// </summary>
        private async Task IncrementExpiredCountAsync()
        {
            await _statisticsLock.WaitAsync();
            try
            {
                _expiredEntriesRemoved++;
            }
            finally
            {
                _statisticsLock.Release();
            }
        }
    }
}