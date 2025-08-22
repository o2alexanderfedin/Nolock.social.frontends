using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NoLock.Social.Core.Cryptography.Interfaces;
using NoLock.Social.Core.Hashing;
using NoLock.Social.Core.Storage.Interfaces;

namespace NoLock.Social.Core.Storage
{
    /// <summary>
    /// Implementation of storage adapter for signed content
    /// </summary>
    public class StorageAdapterService : IStorageAdapterService
    {
        private readonly IContentAddressableStorage<byte[]> _cas;
        private readonly IHashAlgorithm _hashAlgorithm;
        private readonly IVerificationService _verificationService;

        public StorageAdapterService(
            IContentAddressableStorage<byte[]> cas,
            IHashAlgorithm hashAlgorithm,
            IVerificationService verificationService)
        {
            _cas = cas ?? throw new ArgumentNullException(nameof(cas));
            _hashAlgorithm = hashAlgorithm ?? throw new ArgumentNullException(nameof(hashAlgorithm));
            _verificationService = verificationService ?? throw new ArgumentNullException(nameof(verificationService));
        }

        public async Task<StorageMetadata> StoreSignedContentAsync(SignedContent signedContent)
        {
            ArgumentNullException.ThrowIfNull(signedContent);

            // Serialize the signed content
            var serializedContent = SerializeSignedContent(signedContent);

            // Store in CAS - it will compute the content address
            var contentAddress = await _cas.StoreAsync(serializedContent);

            // Create and return metadata
            return new StorageMetadata
            {
                ContentAddress = contentAddress,
                Size = serializedContent.Length,
                Timestamp = DateTime.UtcNow,
                Algorithm = signedContent.Algorithm,
                Version = signedContent.Version,
                PublicKeyBase64 = Convert.ToBase64String(signedContent.PublicKey)
            };
        }

        public async Task<SignedContent?> RetrieveSignedContentAsync(string contentAddress)
        {
            if (string.IsNullOrWhiteSpace(contentAddress))
                throw new ArgumentException("Content address cannot be null or empty", nameof(contentAddress));

            // Retrieve from CAS
            var serializedContent = await _cas.GetAsync(contentAddress);
            if (serializedContent == null)
                return null;

            // Deserialize
            var signedContent = DeserializeSignedContent(serializedContent);

            // Verify signature
            var isValid = await _verificationService.VerifySignedContentAsync(signedContent);
            if (!isValid)
            {
                throw new StorageVerificationException(
                    "Signature verification failed for retrieved content",
                    contentAddress);
            }

            return signedContent;
        }

        public async Task<StorageMetadata?> GetStorageMetadataAsync(string contentAddress)
        {
            if (string.IsNullOrWhiteSpace(contentAddress))
                throw new ArgumentException("Content address cannot be null or empty", nameof(contentAddress));

            // Check if content exists
            if (!await _cas.ExistsAsync(contentAddress))
                return null;

            // Get size
            var size = await _cas.GetSizeAsync(contentAddress);

            // Retrieve content to extract metadata (we need algorithm, version, public key)
            var serializedContent = await _cas.GetAsync(contentAddress);
            if (serializedContent == null)
                return null;

            var signedContent = DeserializeSignedContent(serializedContent);

            return new StorageMetadata
            {
                ContentAddress = contentAddress,
                Size = size,
                Timestamp = signedContent.Timestamp,
                Algorithm = signedContent.Algorithm,
                Version = signedContent.Version,
                PublicKeyBase64 = Convert.ToBase64String(signedContent.PublicKey)
            };
        }

        public async Task<bool> DeleteContentAsync(string contentAddress)
        {
            if (string.IsNullOrWhiteSpace(contentAddress))
                throw new ArgumentException("Content address cannot be null or empty", nameof(contentAddress));

            return await _cas.DeleteAsync(contentAddress);
        }

        public async IAsyncEnumerable<StorageMetadata> ListAllContentAsync()
        {
            var metadataList = new List<StorageMetadata>();

            // Collect all metadata first for sorting
            await foreach (var hash in _cas.GetAllHashesAsync())
            {
                var metadata = await GetStorageMetadataAsync(hash);
                if (metadata != null)
                {
                    metadataList.Add(metadata);
                }
            }

            // Sort by timestamp descending (newest first)
            var sortedList = metadataList.OrderByDescending(m => m.Timestamp);

            // Yield sorted results
            foreach (var metadata in sortedList)
            {
                yield return metadata;
            }
        }

        private byte[] SerializeSignedContent(SignedContent content)
        {
            // Create a serializable representation
            var serializableContent = new
            {
                content.Content,
                ContentHash = Convert.ToBase64String(content.ContentHash),
                Signature = Convert.ToBase64String(content.Signature),
                PublicKey = Convert.ToBase64String(content.PublicKey),
                content.Algorithm,
                content.Version,
                content.Timestamp
            };

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            var json = JsonSerializer.Serialize(serializableContent, options);
            return Encoding.UTF8.GetBytes(json);
        }

        private SignedContent DeserializeSignedContent(byte[] data)
        {
            var json = Encoding.UTF8.GetString(data);
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            // Deserialize to anonymous type first
            var jsonDoc = JsonDocument.Parse(json);
            var root = jsonDoc.RootElement;

            var content = new SignedContent
            {
                Content = root.GetProperty("content").GetString() ?? string.Empty,
                ContentHash = Convert.FromBase64String(root.GetProperty("contentHash").GetString() ?? string.Empty),
                Signature = Convert.FromBase64String(root.GetProperty("signature").GetString() ?? string.Empty),
                PublicKey = Convert.FromBase64String(root.GetProperty("publicKey").GetString() ?? string.Empty),
                Algorithm = root.GetProperty("algorithm").GetString() ?? "Ed25519",
                Version = root.GetProperty("version").GetString() ?? "1.0",
                Timestamp = root.GetProperty("timestamp").GetDateTime()
            };

            return content;
        }
    }
}