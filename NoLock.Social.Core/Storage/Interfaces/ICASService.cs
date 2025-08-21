using System.Threading;
using System.Threading.Tasks;

namespace NoLock.Social.Core.Storage.Interfaces
{
    /// <summary>
    /// Content-Addressed Storage service for storing and retrieving data by content hash
    /// </summary>
    public interface ICASService
    {
        /// <summary>
        /// Stores content and returns its SHA256 hash
        /// </summary>
        /// <param name="content">The content to store (base64 encoded string for images or JSON for results)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>SHA256 hash of the stored content</returns>
        Task<string> StoreAsync<T>(T content, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves content by its hash
        /// </summary>
        /// <param name="hash">SHA256 hash of the content</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The stored content or null if not found</returns>
        Task<T> RetrieveAsync<T>(string hash, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if content exists by its hash
        /// </summary>
        /// <param name="hash">SHA256 hash of the content</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if content exists, false otherwise</returns>
        Task<bool> ExistsAsync(string hash, CancellationToken cancellationToken = default);
    }
}