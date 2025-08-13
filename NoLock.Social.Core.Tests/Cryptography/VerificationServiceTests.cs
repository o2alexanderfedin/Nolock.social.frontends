using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Core.Cryptography.Interfaces;
using NoLock.Social.Core.Cryptography.Services;
using Xunit;

namespace NoLock.Social.Core.Tests.Cryptography
{
    public class VerificationServiceTests
    {
        private readonly Mock<ICryptoJSInteropService> _mockCryptoService;
        private readonly Mock<ILogger<VerificationService>> _mockLogger;
        private readonly IVerificationService _verificationService;

        public VerificationServiceTests()
        {
            _mockCryptoService = new Mock<ICryptoJSInteropService>();
            _mockLogger = new Mock<ILogger<VerificationService>>();
            _verificationService = new VerificationService(_mockCryptoService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task VerifySignatureAsync_ValidSignature_ReturnsTrue()
        {
            // Arrange
            var content = "Test content to verify";
            var signature = new byte[64]; // Ed25519 signature
            var publicKey = new byte[32]; // Ed25519 public key
            var expectedHash = new byte[32]; // SHA-256 hash

            for (int i = 0; i < signature.Length; i++) signature[i] = (byte)i;
            for (int i = 0; i < publicKey.Length; i++) publicKey[i] = (byte)(i + 64);
            for (int i = 0; i < expectedHash.Length; i++) expectedHash[i] = (byte)(i + 96);

            _mockCryptoService.Setup(x => x.ComputeSha256Async(It.IsAny<string>()))
                .ReturnsAsync(expectedHash);

            _mockCryptoService.Setup(x => x.VerifyEd25519Async(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>()))
                .ReturnsAsync(true);

            // Act
            var result = await _verificationService.VerifySignatureAsync(content, signature, publicKey);

            // Assert
            Assert.True(result);
            _mockCryptoService.Verify(x => x.ComputeSha256Async(content), Times.Once);
            _mockCryptoService.Verify(x => x.VerifyEd25519Async(expectedHash, signature, publicKey), Times.Once);
        }

        [Fact]
        public async Task VerifySignatureAsync_InvalidSignature_ReturnsFalse()
        {
            // Arrange
            var content = "Test content to verify";
            var signature = new byte[64];
            var publicKey = new byte[32];
            var expectedHash = new byte[32];

            _mockCryptoService.Setup(x => x.ComputeSha256Async(It.IsAny<string>()))
                .ReturnsAsync(expectedHash);

            _mockCryptoService.Setup(x => x.VerifyEd25519Async(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>()))
                .ReturnsAsync(false);

            // Act
            var result = await _verificationService.VerifySignatureAsync(content, signature, publicKey);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task VerifySignatureAsync_EmptyContent_ThrowsArgumentException()
        {
            // Arrange
            var content = "";
            var signature = new byte[64];
            var publicKey = new byte[32];

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _verificationService.VerifySignatureAsync(content, signature, publicKey));
        }

        [Fact]
        public async Task VerifySignatureAsync_NullContent_ThrowsArgumentNullException()
        {
            // Arrange
            string content = null!;
            var signature = new byte[64];
            var publicKey = new byte[32];

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _verificationService.VerifySignatureAsync(content, signature, publicKey));
        }

        [Fact]
        public async Task VerifySignatureAsync_InvalidSignatureLength_ThrowsArgumentException()
        {
            // Arrange
            var content = "Test content";
            var signature = new byte[32]; // Invalid length
            var publicKey = new byte[32];

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => _verificationService.VerifySignatureAsync(content, signature, publicKey));
            Assert.Contains("signature", exception.Message.ToLower());
        }

        [Fact]
        public async Task VerifySignatureAsync_InvalidPublicKeyLength_ThrowsArgumentException()
        {
            // Arrange
            var content = "Test content";
            var signature = new byte[64];
            var publicKey = new byte[16]; // Invalid length

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => _verificationService.VerifySignatureAsync(content, signature, publicKey));
            Assert.Contains("public key", exception.Message.ToLower());
        }

