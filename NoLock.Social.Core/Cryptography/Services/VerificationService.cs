using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NoLock.Social.Core.Cryptography.Interfaces;

namespace NoLock.Social.Core.Cryptography.Services
{
    /// <summary>
    /// Service for verifying Ed25519 signatures
    /// </summary>
    public class VerificationService : IVerificationService
    {
        private readonly ICryptoJSInteropService _cryptoService;
        private readonly ILogger<VerificationService> _logger;

        private const int ED25519_PUBLIC_KEY_SIZE = 32;
        private const int ED25519_SIGNATURE_SIZE = 64;
        private const int SHA256_HASH_SIZE = 32;

        public VerificationService(ICryptoJSInteropService cryptoService, ILogger<VerificationService> logger)
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

            if (signature.Length != ED25519_SIGNATURE_SIZE)
            {
                throw new ArgumentException($"Signature must be {ED25519_SIGNATURE_SIZE} bytes for Ed25519", nameof(signature));
            }

            if (publicKey.Length != ED25519_PUBLIC_KEY_SIZE)
            {
                throw new ArgumentException($"Public key must be {ED25519_PUBLIC_KEY_SIZE} bytes for Ed25519", nameof(publicKey));
            }

            try
            {
                _logger.LogDebug("Starting signature verification");

                // Step 1: Hash the content using SHA-256
                _logger.LogDebug("Computing SHA-256 hash of content");
                var contentHash = await _cryptoService.ComputeSha256Async(content);

                if (contentHash == null || contentHash.Length != SHA256_HASH_SIZE)
                {
                    throw new CryptoException("Failed to compute SHA-256 hash");
                }

                // Step 2: Verify the signature using Ed25519
                _logger.LogDebug("Verifying Ed25519 signature");
                var isValid = await _cryptoService.VerifyEd25519Async(contentHash, signature, publicKey);

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
                var signature = await _cryptoService.Base64ToBytesAsync(signatureBase64);
                var publicKey = await _cryptoService.Base64ToBytesAsync(publicKeyBase64);

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
        public async Task<bool> VerifySignedContentAsync(SignedContent signedContent)
        {
            if (signedContent == null)
            {
                throw new ArgumentNullException(nameof(signedContent));
            }

            // Validate algorithm
            if (signedContent.Algorithm != "Ed25519")
            {
                throw new NotSupportedException($"Algorithm '{signedContent.Algorithm}' is not supported. Only Ed25519 is supported.");
            }

            // Validate version
            if (signedContent.Version != "1.0")
            {
                throw new NotSupportedException($"Version '{signedContent.Version}' is not supported. Only version 1.0 is supported.");
            }

            _logger.LogDebug("Verifying signed content with algorithm: {Algorithm}, version: {Version}", 
                signedContent.Algorithm, signedContent.Version);

            // Verify the signature
            return await VerifySignatureAsync(
                signedContent.Content, 
                signedContent.Signature, 
                signedContent.PublicKey);
        }
    }
}