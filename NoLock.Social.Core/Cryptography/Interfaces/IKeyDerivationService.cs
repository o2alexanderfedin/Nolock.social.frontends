using System;
using System.Threading.Tasks;

namespace NoLock.Social.Core.Cryptography.Interfaces
{
    /// <summary>
    /// Service for deriving cryptographic keys from passphrases using Argon2id
    /// </summary>
    public interface IKeyDerivationService
    {
        /// <summary>
        /// Event for reporting derivation progress
        /// </summary>
        event EventHandler<KeyDerivationProgressEventArgs>? DerivationProgress;

        /// <summary>
        /// Derives a master key from passphrase and username using Argon2id
        /// CRITICAL: Parameters are IMMUTABLE - changing them breaks determinism
        /// </summary>
        /// <param name="passphrase">User's passphrase (will be NFKC normalized)</param>
        /// <param name="username">User's username (will be lowercased for salt)</param>
        /// <returns>32-byte derived key suitable for Ed25519 seed</returns>
        Task<ISecureBuffer> DeriveMasterKeyAsync(string passphrase, string username);

        /// <summary>
        /// Generates Ed25519 key pair from derived master key
        /// </summary>
        /// <param name="masterKey">32-byte master key from Argon2id</param>
        /// <returns>Ed25519 key pair</returns>
        Task<Ed25519KeyPair> GenerateKeyPairAsync(ISecureBuffer masterKey);

        /// <summary>
        /// Complete flow: derive key and generate Ed25519 key pair
        /// </summary>
        /// <param name="passphrase">User's passphrase</param>
        /// <param name="username">User's username</param>
        /// <returns>Ed25519 key pair and secure buffer with private key</returns>
        Task<(Ed25519KeyPair keyPair, ISecureBuffer privateKeyBuffer)> DeriveIdentityAsync(string passphrase, string username);

        /// <summary>
        /// Gets the fixed Argon2id parameters (for display/verification)
        /// </summary>
        Argon2idParameters GetParameters();
    }

    /// <summary>
    /// Immutable Argon2id parameters
    /// </summary>
    public class Argon2idParameters
    {
        public int MemoryKiB { get; } = 65536; // 64MB
        public int Iterations { get; } = 3;
        public int Parallelism { get; } = 1; // WASM constraint
        public int HashLength { get; } = 32; // bytes
        public string Algorithm { get; } = "Argon2id";
        public string Version { get; } = "1.3";
    }

    /// <summary>
    /// Progress event args for key derivation
    /// </summary>
    public class KeyDerivationProgressEventArgs : EventArgs
    {
        public int PercentComplete { get; set; }
        public string Message { get; set; } = string.Empty;
        public TimeSpan ElapsedTime { get; set; }
    }
}