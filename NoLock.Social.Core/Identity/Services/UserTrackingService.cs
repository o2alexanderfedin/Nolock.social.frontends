using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NoLock.Social.Core.Common.Extensions;
using NoLock.Social.Core.Common.Results;
using NoLock.Social.Core.Identity.Interfaces;
using NoLock.Social.Core.Identity.Models;

namespace NoLock.Social.Core.Identity.Services
{
    /// <summary>
    /// Service implementation for tracking user identity existence and history.
    /// Tracks users in memory only without persistent storage.
    /// </summary>
    public class UserTrackingService : IUserTrackingService
    {
        private readonly ILogger<UserTrackingService> _logger;
        
        // Cache user tracking info for the session duration to avoid repeated queries
        private readonly Dictionary<string, UserTrackingInfo> _cache = new();
        private readonly object _cacheLock = new();

        public UserTrackingService(
            ILogger<UserTrackingService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<UserTrackingInfo> CheckUserExistsAsync(string publicKeyBase64)
        {
            if (string.IsNullOrWhiteSpace(publicKeyBase64))
            {
                throw new ArgumentException("Public key cannot be null or empty", nameof(publicKeyBase64));
            }

            _logger.LogDebug("Checking if user exists for public key: {PublicKey}", 
                publicKeyBase64.Substring(0, Math.Min(10, publicKeyBase64.Length)) + "...");

            // Check cache first for performance
            lock (_cacheLock)
            {
                if (_cache.TryGetValue(publicKeyBase64, out var cached))
                {
                    _logger.LogDebug("Returning cached user tracking info");
                    return cached;
                }
            }

            // Query storage for any content signed by this public key
            var contentCount = 0;
            DateTime? firstSeen = null;
            DateTime? lastSeen = null;

            var queryResult = await _logger.ExecuteWithLogging(
                async () => await QueryUserContentAsync(publicKeyBase64),
                "querying storage for user content");

            // On failure, continue with what we have (graceful degradation)
            if (queryResult.IsSuccess)
            {
                var (count, first, last) = queryResult.Value;
                contentCount = count;
                firstSeen = first;
                lastSeen = last;
            }
            // If query failed, contentCount remains 0, timestamps remain null

            var info = new UserTrackingInfo
            {
                Exists = contentCount > 0,
                FirstSeen = firstSeen,
                LastSeen = lastSeen,
                ContentCount = contentCount,
                PublicKeyBase64 = publicKeyBase64
            };

            // Cache the result for this session
            lock (_cacheLock)
            {
                _cache[publicKeyBase64] = info;
            }

            _logger.LogInformation(
                "User tracking complete: Exists={Exists}, ContentCount={Count}, FirstSeen={FirstSeen}",
                info.Exists, info.ContentCount, info.FirstSeen);

            return info;
        }

        /// <inheritdoc />
        public Task MarkUserAsActiveAsync(string publicKeyBase64)
        {
            if (string.IsNullOrWhiteSpace(publicKeyBase64))
            {
                throw new ArgumentException("Public key cannot be null or empty", nameof(publicKeyBase64));
            }

            _logger.LogDebug("Marking user as active: {PublicKey}", 
                publicKeyBase64.Substring(0, Math.Min(10, publicKeyBase64.Length)) + "...");

            // Clear cache entry to force refresh on next check
            lock (_cacheLock)
            {
                if (_cache.ContainsKey(publicKeyBase64))
                {
                    _cache.Remove(publicKeyBase64);
                    _logger.LogDebug("Cleared cache entry for user");
                }
            }

            // The actual marking happens automatically when content is saved
            // This method just ensures the cache is cleared
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task<UserActivitySummary> GetUserActivityAsync(string publicKeyBase64)
        {
            if (string.IsNullOrWhiteSpace(publicKeyBase64))
            {
                throw new ArgumentException("Public key cannot be null or empty", nameof(publicKeyBase64));
            }

            _logger.LogDebug("Getting user activity summary for: {PublicKey}", 
                publicKeyBase64.Substring(0, Math.Min(10, publicKeyBase64.Length)) + "...");

            var summary = new UserActivitySummary
            {
                TotalContent = 0,
                LastActivity = null,
                TotalStorageBytes = 0,
                RecentContentAddresses = new List<string>()
            };

            var contentList = new List<(string address, DateTime timestamp, long size)>();

            // No persistent storage - return empty summary
            var processResult = await _logger.ExecuteWithLogging(
                async () =>
                {
                    // Without storage adapter, we can't query historical content
                    // Return an empty summary
                    await Task.CompletedTask;
                    return summary;
                },
                "getting user activity summary");

            // Return partial results on failure (graceful degradation)
            // The summary object has been modified even if an exception occurred
            // during iteration, so we return whatever data was collected

            _logger.LogInformation(
                "User activity summary: TotalContent={Content}, TotalStorage={Storage} bytes, LastActivity={LastActivity}",
                summary.TotalContent, summary.TotalStorageBytes, summary.LastActivity);

            return summary;
        }

        /// <summary>
        /// Queries storage for content signed by the specified public key
        /// </summary>
        /// <param name="publicKeyBase64">The public key to search for</param>
        /// <returns>A tuple containing content count, first seen timestamp, and last seen timestamp</returns>
        private async Task<(int contentCount, DateTime? firstSeen, DateTime? lastSeen)> QueryUserContentAsync(string publicKeyBase64)
        {
            // Without storage adapter, we can't query content
            // Check in-memory cache only
            lock (_cacheLock)
            {
                if (_cache.TryGetValue(publicKeyBase64, out var cached))
                {
                    return (cached.ContentCount, cached.FirstSeen, cached.LastSeen);
                }
            }
            
            await Task.CompletedTask;
            return (0, null, null);
        }

        /// <summary>
        /// Clear the cache for all users. Useful for testing or when storage changes externally.
        /// </summary>
        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _cache.Clear();
                _logger.LogDebug("User tracking cache cleared");
            }
        }
    }
}