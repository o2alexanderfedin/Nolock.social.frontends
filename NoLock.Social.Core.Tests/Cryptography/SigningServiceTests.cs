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
    public class SigningServiceTests
    {
        private readonly Mock<ICryptoJSInteropService> _mockCryptoService;
        private readonly Mock<ILogger<SigningService>> _mockLogger;
        private readonly ISigningService _signingService;

        public SigningServiceTests()
        {
            _mockCryptoService = new Mock<ICryptoJSInteropService>();
            _mockLogger = new Mock<ILogger<SigningService>>();
            _signingService = new SigningService(_mockCryptoService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task SignContentAsync_ValidContent_ReturnsSignedContent()
        {
            // Arrange
            var content = "Test content to sign";
            var privateKey = new byte[32]; // Ed25519 private key is 32 bytes
            var publicKey = new byte[32]; // Ed25519 public key is 32 bytes
            var expectedSignature = new byte[64]; // Ed25519 signature is 64 bytes
            var expectedHash = new byte[32]; // SHA-256 hash is 32 bytes

            // Fill with test data
            for (int i = 0; i < privateKey.Length; i++) privateKey[i] = (byte)i;
            for (int i = 0; i < publicKey.Length; i++) publicKey[i] = (byte)(i + 32);
            for (int i = 0; i < expectedSignature.Length; i++) expectedSignature[i] = (byte)(i + 64);
            for (int i = 0; i < expectedHash.Length; i++) expectedHash[i] = (byte)(i + 128);

            _mockCryptoService.Setup(x => x.ComputeSha256Async(It.IsAny<string>()))
                .ReturnsAsync(expectedHash);

            _mockCryptoService.Setup(x => x.SignEd25519Async(It.IsAny<byte[]>(), It.IsAny<byte[]>()))
                .ReturnsAsync(expectedSignature);

            // Act
            var result = await _signingService.SignContentAsync(content, privateKey, publicKey);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(content, result.Content);
            Assert.Equal(expectedHash, result.ContentHash);
            Assert.Equal(expectedSignature, result.Signature);
            Assert.Equal(publicKey, result.PublicKey);
            Assert.Equal("Ed25519", result.Algorithm);
            Assert.Equal("1.0", result.Version);
            Assert.True(result.Timestamp > DateTime.UtcNow.AddSeconds(-5));
            Assert.True(result.Timestamp <= DateTime.UtcNow);
        }

        [Fact]
        public async Task SignContentAsync_EmptyContent_ThrowsArgumentException()
        {
            // Arrange
            var content = "";
            var privateKey = new byte[32];
            var publicKey = new byte[32];

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _signingService.SignContentAsync(content, privateKey, publicKey));
        }

        [Fact]
        public async Task SignContentAsync_NullContent_ThrowsArgumentNullException()
        {
            // Arrange
            string content = null!;
            var privateKey = new byte[32];
            var publicKey = new byte[32];

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _signingService.SignContentAsync(content, privateKey, publicKey));
        }

        [Fact]
        public async Task SignContentAsync_InvalidPrivateKeyLength_ThrowsArgumentException()
        {
            // Arrange
            var content = "Test content";
            var privateKey = new byte[16]; // Invalid length
            var publicKey = new byte[32];

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => _signingService.SignContentAsync(content, privateKey, publicKey));
            Assert.Contains("private key", exception.Message.ToLower());
        }

        [Fact]
        public async Task SignContentAsync_InvalidPublicKeyLength_ThrowsArgumentException()
        {
            // Arrange
            var content = "Test content";
            var privateKey = new byte[32];
            var publicKey = new byte[16]; // Invalid length

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => _signingService.SignContentAsync(content, privateKey, publicKey));
            Assert.Contains("public key", exception.Message.ToLower());
        }

        [Fact]
        public async Task SignContentAsync_NullPrivateKey_ThrowsArgumentNullException()
        {
            // Arrange
            var content = "Test content";
            byte[] privateKey = null!;
            var publicKey = new byte[32];

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _signingService.SignContentAsync(content, privateKey, publicKey));
        }

        [Fact]
        public async Task SignContentAsync_NullPublicKey_ThrowsArgumentNullException()
        {
            // Arrange
            var content = "Test content";
            var privateKey = new byte[32];
            byte[] publicKey = null!;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _signingService.SignContentAsync(content, privateKey, publicKey));
        }

        [Fact]
        public async Task SignContentAsync_HashingFails_ThrowsCryptoException()
        {
            // Arrange
            var content = "Test content";
            var privateKey = new byte[32];
            var publicKey = new byte[32];

            _mockCryptoService.Setup(x => x.ComputeSha256Async(It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Hashing failed"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<CryptoException>(
                () => _signingService.SignContentAsync(content, privateKey, publicKey));
            Assert.Contains("hash", exception.Message.ToLower());
        }

        [Fact]
        public async Task SignContentAsync_SigningFails_ThrowsCryptoException()
        {
            // Arrange
            var content = "Test content";
            var privateKey = new byte[32];
            var publicKey = new byte[32];
            var expectedHash = new byte[32];

            _mockCryptoService.Setup(x => x.ComputeSha256Async(It.IsAny<string>()))
                .ReturnsAsync(expectedHash);

            _mockCryptoService.Setup(x => x.SignEd25519Async(It.IsAny<byte[]>(), It.IsAny<byte[]>()))
                .ThrowsAsync(new InvalidOperationException("Signing failed"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<CryptoException>(
                () => _signingService.SignContentAsync(content, privateKey, publicKey));
            Assert.Contains("sign", exception.Message.ToLower());
        }

        [Fact]
        public async Task SignContentAsync_LargeContent_SignsSuccessfully()
        {
            // Arrange
            var contentBuilder = new StringBuilder();
            for (int i = 0; i < 10000; i++)
            {
                contentBuilder.AppendLine($"Line {i}: This is test content that will be signed.");
            }
            var content = contentBuilder.ToString();
            var privateKey = new byte[32];
            var publicKey = new byte[32];
            var expectedSignature = new byte[64];
            var expectedHash = new byte[32];

            _mockCryptoService.Setup(x => x.ComputeSha256Async(It.IsAny<string>()))
                .ReturnsAsync(expectedHash);

            _mockCryptoService.Setup(x => x.SignEd25519Async(It.IsAny<byte[]>(), It.IsAny<byte[]>()))
                .ReturnsAsync(expectedSignature);

            // Act
            var result = await _signingService.SignContentAsync(content, privateKey, publicKey);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(content, result.Content);
            Assert.Equal(expectedSignature, result.Signature);
        }

        [Fact]
        public async Task SignContentAsync_UnicodeContent_SignsCorrectly()
        {
            // Arrange
            var content = "Hello 世界 🌍 Привет мир";
            var privateKey = new byte[32];
            var publicKey = new byte[32];
            var expectedSignature = new byte[64];
            var expectedHash = new byte[32];

            _mockCryptoService.Setup(x => x.ComputeSha256Async(It.IsAny<string>()))
                .ReturnsAsync(expectedHash);

            _mockCryptoService.Setup(x => x.SignEd25519Async(It.IsAny<byte[]>(), It.IsAny<byte[]>()))
                .ReturnsAsync(expectedSignature);

            // Act
            var result = await _signingService.SignContentAsync(content, privateKey, publicKey);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(content, result.Content);
            _mockCryptoService.Verify(x => x.ComputeSha256Async(It.Is<string>(
                data => data == content)), Times.Once);
        }

        [Fact]
        public void ToBase64_ReturnsCorrectBase64Representation()
        {
            // Arrange
            var content = "Test content";
            var signature = new byte[] { 1, 2, 3, 4, 5 };
            var publicKey = new byte[] { 6, 7, 8, 9, 10 };
            var contentHash = new byte[] { 11, 12, 13, 14, 15 };

            var signedContent = new SignedContent
            {
                Content = content,
                Signature = signature,
                PublicKey = publicKey,
                ContentHash = contentHash,
                Algorithm = "Ed25519",
                Version = "1.0",
                Timestamp = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc)
            };

            // Act
            var base64Result = signedContent.ToBase64();

            // Assert
            Assert.NotNull(base64Result);
            Assert.Equal(Convert.ToBase64String(signature), base64Result.SignatureBase64);
            Assert.Equal(Convert.ToBase64String(publicKey), base64Result.PublicKeyBase64);
            Assert.Equal(Convert.ToBase64String(contentHash), base64Result.ContentHashBase64);
            Assert.Equal("Ed25519", base64Result.Algorithm);
            Assert.Equal("1.0", base64Result.Version);
            Assert.Equal(signedContent.Timestamp, base64Result.Timestamp);
        }

        [Fact]
        public async Task SignContentWithKeyPair_ValidInput_SignsSuccessfully()
        {
            // Arrange
            var content = "Test content";
            var keyPair = new Ed25519KeyPair
            {
                PrivateKey = new byte[32],
                PublicKey = new byte[32]
            };
            var expectedSignature = new byte[64];
            var expectedHash = new byte[32];

            _mockCryptoService.Setup(x => x.ComputeSha256Async(It.IsAny<string>()))
                .ReturnsAsync(expectedHash);

            _mockCryptoService.Setup(x => x.SignEd25519Async(It.IsAny<byte[]>(), It.IsAny<byte[]>()))
                .ReturnsAsync(expectedSignature);

            // Act
            var result = await _signingService.SignContentAsync(content, keyPair);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(content, result.Content);
            Assert.Equal(keyPair.PublicKey, result.PublicKey);
        }

        [Fact]
        public async Task SignContentWithKeyPair_NullKeyPair_ThrowsArgumentNullException()
        {
            // Arrange
            var content = "Test content";
            Ed25519KeyPair keyPair = null!;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _signingService.SignContentAsync(content, keyPair));
        }
    }
}