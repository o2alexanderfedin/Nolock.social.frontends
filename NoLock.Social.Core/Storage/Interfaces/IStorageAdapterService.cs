using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NoLock.Social.Core.Cryptography.Interfaces;

namespace NoLock.Social.Core.Storage.Interfaces
{
    /// <summary>
    /// Service for adapting signed content to content-addressable storage
    /// Handles serialization, content addressing, and verification
    /// </summary>
    public interface IStorageAdapterService
    {
        /// <summary>
        /// Store signed content in CAS with content addressing
        /// </summary>
        /// <param name="signedContent">The signed content to store</param>
        /// <returns>Storage metadata including content address</returns>
        Task<StorageMetadata> StoreSignedContentAsync(SignedContent signedContent);

        /// <summary>
        /// Retrieve signed content from CAS and verify signature
        /// </summary>
        /// <param name="contentAddress">The content address (hash)</param>
        /// <returns>Signed content if found and verified, null otherwise</returns>
        Task<SignedContent?> RetrieveSignedContentAsync(string contentAddress);

        /// <summary>
        /// Get storage metadata without retrieving full content
        /// </summary>
        /// <param name="contentAddress">The content address (hash)</param>
        /// <returns>Storage metadata if exists, null otherwise</returns>
        Task<StorageMetadata?> GetStorageMetadataAsync(string contentAddress);

        /// <summary>
        /// Delete content from storage
        /// </summary>
        /// <param name="contentAddress">The content address (hash)</param>
        /// <returns>True if deleted, false if not found</returns>
        Task<bool> DeleteContentAsync(string contentAddress);

        /// <summary>
        /// List all stored content metadata
        /// </summary>
        /// <returns>Async enumerable of storage metadata</returns>
        IAsyncEnumerable<StorageMetadata> ListAllContentAsync();
    }

    /// <summary>
    /// Metadata for stored signed content
    /// </summary>
    public class StorageMetadata
    {
        /// <summary>
        /// Content address (SHA-256 hash)
        /// </summary>
        public string ContentAddress { get; set; } = string.Empty;

        /// <summary>
        /// Size of stored content in bytes
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Timestamp when content was stored
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Signature algorithm used
        /// </summary>
        public string Algorithm { get; set; } = "Ed25519";

        /// <summary>
        /// Version of the signing format
        /// </summary>
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// Base64-encoded public key for verification
        /// </summary>
        public string PublicKeyBase64 { get; set; } = string.Empty;
    }

    /// <summary>
    /// Exception thrown when storage verification fails
    /// </summary>
    public class StorageVerificationException : Exception
    {
        public string ContentAddress { get; }

        public StorageVerificationException(string message, string contentAddress) 
            : base(message)
        {
            ContentAddress = contentAddress;
        }

        public StorageVerificationException(string message, string contentAddress, Exception innerException) 
            : base(message, innerException)
        {
            ContentAddress = contentAddress;
        }
    }
}