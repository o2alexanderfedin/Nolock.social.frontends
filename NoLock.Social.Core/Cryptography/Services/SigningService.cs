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
        private readonly ICryptoJSInteropService _cryptoService;
        private readonly ILogger<SigningService> _logger;

        private const int ED25519_PRIVATE_KEY_SIZE = 32;
        private const int ED25519_PUBLIC_KEY_SIZE = 32;
        private const int ED25519_SIGNATURE_SIZE = 64;

        public SigningService(ICryptoJSInteropService cryptoService, ILogger<SigningService> logger)
        {
            _cryptoService = cryptoService ?? throw new ArgumentNullException(nameof(cryptoService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<SignedContent> SignContentAsync(string content, byte[] privateKey, byte[] publicKey)
        {
            // Validate inputs
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (string.IsNullOrEmpty(content))
            {
                throw new ArgumentException("Content cannot be empty", nameof(content));
            }

            if (privateKey == null)
            {
                throw new ArgumentNullException(nameof(privateKey));
            }

            if (publicKey == null)
            {
                throw new ArgumentNullException(nameof(publicKey));
            }

            if (privateKey.Length != ED25519_PRIVATE_KEY_SIZE)
            {
                throw new ArgumentException($"Private key must be {ED25519_PRIVATE_KEY_SIZE} bytes for Ed25519", nameof(privateKey));
            }

            if (publicKey.Length != ED25519_PUBLIC_KEY_SIZE)
            {
                throw new ArgumentException($"Public key must be {ED25519_PUBLIC_KEY_SIZE} bytes for Ed25519", nameof(publicKey));
            }

            try
            {
                _logger.LogDebug("Starting content signing process");

                // Step 1: Hash the content using SHA-256
                _logger.LogDebug("Computing SHA-256 hash of content");
                var contentHash = await _cryptoService.ComputeSha256Async(content);

                if (contentHash == null || contentHash.Length != 32)
                {
                    throw new CryptoException("Failed to compute SHA-256 hash");
                }

                // Step 2: Sign the hash with Ed25519
                _logger.LogDebug("Signing content hash with Ed25519");
                var signature = await _cryptoService.SignEd25519Async(contentHash, privateKey);

                if (signature == null || signature.Length != ED25519_SIGNATURE_SIZE)
                {
                    throw new CryptoException($"Invalid signature size. Expected {ED25519_SIGNATURE_SIZE} bytes");
                }

                // Step 3: Create signed content object
                var signedContent = new SignedContent
                {
                    Content = content,
                    ContentHash = contentHash,
                    Signature = signature,
                    PublicKey = publicKey,
                    Algorithm = "Ed25519",
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
        public async Task<SignedContent> SignContentAsync(string content, Ed25519KeyPair keyPair)
        {
            if (keyPair == null)
            {
                throw new ArgumentNullException(nameof(keyPair));
            }

            return await SignContentAsync(content, keyPair.PrivateKey, keyPair.PublicKey);
        }
    }
}