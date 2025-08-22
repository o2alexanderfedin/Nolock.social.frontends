using System;
using System.Threading.Tasks;

namespace NoLock.Social.Core.Cryptography.Interfaces
{
    /// <summary>
    /// Service for verifying Ed25519 signatures
    /// </summary>
    public interface IVerificationService
    {
        /// <summary>
        /// Verifies an Ed25519 signature for content
        /// </summary>
        /// <param name="content">The content that was signed</param>
        /// <param name="signature">The Ed25519 signature (64 bytes)</param>
        /// <param name="publicKey">The Ed25519 public key (32 bytes)</param>
        /// <returns>True if signature is valid, false otherwise</returns>
        Task<bool> VerifySignatureAsync(string content, byte[] signature, byte[] publicKey);

        /// <summary>
        /// Verifies an Ed25519 signature for content using base64-encoded values
        /// </summary>
        /// <param name="content">The content that was signed</param>
        /// <param name="signatureBase64">The base64-encoded Ed25519 signature</param>
        /// <param name="publicKeyBase64">The base64-encoded Ed25519 public key</param>
        /// <returns>True if signature is valid, false otherwise</returns>
        Task<bool> VerifySignatureAsync(string content, string signatureBase64, string publicKeyBase64);

        /// <summary>
        /// Verifies a SignedContent object
        /// </summary>
        /// <param name="signedTarget">The signed content to verify</param>
        /// <returns>True if signature is valid, false otherwise</returns>
        Task<bool> VerifySignedContentAsync(SignedTarget signedTarget);
    }
}