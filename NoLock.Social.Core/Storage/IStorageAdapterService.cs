using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NoLock.Social.Core.Cryptography.Interfaces;

namespace NoLock.Social.Core.Storage
{
    /// <summary>
    /// Service for managing signed content in storage
    /// </summary>
    public interface IStorageAdapterService
    {
        /// <summary>
        /// Stores signed content in the storage system
        /// </summary>
        /// <param name="signedContent">The signed content to store</param>
        /// <param name="metadata">Optional metadata for the content</param>
        /// <returns>Storage identifier for the stored content</returns>
        Task<string> StoreSignedContentAsync(SignedTarget signedContent, StorageMetadata? metadata = null);

        /// <summary>
        /// Retrieves signed content from storage
        /// </summary>
        /// <param name="storageId">The storage identifier</param>
        /// <returns>The signed content with its metadata</returns>
        Task<(SignedTarget content, StorageMetadata metadata)> RetrieveSignedContentAsync(string storageId);

        /// <summary>
        /// Deletes content from storage
        /// </summary>
        /// <param name="storageId">The storage identifier</param>
        /// <returns>True if deletion was successful</returns>
        Task<bool> DeleteContentAsync(string storageId);

        /// <summary>
        /// Lists all content in storage with optional filtering
        /// </summary>
        /// <param name="filter">Optional filter criteria</param>
        /// <returns>Collection of storage entries</returns>
        Task<IEnumerable<StorageMetadata>> ListAllContentAsync(string? filter = null);
    }
}