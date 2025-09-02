using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NoLock.Social.Core.Storage
{
    /// <summary>
    /// Logging decorator for IContentAddressableStorage<T>.
    /// Logs all method calls with parameters and execution results.
    /// </summary>
    /// <typeparam name="T">The type of content to store</typeparam>
    public class LoggingContentAddressableStorageDecorator<T> : ContentAddressableStorageDecorator<T>
    {
        private readonly ILogger<LoggingContentAddressableStorageDecorator<T>> _logger;

        /// <summary>
        /// Initializes a new instance of the LoggingContentAddressableStorageDecorator class.
        /// </summary>
        /// <param name="underlying">The underlying storage instance to wrap</param>
        /// <param name="logger">Logger instance for recording operations</param>
        /// <exception cref="ArgumentNullException">Thrown when underlying or logger is null</exception>
        public LoggingContentAddressableStorageDecorator(
            IContentAddressableStorage<T> underlying, 
            ILogger<LoggingContentAddressableStorageDecorator<T>> logger) 
            : base(underlying)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Stores content with logging of the operation.
        /// </summary>
        /// <param name="content">The content to store</param>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>The hash of the stored content</returns>
        public override async ValueTask<string> StoreAsync(T content, CancellationToken cancellation = default)
        {
            _logger.LogInformation("Storing content of type {ContentType}", typeof(T).Name);
            
            var result = await base.StoreAsync(content, cancellation);
            
            _logger.LogInformation("Content stored successfully with hash: {Hash}", result);
            return result;
        }

        /// <summary>
        /// Retrieves content with logging of the operation.
        /// </summary>
        /// <param name="hash">The hash of the content to retrieve</param>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>The deserialized content or null if not found</returns>
        public override async ValueTask<T?> GetAsync(string hash, CancellationToken cancellation = default)
        {
            _logger.LogInformation("Retrieving content with hash: {Hash}", hash);
            
            var result = await base.GetAsync(hash, cancellation);
            
            if (result != null)
            {
                _logger.LogInformation("Content retrieved successfully for hash: {Hash}", hash);
            }
            else
            {
                _logger.LogWarning("Content not found for hash: {Hash}", hash);
            }
            
            return result;
        }

        /// <summary>
        /// Deletes content with logging of the operation.
        /// </summary>
        /// <param name="hash">The hash of the content to delete</param>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>True if content was deleted, false if not found</returns>
        public override async ValueTask<bool> DeleteAsync(string hash, CancellationToken cancellation = default)
        {
            _logger.LogInformation("Attempting to delete content with hash: {Hash}", hash);
            
            var result = await base.DeleteAsync(hash, cancellation);
            
            if (result)
            {
                _logger.LogInformation("Content deleted successfully for hash: {Hash}", hash);
            }
            else
            {
                _logger.LogWarning("Content not found for deletion, hash: {Hash}", hash);
            }
            
            return result;
        }
    }
}