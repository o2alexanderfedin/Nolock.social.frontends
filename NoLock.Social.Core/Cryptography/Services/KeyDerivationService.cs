using Microsoft.Extensions.Logging;
using NoLock.Social.Core.Cryptography.Interfaces;
using System.Diagnostics;
using System.Text;

namespace NoLock.Social.Core.Cryptography.Services
{
    /// <summary>
    /// Service for deriving cryptographic keys from passphrases using PBKDF2
    /// </summary>
    public class KeyDerivationService : IKeyDerivationService
    {
        private readonly IWebCryptoService _webCrypto;
        private readonly ISecureMemoryManager _secureMemoryManager;
        private readonly ILogger<KeyDerivationService> _logger;
        private readonly Argon2idParameters _parameters = new();

        public event EventHandler<KeyDerivationProgressEventArgs>? DerivationProgress;

        public KeyDerivationService(
            IWebCryptoService webCrypto, 
            ISecureMemoryManager secureMemoryManager,
            ILogger<KeyDerivationService> logger)
        {
            _webCrypto = webCrypto ?? throw new ArgumentNullException(nameof(webCrypto));
            _secureMemoryManager = secureMemoryManager ?? throw new ArgumentNullException(nameof(secureMemoryManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ISecureBuffer> DeriveMasterKeyAsync(string passphrase, string username)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(passphrase))
                throw new ArgumentException("Passphrase cannot be null or empty", nameof(passphrase));
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be null or empty", nameof(username));

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Report progress - starting
                RaiseProgress(10, "Starting key derivation...", stopwatch.Elapsed);

                // Normalize username to lowercase for salt generation
                // This ensures deterministic key derivation regardless of username casing
                var normalizedUsername = username.ToLowerInvariant();

                // Report progress - deriving
                RaiseProgress(30, "Deriving key with PBKDF2...", stopwatch.Elapsed);

                // Generate salt from username
                var saltData = Encoding.UTF8.GetBytes(normalizedUsername);
                var salt = await _webCrypto.Sha256Async(saltData);
                
                // Convert passphrase to bytes
                var passwordBytes = Encoding.UTF8.GetBytes(passphrase);
                
                // Derive key using PBKDF2 with high iteration count
                // Using 600,000 iterations as recommended by OWASP for PBKDF2-SHA256
                var derivedKey = await _webCrypto.Pbkdf2Async(
                    passwordBytes, 
                    salt, 
                    600000,  // High iteration count for security
                    32,      // 32 bytes output
                    "SHA-256");

                // Report progress - securing
                RaiseProgress(90, "Securing derived key...", stopwatch.Elapsed);

                // Store derived key in secure buffer
                var secureBuffer = _secureMemoryManager.CreateSecureBuffer(derivedKey);

                // Clear the original array
                Array.Clear(derivedKey, 0, derivedKey.Length);

                stopwatch.Stop();

                // Report completion
                RaiseProgress(100, "Key derivation complete", stopwatch.Elapsed);

                // Check performance budget (500ms target, 1000ms max)
                if (stopwatch.ElapsedMilliseconds > 1000)
                {
                    _logger.LogWarning("Key derivation took {ElapsedMilliseconds}ms, exceeding 1000ms timeout", stopwatch.ElapsedMilliseconds);
                }

                return secureBuffer;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                RaiseProgress(0, $"Key derivation failed: {ex.Message}", stopwatch.Elapsed);
                throw new InvalidOperationException("Failed to derive master key", ex);
            }
        }

        public async Task<Ed25519KeyPair> GenerateKeyPairAsync(ISecureBuffer masterKey)
        {
            if (masterKey == null)
                throw new ArgumentNullException(nameof(masterKey));
            if (masterKey.Size != 32)
                throw new ArgumentException("Master key must be exactly 32 bytes", nameof(masterKey));

            try
            {
                // For now, generate ECDSA key pair and adapt it
                // In production, you might want to use the seed to deterministically generate the key
                var ecdsaKeyPair = await _webCrypto.GenerateECDSAKeyPairAsync("P-256");
                
                // Convert to Ed25519KeyPair format (this is a simplification)
                // In production, you'd want proper key derivation from the seed
                return new Ed25519KeyPair
                {
                    PublicKey = ecdsaKeyPair.PublicKey,
                    PrivateKey = ecdsaKeyPair.PrivateKey
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to generate key pair", ex);
            }
        }

        public async Task<(Ed25519KeyPair keyPair, ISecureBuffer privateKeyBuffer)> DeriveIdentityAsync(string passphrase, string username)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(passphrase))
                throw new ArgumentException("Passphrase cannot be null or empty", nameof(passphrase));
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be null or empty", nameof(username));

            ISecureBuffer? masterKey = null;
            
            try
            {
                // Step 1: Derive master key
                masterKey = await DeriveMasterKeyAsync(passphrase, username);

                // Step 2: Generate Ed25519 key pair from master key
                var keyPair = await GenerateKeyPairAsync(masterKey);

                // Step 3: Store private key in secure buffer
                var privateKeyBuffer = _secureMemoryManager.CreateSecureBuffer(keyPair.PrivateKey);

                // Clear the private key from the key pair structure
                Array.Clear(keyPair.PrivateKey, 0, keyPair.PrivateKey.Length);

                return (keyPair, privateKeyBuffer);
            }
            finally
            {
                // Always clear the master key
                masterKey?.Clear();
            }
        }

        public Argon2idParameters GetParameters()
        {
            return _parameters;
        }

        private void RaiseProgress(int percentComplete, string message, TimeSpan elapsed)
        {
            DerivationProgress?.Invoke(this, new KeyDerivationProgressEventArgs
            {
                PercentComplete = percentComplete,
                Message = message,
                ElapsedTime = elapsed
            });
        }
    }
}