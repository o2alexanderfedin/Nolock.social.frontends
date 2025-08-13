using System;
using System.Threading.Tasks;

namespace NoLock.Social.Core.Cryptography.Interfaces
{
    /// <summary>
    /// Service for JavaScript interop with Web Crypto API and libsodium.js
    /// </summary>
    public interface ICryptoJSInteropService
    {
        /// <summary>
        /// Initialize libsodium.js library
        /// </summary>
        Task<bool> InitializeLibsodiumAsync();

        /// <summary>
        /// Check if libsodium is ready
        /// </summary>
        Task<bool> IsLibsodiumReadyAsync();

        /// <summary>
        /// Compute SHA-256 hash using Web Crypto API
        /// </summary>
        Task<byte[]> ComputeSha256Async(byte[] data);

        /// <summary>
        /// Compute SHA-256 hash of a string using Web Crypto API
        /// </summary>
        Task<byte[]> ComputeSha256Async(string data);

        /// <summary>
        /// Generate secure random bytes
        /// </summary>
        Task<byte[]> GetRandomBytesAsync(int length);

        /// <summary>
        /// Derive key using Argon2id (via libsodium.js)
        /// </summary>
        Task<byte[]> DeriveKeyArgon2idAsync(string passphrase, string username);

        /// <summary>
        /// Generate Ed25519 key pair from seed (via libsodium.js)
        /// </summary>
        Task<Ed25519KeyPair> GenerateEd25519KeyPairFromSeedAsync(byte[] seed);

        /// <summary>
        /// Sign data with Ed25519 private key (via libsodium.js)
        /// </summary>
        Task<byte[]> SignEd25519Async(byte[] data, byte[] privateKey);

        /// <summary>
        /// Verify Ed25519 signature (via libsodium.js)
        /// </summary>
        Task<bool> VerifyEd25519Async(byte[] data, byte[] signature, byte[] publicKey);

        /// <summary>
        /// Convert bytes to base64 string
        /// </summary>
        Task<string> BytesToBase64Async(byte[] bytes);

        /// <summary>
        /// Convert base64 string to bytes
        /// </summary>
        Task<byte[]> Base64ToBytesAsync(string base64);

        /// <summary>
        /// Clear sensitive data from memory (best effort)
        /// </summary>
        Task ClearMemoryAsync(byte[] data);
    }

    /// <summary>
    /// Ed25519 key pair
    /// </summary>
    public class Ed25519KeyPair
    {
        public byte[] PublicKey { get; set; } = Array.Empty<byte>();
        public byte[] PrivateKey { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Clear private key from memory
        /// </summary>
        public void Clear()
        {
            if (PrivateKey != null && PrivateKey.Length > 0)
            {
                Array.Clear(PrivateKey, 0, PrivateKey.Length);
            }
        }
    }
}