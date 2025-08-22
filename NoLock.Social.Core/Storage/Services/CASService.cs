using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NoLock.Social.Core.Storage.Interfaces;

namespace NoLock.Social.Core.Storage.Services
{
    /// <summary>
    /// In-memory Content-Addressed Storage service implementation
    /// </summary>
    public class CASService : ICASService
    {
        private readonly ILogger<CASService> _logger;
        private readonly ConcurrentDictionary<string, StoredContent> _storage;

        public CASService(ILogger<CASService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _storage = new ConcurrentDictionary<string, StoredContent>();
        }

        public Task<string> StoreAsync<T>(T content, CancellationToken cancellationToken = default)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            cancellationToken.ThrowIfCancellationRequested();

            var serializedContent = JsonSerializer.Serialize(content);
            var hash = ComputeHash(serializedContent);
            
            var storedContent = new StoredContent
            {
                Content = serializedContent,
                ContentType = typeof(T).Name,
                StoredAt = DateTime.UtcNow
            };

            _storage.AddOrUpdate(hash, storedContent, (key, existing) => storedContent);
            
            _logger.LogInformation("Stored content of type {Type} with hash {Hash}", typeof(T).Name, hash);
            
            return Task.FromResult(hash);
        }

        public Task<T> RetrieveAsync<T>(string hash, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(hash))
                throw new ArgumentException("Hash cannot be empty", nameof(hash));

            cancellationToken.ThrowIfCancellationRequested();

            if (_storage.TryGetValue(hash, out var storedContent))
            {
                _logger.LogDebug("Retrieved content with hash {Hash}", hash);
                var deserialized = JsonSerializer.Deserialize<T>(storedContent.Content);
                return Task.FromResult(deserialized);
            }

            _logger.LogDebug("Content with hash {Hash} not found", hash);
            return Task.FromResult<T>(default(T));
        }

        public Task<bool> ExistsAsync(string hash, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(hash))
                throw new ArgumentException("Hash cannot be empty", nameof(hash));

            cancellationToken.ThrowIfCancellationRequested();

            var exists = _storage.ContainsKey(hash);
            _logger.LogDebug("Content with hash {Hash} exists: {Exists}", hash, exists);
            
            return Task.FromResult(exists);
        }


        private string ComputeHash(string content)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(content);
            var hashBytes = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hashBytes);
        }

        private class StoredContent
        {
            public string Content { get; set; }
            public string ContentType { get; set; }
            public DateTime StoredAt { get; set; }
        }
    }
}