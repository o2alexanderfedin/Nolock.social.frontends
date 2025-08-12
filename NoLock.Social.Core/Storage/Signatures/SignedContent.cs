using System;

namespace NoLock.Social.Core.Storage.Signatures
{
    public sealed class SignedContent
    {
        public string ContentHash { get; }
        public byte[] Signature { get; }
        public string Algorithm { get; }
        public string? SignerPublicKeyId { get; }
        public DateTime SignedAt { get; }

        public SignedContent(
            string contentHash, 
            byte[] signature, 
            string algorithm, 
            string? signerPublicKeyId = null,
            DateTime? signedAt = null)
        {
            if (string.IsNullOrWhiteSpace(contentHash))
                throw new ArgumentException("Content hash cannot be null or empty", nameof(contentHash));
            
            if (signature == null || signature.Length == 0)
                throw new ArgumentException("Signature cannot be null or empty", nameof(signature));
            
            if (string.IsNullOrWhiteSpace(algorithm))
                throw new ArgumentException("Algorithm cannot be null or empty", nameof(algorithm));

            ContentHash = contentHash;
            Signature = (byte[])signature.Clone(); // Defensive copy
            Algorithm = algorithm;
            SignerPublicKeyId = signerPublicKeyId;
            SignedAt = signedAt ?? DateTime.UtcNow;
        }
    }
}