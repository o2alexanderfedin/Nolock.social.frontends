using System;
using System.Threading.Tasks;

namespace NoLock.Social.Core.Cryptography.Interfaces
{
    /// <summary>
    /// Service for signing content with Ed25519 signatures
    /// </summary>
    public interface ISigningService
    {
        /// <summary>
        /// Signs content using Ed25519 with SHA-256 hashing
        /// </summary>
        /// <param name="targetHash">The content to sign</param>
        /// <param name="privateKey">Ed25519 private key (32 bytes)</param>
        /// <param name="publicKey">Ed25519 public key (32 bytes)</param>
        /// <returns>Signed content with signature and metadata</returns>
        Task<SignedTarget> SignAsync(string targetHash, byte[] privateKey, byte[] publicKey);

        /// <summary>
        /// Signs content using Ed25519 key pair
        /// </summary>
        /// <param name="targetHash">The content to sign</param>
        /// <param name="keyPair">Ed25519 key pair</param>
        /// <returns>Signed content with signature and metadata</returns>
        Task<SignedTarget> SignAsync(string targetHash, Ed25519KeyPair keyPair);
    }

    /// <summary>
    /// Represents signed content with cryptographic signature
    /// </summary>
    public class SignedTarget
    {
        /// <summary>
        /// SHA-256 hash of the content
        /// </summary>
        public byte[] TargetHash { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Ed25519 signature (64 bytes)
        /// </summary>
        public byte[] Signature { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Public key used for verification (32 bytes)
        /// </summary>
        public byte[] PublicKey { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Signature algorithm used
        /// </summary>
        public string Algorithm { get; set; } = "Ed25519";

        /// <summary>
        /// Version of the signing format
        /// </summary>
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// Timestamp when the content was signed
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Convert to base64 representation for transmission/storage
        /// </summary>
        public SignedTargetBase64 ToBase64()
        {
            return new SignedTargetBase64
            {
                TargetHashBase64 = Convert.ToBase64String(TargetHash),
                SignatureBase64 = Convert.ToBase64String(Signature),
                PublicKeyBase64 = Convert.ToBase64String(PublicKey),
                Algorithm = Algorithm,
                Version = Version,
                Timestamp = Timestamp
            };
        }
    }

    /// <summary>
    /// Base64-encoded representation of signed content for transmission
    /// </summary>
    public class SignedTargetBase64
    {
        /// <summary>
        /// Base64-encoded SHA-256 hash of the target
        /// </summary>
        public string TargetHashBase64 { get; set; } = string.Empty;

        /// <summary>
        /// Base64-encoded Ed25519 signature
        /// </summary>
        public string SignatureBase64 { get; set; } = string.Empty;

        /// <summary>
        /// Base64-encoded public key
        /// </summary>
        public string PublicKeyBase64 { get; set; } = string.Empty;

        /// <summary>
        /// Signature algorithm used
        /// </summary>
        public string Algorithm { get; set; } = "Ed25519";

        /// <summary>
        /// Version of the signing format
        /// </summary>
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// Timestamp when the content was signed
        /// </summary>
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Exception thrown when cryptographic operations fail
    /// </summary>
    public class CryptoException : Exception
    {
        public CryptoException(string message) : base(message) { }
        public CryptoException(string message, Exception innerException) : base(message, innerException) { }
    }
}