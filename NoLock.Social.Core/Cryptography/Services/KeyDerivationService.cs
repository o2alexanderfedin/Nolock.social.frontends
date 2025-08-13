using NoLock.Social.Core.Cryptography.Interfaces;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace NoLock.Social.Core.Cryptography.Services
{
    /// <summary>
    /// Service for deriving cryptographic keys from passphrases using Argon2id
    /// </summary>
    public class KeyDerivationService : IKeyDerivationService
    {
        private readonly ICryptoJSInteropService _cryptoInterop;
        private readonly ISecureMemoryManager _secureMemoryManager;
        private readonly Argon2idParameters _parameters = new();

        public event EventHandler<KeyDerivationProgressEventArgs>? DerivationProgress;

        public KeyDerivationService(ICryptoJSInteropService cryptoInterop, ISecureMemoryManager secureMemoryManager)
        {
            _cryptoInterop = cryptoInterop ?? throw new ArgumentNullException(nameof(cryptoInterop));
            _secureMemoryManager = secureMemoryManager ?? throw new ArgumentNullException(nameof(secureMemoryManager));
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
                RaiseProgress(30, "Deriving key with Argon2id...", stopwatch.Elapsed);

                // Derive key using Argon2id with IMMUTABLE parameters
                // CRITICAL: These parameters MUST NOT be changed as it would break determinism
                var derivedKey = await _cryptoInterop.DeriveKeyArgon2idAsync(passphrase, normalizedUsername);

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
                    Console.WriteLine($"Warning: Key derivation took {stopwatch.ElapsedMilliseconds}ms, exceeding 1000ms timeout");
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
                // Generate Ed25519 key pair from seed
                var keyPair = await _cryptoInterop.GenerateEd25519KeyPairFromSeedAsync(masterKey.Data);
                
                return keyPair;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to generate Ed25519 key pair", ex);
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