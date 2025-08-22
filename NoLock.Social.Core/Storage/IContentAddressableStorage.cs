using System.Collections.Generic;
using System.Threading.Tasks;

namespace NoLock.Social.Core.Storage
{
    /// <summary>
    /// Generic interface for type-safe content-addressable storage operations.
    /// Provides strongly-typed storage and retrieval of objects with automatic serialization.
    /// </summary>
    /// <typeparam name="T">The type of content to store</typeparam>
    public interface IContentAddressableStorage<T>
    {
        /// <summary>
        /// Stores content of type T and returns its content-addressed hash.
        /// </summary>
        /// <param name="content">The content to store</param>
        /// <returns>The hash of the stored content</returns>
        ValueTask<string> StoreAsync(T content);

        /// <summary>
        /// Retrieves content by its hash, returning the deserialized object or null if not found.
        /// </summary>
        /// <param name="hash">The hash of the content to retrieve</param>
        /// <returns>The deserialized content or null if not found</returns>
        ValueTask<T?> GetAsync(string hash);

        /// <summary>
        /// Checks if content exists for the given hash.
        /// </summary>
        /// <param name="hash">The hash to check</param>
        /// <returns>True if content exists, false otherwise</returns>
        ValueTask<bool> ExistsAsync(string hash);

        /// <summary>
        /// Deletes content by its hash.
        /// </summary>
        /// <param name="hash">The hash of the content to delete</param>
        /// <returns>True if content was deleted, false if not found</returns>
        ValueTask<bool> DeleteAsync(string hash);

        /// <summary>
        /// Gets all stored content hashes.
        /// </summary>
        /// <returns>An async enumerable of all stored hashes</returns>
        IAsyncEnumerable<string> GetAllHashesAsync();

        /// <summary>
        /// Gets the size of content by its hash.
        /// </summary>
        /// <param name="hash">The hash of the content</param>
        /// <returns>The size in bytes of the serialized content</returns>
        ValueTask<long> GetSizeAsync(string hash);

        /// <summary>
        /// Gets the total size of all stored content.
        /// </summary>
        /// <returns>The total size in bytes of all stored content</returns>
        ValueTask<long> GetTotalSizeAsync();

        /// <summary>
        /// Clears all stored content.
        /// </summary>
        ValueTask ClearAsync();
    }
}