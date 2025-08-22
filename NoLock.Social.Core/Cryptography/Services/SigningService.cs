using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NoLock.Social.Core.Cryptography.Interfaces;

namespace NoLock.Social.Core.Cryptography.Services
{
    /// <summary>
    /// Service for signing content with Ed25519 signatures
    /// </summary>
    public class SigningService : ISigningService
    {
        private readonly IWebCryptoService _cryptoService;
        private readonly ILogger<SigningService> _logger;

        // PKCS8 format for P-256 private key is ~138 bytes
        // SPKI format for P-256 public key is ~91 bytes
        private const int ECDSA_MIN_PRIVATE_KEY_SIZE = 100;  // PKCS8 format
        private const int ECDSA_MIN_PUBLIC_KEY_SIZE = 80;    // SPKI format
        private const int ECDSA_MIN_SIGNATURE_SIZE = 64;     // P-256 signature size

        public SigningService(IWebCryptoService cryptoService, ILogger<SigningService> logger)
        {
            _cryptoService = cryptoService ?? throw new ArgumentNullException(nameof(cryptoService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<SignedTarget> SignAsync(string targetHash, byte[] privateKey, byte[] publicKey)
        {
            // Validate inputs
            if (targetHash == null)
            {
                throw new ArgumentNullException(nameof(targetHash));
            }

            if (string.IsNullOrEmpty(targetHash))
            {
                throw new ArgumentException("Content cannot be empty", nameof(targetHash));
            }

            if (privateKey == null)
            {
                throw new ArgumentNullException(nameof(privateKey));
            }

            if (publicKey == null)
            {
                throw new ArgumentNullException(nameof(publicKey));
            }

            if (privateKey.Length < ECDSA_MIN_PRIVATE_KEY_SIZE)
            {
                throw new ArgumentException($"Private key must be at least {ECDSA_MIN_PRIVATE_KEY_SIZE} bytes for ECDSA P-256 PKCS8 format", nameof(privateKey));
            }

            if (publicKey.Length < ECDSA_MIN_PUBLIC_KEY_SIZE)
            {
                throw new ArgumentException($"Public key must be at least {ECDSA_MIN_PUBLIC_KEY_SIZE} bytes for ECDSA P-256 SPKI format", nameof(publicKey));
            }

            try
            {
                _logger.LogDebug("Starting content signing process");

                // Step 1: Hash the content using SHA-256
                _logger.LogDebug("Computing SHA-256 hash of content");
                var contentBytes = Encoding.UTF8.GetBytes(targetHash);
                var contentHash = await _cryptoService.Sha256Async(contentBytes);

                if (contentHash == null || contentHash.Length != 32)
                {
                    throw new CryptoException("Failed to compute SHA-256 hash");
                }

                // Step 2: Sign the hash with ECDSA P-256
                _logger.LogDebug("Signing content hash with ECDSA P-256");
                var signature = await _cryptoService.SignECDSAAsync(privateKey, contentHash, "P-256", "SHA-256");

                if (signature == null || signature.Length < ECDSA_MIN_SIGNATURE_SIZE)
                {
                    throw new CryptoException($"Invalid signature size. Expected at least {ECDSA_MIN_SIGNATURE_SIZE} bytes");
                }

                // Step 3: Create signed content object
                var signedContent = new SignedTarget
                {
                    TargetHash = contentHash,
                    Signature = signature,
                    PublicKey = publicKey,
                    Algorithm = "ECDSA-P256",
                    Version = "1.0",
                    Timestamp = DateTime.UtcNow
                };

                _logger.LogInformation("Content successfully signed");
                return signedContent;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Cryptographic operation failed");
                
                // Determine the type of error based on the exception message
                if (ex.Message.ToLower().Contains("hash"))
                {
                    throw new CryptoException("Failed to hash content", ex);
                }
                else if (ex.Message.ToLower().Contains("sign"))
                {
                    throw new CryptoException("Failed to sign content", ex);
                }
                else
                {
                    throw new CryptoException("Cryptographic operation failed", ex);
                }
            }
            catch (Exception ex) when (!(ex is ArgumentException || ex is ArgumentNullException || ex is CryptoException))
            {
                _logger.LogError(ex, "Unexpected error during content signing");
                throw new CryptoException("An unexpected error occurred during signing", ex);
            }
        }

        /// <inheritdoc />
        public async Task<SignedTarget> SignAsync(string targetHash, Ed25519KeyPair keyPair)
        {
            if (keyPair == null)
            {
                throw new ArgumentNullException(nameof(keyPair));
            }

            return await SignAsync(targetHash, keyPair.PrivateKey, keyPair.PublicKey);
        }
    }
}