        [Fact]
        public async Task VerifySignatureAsync_NullSignature_ThrowsArgumentNullException()
        {
            // Arrange
            var content = "Test content";
            byte[] signature = null!;
            var publicKey = new byte[32];

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _verificationService.VerifySignatureAsync(content, signature, publicKey));
        }

        [Fact]
        public async Task VerifySignatureAsync_NullPublicKey_ThrowsArgumentNullException()
        {
            // Arrange
            var content = "Test content";
            var signature = new byte[64];
            byte[] publicKey = null!;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _verificationService.VerifySignatureAsync(content, signature, publicKey));
        }

        [Fact]
        public async Task VerifySignedContent_ValidSignature_ReturnsTrue()
        {
            // Arrange
            var signedContent = new SignedContent
            {
                Content = "Test content",
                ContentHash = new byte[32],
                Signature = new byte[64],
                PublicKey = new byte[32],
                Algorithm = "Ed25519",
                Version = "1.0"
            };

            var expectedHash = new byte[32];
            for (int i = 0; i < expectedHash.Length; i++) expectedHash[i] = (byte)i;

            _mockCryptoService.Setup(x => x.ComputeSha256Async(It.IsAny<string>()))
                .ReturnsAsync(expectedHash);

            _mockCryptoService.Setup(x => x.VerifyEd25519Async(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>()))
                .ReturnsAsync(true);

            // Act
            var result = await _verificationService.VerifySignedContentAsync(signedContent);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task VerifySignedContent_NullSignedContent_ThrowsArgumentNullException()
        {
            // Arrange
            SignedContent signedContent = null!;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _verificationService.VerifySignedContentAsync(signedContent));
        }

        [Fact]
        public async Task VerifySignedContent_UnsupportedAlgorithm_ThrowsNotSupportedException()
        {
            // Arrange
            var signedContent = new SignedContent
            {
                Content = "Test content",
                Signature = new byte[64],
                PublicKey = new byte[32],
                Algorithm = "RSA", // Unsupported
                Version = "1.0"
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<NotSupportedException>(
                () => _verificationService.VerifySignedContentAsync(signedContent));
            Assert.Contains("algorithm", exception.Message.ToLower());
        }

        [Fact]
        public async Task VerifySignedContent_UnsupportedVersion_ThrowsNotSupportedException()
        {
            // Arrange
            var signedContent = new SignedContent
            {
                Content = "Test content",
                Signature = new byte[64],
                PublicKey = new byte[32],
                Algorithm = "Ed25519",
                Version = "2.0" // Unsupported version
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<NotSupportedException>(
                () => _verificationService.VerifySignedContentAsync(signedContent));
            Assert.Contains("version", exception.Message.ToLower());
        }

        [Fact]
        public async Task VerifyBase64Signature_ValidSignature_ReturnsTrue()
        {
            // Arrange
            var content = "Test content";
            var signature = new byte[64];
            var publicKey = new byte[32];

            for (int i = 0; i < signature.Length; i++) signature[i] = (byte)i;
            for (int i = 0; i < publicKey.Length; i++) publicKey[i] = (byte)(i + 64);

            var signatureBase64 = Convert.ToBase64String(signature);
            var publicKeyBase64 = Convert.ToBase64String(publicKey);

            _mockCryptoService.Setup(x => x.Base64ToBytesAsync(signatureBase64))
                .ReturnsAsync(signature);

            _mockCryptoService.Setup(x => x.Base64ToBytesAsync(publicKeyBase64))
                .ReturnsAsync(publicKey);

            _mockCryptoService.Setup(x => x.ComputeSha256Async(It.IsAny<string>()))
                .ReturnsAsync(new byte[32]);

            _mockCryptoService.Setup(x => x.VerifyEd25519Async(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>()))
                .ReturnsAsync(true);

            // Act
            var result = await _verificationService.VerifySignatureAsync(content, signatureBase64, publicKeyBase64);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task VerifyBase64Signature_InvalidBase64_ThrowsFormatException()
        {
            // Arrange
            var content = "Test content";
            var signatureBase64 = "invalid-base64!@#$";
            var publicKeyBase64 = Convert.ToBase64String(new byte[32]);

            _mockCryptoService.Setup(x => x.Base64ToBytesAsync(signatureBase64))
                .ThrowsAsync(new FormatException("Invalid base64"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<FormatException>(
                () => _verificationService.VerifySignatureAsync(content, signatureBase64, publicKeyBase64));
            Assert.Contains("base64", exception.Message.ToLower());
        }

        [Fact]
        public async Task VerifySignatureAsync_HashingFails_ThrowsCryptoException()
        {
            // Arrange
            var content = "Test content";
            var signature = new byte[64];
            var publicKey = new byte[32];

            _mockCryptoService.Setup(x => x.ComputeSha256Async(It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Hashing failed"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<CryptoException>(
                () => _verificationService.VerifySignatureAsync(content, signature, publicKey));
            Assert.Contains("hash", exception.Message.ToLower());
        }

        [Fact]
        public async Task VerifySignatureAsync_VerificationFails_ThrowsCryptoException()
        {
            // Arrange
            var content = "Test content";
            var signature = new byte[64];
            var publicKey = new byte[32];

            _mockCryptoService.Setup(x => x.ComputeSha256Async(It.IsAny<string>()))
                .ReturnsAsync(new byte[32]);

            _mockCryptoService.Setup(x => x.VerifyEd25519Async(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>()))
                .ThrowsAsync(new InvalidOperationException("Verification failed"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<CryptoException>(
                () => _verificationService.VerifySignatureAsync(content, signature, publicKey));
            Assert.Contains("verification", exception.Message.ToLower());
        }

        [Fact]
        public async Task VerifySignatureAsync_LargeContent_VerifiesSuccessfully()
        {
            // Arrange
            var contentBuilder = new StringBuilder();
            for (int i = 0; i < 10000; i++)
            {
                contentBuilder.AppendLine($"Line {i}: This is test content that will be verified.");
            }
            var content = contentBuilder.ToString();
            var signature = new byte[64];
            var publicKey = new byte[32];

            _mockCryptoService.Setup(x => x.ComputeSha256Async(It.IsAny<string>()))
                .ReturnsAsync(new byte[32]);

            _mockCryptoService.Setup(x => x.VerifyEd25519Async(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>()))
                .ReturnsAsync(true);

            // Act
            var result = await _verificationService.VerifySignatureAsync(content, signature, publicKey);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task VerifySignatureAsync_UnicodeContent_VerifiesCorrectly()
        {
            // Arrange
            var content = "Hello 世界 🌍 Привет мир";
            var signature = new byte[64];
            var publicKey = new byte[32];

            _mockCryptoService.Setup(x => x.ComputeSha256Async(It.IsAny<string>()))
                .ReturnsAsync(new byte[32]);

            _mockCryptoService.Setup(x => x.VerifyEd25519Async(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>()))
                .ReturnsAsync(true);

            // Act
            var result = await _verificationService.VerifySignatureAsync(content, signature, publicKey);

            // Assert
            Assert.True(result);
            _mockCryptoService.Verify(x => x.ComputeSha256Async(content), Times.Once);
        }
    }
}