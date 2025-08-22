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
        private readonly Mock<IWebCryptoService> _mockCryptoService;
        private readonly Mock<ILogger<VerificationService>> _mockLogger;
        private readonly IVerificationService _verificationService;

        public VerificationServiceTests()
        {
            _mockCryptoService = new Mock<IWebCryptoService>();
            _mockLogger = new Mock<ILogger<VerificationService>>();
            _verificationService = new VerificationService(_mockCryptoService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task VerifySignatureAsync_ValidSignature_ReturnsTrue()
        {
            // Arrange
            var content = "Test content to verify";
            var signature = new byte[64]; // ECDSA P-256 signature
            var publicKey = new byte[91]; // ECDSA P-256 public key in SPKI format
            var expectedHash = new byte[32]; // SHA-256 hash

            for (var i = 0; i < signature.Length; i++) signature[i] = (byte)i;
            for (var i = 0; i < publicKey.Length; i++) publicKey[i] = (byte)(i + 64);
            for (var i = 0; i < expectedHash.Length; i++) expectedHash[i] = (byte)(i + 96);

            _mockCryptoService.Setup(x => x.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _mockCryptoService.Setup(x => x.VerifyECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>(), "P-256", "SHA-256"))
                .ReturnsAsync(true);

            // Act
            var result = await _verificationService.VerifySignatureAsync(content, signature, publicKey);

            // Assert
            Assert.True(result);
            _mockCryptoService.Verify(x => x.Sha256Async(It.IsAny<byte[]>()), Times.Once);
            _mockCryptoService.Verify(x => x.VerifyECDSAAsync(publicKey, signature, expectedHash, "P-256", "SHA-256"), Times.Once);
        }

        [Fact]
        public async Task VerifySignatureAsync_InvalidSignature_ReturnsFalse()
        {
            // Arrange
            var content = "Test content to verify";
            var signature = new byte[64];
            var publicKey = new byte[91];
            var expectedHash = new byte[32];

            _mockCryptoService.Setup(x => x.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _mockCryptoService.Setup(x => x.VerifyECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>(), "P-256", "SHA-256"))
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
            var publicKey = new byte[91];

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
            var publicKey = new byte[91];

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
            var publicKey = new byte[91];

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
            var publicKey = new byte[91];

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
            var signedContent = new SignedTarget
            {
                TargetHash = new byte[32],
                Signature = new byte[64],
                PublicKey = new byte[91],
                Algorithm = "ECDSA-P256",
                Version = "1.0"
            };

            var expectedHash = new byte[32];
            for (var i = 0; i < expectedHash.Length; i++) expectedHash[i] = (byte)i;

            _mockCryptoService.Setup(x => x.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _mockCryptoService.Setup(x => x.VerifyECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>(), "P-256", "SHA-256"))
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
            SignedTarget signedTarget = null!;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _verificationService.VerifySignedContentAsync(signedTarget));
        }

        [Fact]
        public async Task VerifySignedContent_UnsupportedAlgorithm_ThrowsNotSupportedException()
        {
            // Arrange
            var signedContent = new SignedTarget
            {
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
            var signedContent = new SignedTarget
            {
                Signature = new byte[64],
                PublicKey = new byte[32],
                Algorithm = "ECDSA-P256",
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
            var publicKey = new byte[91];

            for (var i = 0; i < signature.Length; i++) signature[i] = (byte)i;
            for (var i = 0; i < publicKey.Length; i++) publicKey[i] = (byte)(i + 64);

            var signatureBase64 = Convert.ToBase64String(signature);
            var publicKeyBase64 = Convert.ToBase64String(publicKey);

            // Base64 conversion is now done directly in the service
            
            _mockCryptoService.Setup(x => x.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(new byte[32]);

            _mockCryptoService.Setup(x => x.VerifyECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>(), "P-256", "SHA-256"))
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

            // Base64 conversion will throw directly from Convert.FromBase64String

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
            var publicKey = new byte[91];

            _mockCryptoService.Setup(x => x.Sha256Async(It.IsAny<byte[]>()))
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
            var publicKey = new byte[91];

            _mockCryptoService.Setup(x => x.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(new byte[32]);

            _mockCryptoService.Setup(x => x.VerifyECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>(), "P-256", "SHA-256"))
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
            for (var i = 0; i < 10000; i++)
            {
                contentBuilder.AppendLine($"Line {i}: This is test content that will be verified.");
            }
            var content = contentBuilder.ToString();
            var signature = new byte[64];
            var publicKey = new byte[91];

            _mockCryptoService.Setup(x => x.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(new byte[32]);

            _mockCryptoService.Setup(x => x.VerifyECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>(), "P-256", "SHA-256"))
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
            var publicKey = new byte[91];

            _mockCryptoService.Setup(x => x.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(new byte[32]);

            _mockCryptoService.Setup(x => x.VerifyECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>(), "P-256", "SHA-256"))
                .ReturnsAsync(true);

            // Act
            var result = await _verificationService.VerifySignatureAsync(content, signature, publicKey);

            // Assert
            Assert.True(result);
            _mockCryptoService.Verify(x => x.Sha256Async(It.IsAny<byte[]>()), Times.Once);
        }
    }
}