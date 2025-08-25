namespace NoLock.Social.Core.Cryptography.Interfaces
{
    /// <summary>
    /// Service for Web Crypto API interop
    /// </summary>
    public interface IWebCryptoService
    {
        /// <summary>
        /// Check if Web Crypto API is available
        /// </summary>
        Task<bool> IsAvailableAsync();
        
        /// <summary>
        /// Generate random bytes
        /// </summary>
        Task<byte[]> GetRandomBytesAsync(int length);
        
        /// <summary>
        /// Compute SHA-256 hash
        /// </summary>
        Task<byte[]> Sha256Async(byte[] data);
        
        /// <summary>
        /// Compute SHA-512 hash
        /// </summary>
        Task<byte[]> Sha512Async(byte[] data);
        
        /// <summary>
        /// Derive key using PBKDF2
        /// </summary>
        Task<byte[]> Pbkdf2Async(byte[] password, byte[] salt, int iterations, int keyLength, string hash = "SHA-256");
        
        /// <summary>
        /// Generate ECDSA key pair
        /// </summary>
        Task<ECDSAKeyPair> GenerateECDSAKeyPairAsync(string curve = "P-256");
        
        /// <summary>
        /// Sign data with ECDSA
        /// </summary>
        Task<byte[]> SignECDSAAsync(byte[] privateKey, byte[] data, string curve = "P-256", string hash = "SHA-256");
        
        /// <summary>
        /// Verify ECDSA signature
        /// </summary>
        Task<bool> VerifyECDSAAsync(byte[] publicKey, byte[] signature, byte[] data, string curve = "P-256", string hash = "SHA-256");
        
        /// <summary>
        /// Encrypt data with AES-GCM
        /// </summary>
        Task<byte[]> EncryptAESGCMAsync(byte[] key, byte[] data, byte[] iv);
        
        /// <summary>
        /// Decrypt data with AES-GCM
        /// </summary>
        Task<byte[]> DecryptAESGCMAsync(byte[] key, byte[] encryptedData, byte[] iv);
    }
    
    /// <summary>
    /// ECDSA key pair
    /// </summary>
    public class ECDSAKeyPair
    {
        public byte[] PublicKey { get; set; } = Array.Empty<byte>();
        public byte[] PrivateKey { get; set; } = Array.Empty<byte>();
    }
    
    /// <summary>
    /// Ed25519 key pair (simulated using ECDSA P-256)
    /// </summary>
    public class Ed25519KeyPair
    {
        public byte[] PublicKey { get; set; } = Array.Empty<byte>();
        public byte[] PrivateKey { get; set; } = Array.Empty<byte>();
    }
}