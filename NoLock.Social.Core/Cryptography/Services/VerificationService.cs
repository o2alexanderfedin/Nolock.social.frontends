using System.Text;
using Microsoft.Extensions.Logging;
using NoLock.Social.Core.Cryptography.Interfaces;

namespace NoLock.Social.Core.Cryptography.Services
{
    /// <summary>
    /// Service for verifying Ed25519 signatures
    /// </summary>
    public class VerificationService : IVerificationService
    {
        private readonly IWebCryptoService _cryptoService;
        private readonly ILogger<VerificationService> _logger;

        private const int ECDSA_MIN_PUBLIC_KEY_SIZE = 80;   // SPKI format
        private const int ECDSA_MIN_SIGNATURE_SIZE = 64;    // P-256 signature size
        private const int SHA256_HASH_SIZE = 32;

        public VerificationService(IWebCryptoService cryptoService, ILogger<VerificationService> logger)
        {
            _cryptoService = cryptoService ?? throw new ArgumentNullException(nameof(cryptoService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<bool> VerifySignatureAsync(string content, byte[] signature, byte[] publicKey)
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

            if (signature == null)
            {
                throw new ArgumentNullException(nameof(signature));
            }

            if (publicKey == null)
            {
                throw new ArgumentNullException(nameof(publicKey));
            }

            if (signature.Length < ECDSA_MIN_SIGNATURE_SIZE)
            {
                throw new ArgumentException($"Signature must be at least {ECDSA_MIN_SIGNATURE_SIZE} bytes for ECDSA P-256", nameof(signature));
            }

            if (publicKey.Length < ECDSA_MIN_PUBLIC_KEY_SIZE)
            {
                throw new ArgumentException($"Public key must be at least {ECDSA_MIN_PUBLIC_KEY_SIZE} bytes for ECDSA P-256 SPKI format", nameof(publicKey));
            }

            try
            {
                _logger.LogDebug("Starting signature verification");

                // Step 1: Hash the content using SHA-256
                _logger.LogDebug("Computing SHA-256 hash of content");
                var contentBytes = Encoding.UTF8.GetBytes(content);
                var contentHash = await _cryptoService.Sha256Async(contentBytes);

                if (contentHash == null || contentHash.Length != SHA256_HASH_SIZE)
                {
                    throw new CryptoException("Failed to compute SHA-256 hash");
                }

                // Step 2: Verify the signature using ECDSA P-256
                _logger.LogDebug("Verifying ECDSA P-256 signature");
                var isValid = await _cryptoService.VerifyECDSAAsync(publicKey, signature, contentHash, "P-256", "SHA-256");

                if (isValid)
                {
                    _logger.LogInformation("Signature verification successful");
                }
                else
                {
                    _logger.LogWarning("Signature verification failed - invalid signature");
                }

                return isValid;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Cryptographic operation failed during verification");
                
                // Determine the type of error based on the exception message
                if (ex.Message.ToLower().Contains("hash"))
                {
                    throw new CryptoException("Failed to hash content during verification", ex);
                }
                else if (ex.Message.ToLower().Contains("verif"))
                {
                    throw new CryptoException("Failed to perform signature verification", ex);
                }
                else
                {
                    throw new CryptoException("Cryptographic operation failed during verification", ex);
                }
            }
            catch (Exception ex) when (!(ex is ArgumentException || ex is ArgumentNullException || ex is CryptoException))
            {
                _logger.LogError(ex, "Unexpected error during signature verification");
                throw new CryptoException("An unexpected error occurred during verification", ex);
            }
        }

        /// <inheritdoc />
        public async Task<bool> VerifySignatureAsync(string content, string signatureBase64, string publicKeyBase64)
        {
            if (string.IsNullOrEmpty(signatureBase64))
            {
                throw new ArgumentException("Signature base64 cannot be empty", nameof(signatureBase64));
            }

            if (string.IsNullOrEmpty(publicKeyBase64))
            {
                throw new ArgumentException("Public key base64 cannot be empty", nameof(publicKeyBase64));
            }

            try
            {
                _logger.LogDebug("Converting base64 values to bytes");
                
                // Convert base64 strings to byte arrays
                var signature = Convert.FromBase64String(signatureBase64);
                var publicKey = Convert.FromBase64String(publicKeyBase64);

                // Verify using the byte array method
                return await VerifySignatureAsync(content, signature, publicKey);
            }
            catch (FormatException ex)
            {
                _logger.LogError(ex, "Invalid base64 format");
                throw new FormatException("Invalid base64 format in signature or public key", ex);
            }
        }

        /// <inheritdoc />
        public async Task<bool> VerifySignedContentAsync(SignedTarget signedTarget)
        {
            if (signedTarget == null)
            {
                throw new ArgumentNullException(nameof(signedTarget));
            }

            // Validate algorithm
            if (signedTarget.Algorithm != "ECDSA-P256")
            {
                throw new NotSupportedException($"Algorithm '{signedTarget.Algorithm}' is not supported. Only ECDSA-P256 is supported.");
            }

            // Validate version
            if (signedTarget.Version != "1.0")
            {
                throw new NotSupportedException($"Version '{signedTarget.Version}' is not supported. Only version 1.0 is supported.");
            }

            _logger.LogDebug("Verifying signed content with algorithm: {Algorithm}, version: {Version}", 
                signedTarget.Algorithm, signedTarget.Version);

            try
            {
                // Verify the signature directly against the target hash
                _logger.LogDebug("Verifying ECDSA P-256 signature against target hash");
                var isValid = await _cryptoService.VerifyECDSAAsync(
                    signedTarget.PublicKey, 
                    signedTarget.Signature, 
                    signedTarget.TargetHash, 
                    "P-256", 
                    "SHA-256");

                if (isValid)
                {
                    _logger.LogInformation("Signature verification successful");
                }
                else
                {
                    _logger.LogWarning("Signature verification failed - invalid signature");
                }

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying signed target");
                throw new CryptoException("Failed to verify signed target", ex);
            }
        }
    }
}