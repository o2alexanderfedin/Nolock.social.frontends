using System;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Core.Cryptography.Interfaces;
using NoLock.Social.Core.Cryptography.Services;
using Xunit;

namespace NoLock.Social.Core.Tests.Cryptography.Services
{
    public class SigningServiceTests
    {
        private readonly Mock<IWebCryptoService> _cryptoServiceMock;
        private readonly Mock<ILogger<SigningService>> _loggerMock;
        private readonly SigningService _service;

        public SigningServiceTests()
        {
            _cryptoServiceMock = new Mock<IWebCryptoService>();
            _loggerMock = new Mock<ILogger<SigningService>>();

            _service = new SigningService(_cryptoServiceMock.Object, _loggerMock.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidDependencies_ShouldInitialize()
        {
            // Arrange & Act
            var service = new SigningService(_cryptoServiceMock.Object, _loggerMock.Object);

            // Assert
            service.Should().NotBeNull();
            service.Should().BeAssignableTo<ISigningService>();
        }

        [Theory]
        [InlineData(null, "cryptoService", "Null crypto service")]
        [InlineData("logger", null, "Null logger")]
        public void Constructor_WithNullDependencies_ShouldThrowArgumentNullException(
            string nullParam1, string nullParam2, string scenario)
        {
            // Arrange
            var cryptoService = nullParam1 == null ? null : _cryptoServiceMock.Object;
            var logger = nullParam1 == "logger" ? null : _loggerMock.Object;

            // Act & Assert
            var action = () => new SigningService(cryptoService, logger);
            action.Should().Throw<ArgumentNullException>();
        }

        #endregion

        #region SignAsync (byte arrays) Tests

        [Fact]
        public async Task SignAsync_WithNullTargetHash_ShouldThrowArgumentNullException()
        {
            // Arrange
            var privateKey = new byte[138];
            var publicKey = new byte[91];

            // Act & Assert
            await _service.Invoking(s => s.SignAsync(null, privateKey, publicKey))
                .Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("targetHash");
        }

        [Fact]
        public async Task SignAsync_WithEmptyTargetHash_ShouldThrowArgumentException()
        {
            // Arrange
            var privateKey = new byte[138];
            var publicKey = new byte[91];

            // Act & Assert
            await _service.Invoking(s => s.SignAsync("", privateKey, publicKey))
                .Should().ThrowAsync<ArgumentException>()
                .WithParameterName("targetHash")
                .WithMessage("Content cannot be empty*");
        }

        [Fact]
        public async Task SignAsync_WithNullPrivateKey_ShouldThrowArgumentNullException()
        {
            // Arrange
            var publicKey = new byte[91];

            // Act & Assert
            await _service.Invoking(s => s.SignAsync("test content", null, publicKey))
                .Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("privateKey");
        }

        [Fact]
        public async Task SignAsync_WithNullPublicKey_ShouldThrowArgumentNullException()
        {
            // Arrange
            var privateKey = new byte[138];

            // Act & Assert
            await _service.Invoking(s => s.SignAsync("test content", privateKey, null))
                .Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("publicKey");
        }

        [Theory]
        [InlineData(32, "32 byte private key")]
        [InlineData(64, "64 byte private key")]
        [InlineData(99, "99 byte private key")]
        public async Task SignAsync_WithInvalidPrivateKeySize_ShouldThrowArgumentException(
            int privateKeySize, string scenario)
        {
            // Arrange
            var privateKey = new byte[privateKeySize];
            var publicKey = new byte[91];

            // Act & Assert
            await _service.Invoking(s => s.SignAsync("test content", privateKey, publicKey))
                .Should().ThrowAsync<ArgumentException>()
                .WithParameterName("privateKey")
                .WithMessage("Private key must be at least 100 bytes for ECDSA P-256 PKCS8 format*");
        }

        [Theory]
        [InlineData(32, "32 byte public key")]
        [InlineData(64, "64 byte public key")]
        [InlineData(79, "79 byte public key")]
        public async Task SignAsync_WithInvalidPublicKeySize_ShouldThrowArgumentException(
            int publicKeySize, string scenario)
        {
            // Arrange
            var privateKey = new byte[138];
            var publicKey = new byte[publicKeySize];

            // Act & Assert
            await _service.Invoking(s => s.SignAsync("test content", privateKey, publicKey))
                .Should().ThrowAsync<ArgumentException>()
                .WithParameterName("publicKey")
                .WithMessage("Public key must be at least 80 bytes for ECDSA P-256 SPKI format*");
        }

        [Fact]
        public async Task SignAsync_WithValidInputs_ShouldReturnSignedTarget()
        {
            // Arrange
            var targetHash = "Hello, World!";
            var privateKey = new byte[138];
            var publicKey = new byte[91];
            var expectedHash = new byte[32];
            var expectedSignature = new byte[64];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.SignECDSAAsync(
                It.IsAny<byte[]>(),
                It.IsAny<byte[]>(),
                "P-256",
                "SHA-256"))
                .ReturnsAsync(expectedSignature);

            // Act
            var result = await _service.SignAsync(targetHash, privateKey, publicKey);

            // Assert
            result.Should().NotBeNull();
            result.TargetHash.Should().BeEquivalentTo(expectedHash);
            result.Signature.Should().BeEquivalentTo(expectedSignature);
            result.PublicKey.Should().BeEquivalentTo(publicKey);
            result.Algorithm.Should().Be("ECDSA-P256");
            result.Version.Should().Be("1.0");
            result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task SignAsync_ShouldHashContentCorrectly()
        {
            // Arrange
            var targetHash = "Test content for hashing";
            var privateKey = new byte[138];
            var publicKey = new byte[91];
            var expectedHash = new byte[32];
            var expectedSignature = new byte[64];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.SignECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(expectedSignature);

            // Act
            await _service.SignAsync(targetHash, privateKey, publicKey);

            // Assert
            _cryptoServiceMock.Verify(c => c.Sha256Async(It.Is<byte[]>(b => Encoding.UTF8.GetString(b) == targetHash)), Times.Once);
        }

        [Fact]
        public async Task SignAsync_WhenHashingFails_ShouldThrowCryptoException()
        {
            // Arrange
            var targetHash = "test content";
            var privateKey = new byte[138];
            var publicKey = new byte[91];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ThrowsAsync(new InvalidOperationException("Hashing failed"));

            // Act & Assert
            await _service.Invoking(s => s.SignAsync(targetHash, privateKey, publicKey))
                .Should().ThrowAsync<CryptoException>()
                .WithMessage("Failed to hash content")
;
        }

        [Fact]
        public async Task SignAsync_WhenHashReturnsNull_ShouldThrowCryptoException()
        {
            // Arrange
            var targetHash = "test content";
            var privateKey = new byte[138];
            var publicKey = new byte[91];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync((byte[])null);

            // Act & Assert
            await _service.Invoking(s => s.SignAsync(targetHash, privateKey, publicKey))
                .Should().ThrowAsync<CryptoException>()
                .WithMessage("Failed to compute SHA-256 hash");
        }

        [Fact]
        public async Task SignAsync_WhenHashReturnsWrongSize_ShouldThrowCryptoException()
        {
            // Arrange
            var targetHash = "test content";
            var privateKey = new byte[138];
            var publicKey = new byte[91];
            var invalidHash = new byte[16]; // Wrong size

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(invalidHash);

            // Act & Assert
            await _service.Invoking(s => s.SignAsync(targetHash, privateKey, publicKey))
                .Should().ThrowAsync<CryptoException>()
                .WithMessage("Failed to compute SHA-256 hash");
        }

        [Fact]
        public async Task SignAsync_WhenSigningFails_ShouldThrowCryptoException()
        {
            // Arrange
            var targetHash = "test content";
            var privateKey = new byte[138];
            var publicKey = new byte[91];
            var expectedHash = new byte[32];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.SignECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Signing failed"));

            // Act & Assert
            await _service.Invoking(s => s.SignAsync(targetHash, privateKey, publicKey))
                .Should().ThrowAsync<CryptoException>()
                .WithMessage("Failed to sign content")
;
        }

        [Fact]
        public async Task SignAsync_WhenSigningReturnsNull_ShouldThrowCryptoException()
        {
            // Arrange
            var targetHash = "test content";
            var privateKey = new byte[138];
            var publicKey = new byte[91];
            var expectedHash = new byte[32];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.SignECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((byte[])null);

            // Act & Assert
            await _service.Invoking(s => s.SignAsync(targetHash, privateKey, publicKey))
                .Should().ThrowAsync<CryptoException>()
                .WithMessage("Invalid signature size. Expected at least 64 bytes");
        }

        [Fact]
        public async Task SignAsync_WhenSigningReturnsWrongSize_ShouldThrowCryptoException()
        {
            // Arrange
            var targetHash = "test content";
            var privateKey = new byte[138];
            var publicKey = new byte[91];
            var expectedHash = new byte[32];
            var invalidSignature = new byte[32]; // Too small

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.SignECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(invalidSignature);

            // Act & Assert
            await _service.Invoking(s => s.SignAsync(targetHash, privateKey, publicKey))
                .Should().ThrowAsync<CryptoException>()
                .WithMessage("Invalid signature size. Expected at least 64 bytes");
        }

        [Fact]
        public async Task SignAsync_WhenUnexpectedExceptionOccurs_ShouldThrowCryptoException()
        {
            // Arrange
            var targetHash = "test content";
            var privateKey = new byte[138];
            var publicKey = new byte[91];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ThrowsAsync(new Exception("Unexpected error"));

            // Act & Assert
            await _service.Invoking(s => s.SignAsync(targetHash, privateKey, publicKey))
                .Should().ThrowAsync<CryptoException>()
                .WithMessage("An unexpected error occurred during signing")
;
        }

        [Theory]
        [InlineData("Simple content", "Simple content signing")]
        [InlineData("Content with special chars: !@#$%^&*()", "Special characters")]
        [InlineData("Multi\nline\ncontent", "Multi-line content")]
        [InlineData("Very long content that exceeds typical message sizes and contains a lot of text to test the hashing and signing process with larger payloads", "Long content")]
        public async Task SignAsync_WithVariousContentTypes_ShouldProcessCorrectly(
            string content, string scenario)
        {
            // Arrange
            var privateKey = new byte[138];
            var publicKey = new byte[91];
            var expectedHash = new byte[32];
            var expectedSignature = new byte[64];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.SignECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(expectedSignature);

            // Act
            var result = await _service.SignAsync(content, privateKey, publicKey);

            // Assert
            result.Should().NotBeNull(scenario);
            result.Algorithm.Should().Be("ECDSA-P256");
            result.Version.Should().Be("1.0");
        }

        #endregion

        #region SignAsync (Ed25519KeyPair) Tests

        [Fact]
        public async Task SignAsync_KeyPair_WithNullKeyPair_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await _service.Invoking(s => s.SignAsync("test content", (Ed25519KeyPair)null))
                .Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("keyPair");
        }

        [Fact]
        public async Task SignAsync_KeyPair_WithValidKeyPair_ShouldCallByteArrayOverload()
        {
            // Arrange
            var targetHash = "test content";
            var keyPair = new Ed25519KeyPair
            {
                PrivateKey = new byte[138],
                PublicKey = new byte[91]
            };
            var expectedHash = new byte[32];
            var expectedSignature = new byte[64];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.SignECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(expectedSignature);

            // Act
            var result = await _service.SignAsync(targetHash, keyPair);

            // Assert
            result.Should().NotBeNull();
            result.PublicKey.Should().BeEquivalentTo(keyPair.PublicKey);
            _cryptoServiceMock.Verify(c => c.SignECDSAAsync(keyPair.PrivateKey, expectedHash, "P-256", "SHA-256"), Times.Once);
        }

        [Fact]
        public async Task SignAsync_KeyPair_WithInvalidKeySizes_ShouldThrowArgumentException()
        {
            // Arrange
            var targetHash = "test content";
            var keyPair = new Ed25519KeyPair
            {
                PrivateKey = new byte[50], // Too small
                PublicKey = new byte[50]   // Too small
            };

            // Act & Assert
            await _service.Invoking(s => s.SignAsync(targetHash, keyPair))
                .Should().ThrowAsync<ArgumentException>()
                .WithParameterName("privateKey");
        }

        #endregion

        #region Edge Cases and Security Tests

        [Fact]
        public async Task SignAsync_WithLargeContent_ShouldProcessCorrectly()
        {
            // Arrange
            var largeContent = new string('A', 10000); // 10KB content
            var privateKey = new byte[138];
            var publicKey = new byte[91];
            var expectedHash = new byte[32];
            var expectedSignature = new byte[64];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.SignECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(expectedSignature);

            // Act
            var result = await _service.SignAsync(largeContent, privateKey, publicKey);

            // Assert
            result.Should().NotBeNull();
            _cryptoServiceMock.Verify(c => c.Sha256Async(It.Is<byte[]>(b => b.Length == 10000)), Times.Once);
        }

        [Fact]
        public async Task SignAsync_WithMinimumValidSizes_ShouldWork()
        {
            // Arrange
            var targetHash = "test";
            var privateKey = new byte[100]; // Minimum valid size
            var publicKey = new byte[80];   // Minimum valid size
            var expectedHash = new byte[32];
            var expectedSignature = new byte[64];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.SignECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(expectedSignature);

            // Act
            var result = await _service.SignAsync(targetHash, privateKey, publicKey);

            // Assert
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task SignAsync_WithMaximumSizes_ShouldWork()
        {
            // Arrange
            var targetHash = "test";
            var privateKey = new byte[2048]; // Large private key
            var publicKey = new byte[1024];  // Large public key
            var expectedHash = new byte[32];
            var expectedSignature = new byte[128]; // Large signature

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.SignECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(expectedSignature);

            // Act
            var result = await _service.SignAsync(targetHash, privateKey, publicKey);

            // Assert
            result.Should().NotBeNull();
            result.Signature.Should().HaveCount(128);
        }

        [Fact]
        public async Task SignAsync_WithUnicodeContent_ShouldProcessCorrectly()
        {
            // Arrange
            var unicodeContent = "Hello 世界 🌍 Emoji test ñáéíóú";
            var privateKey = new byte[138];
            var publicKey = new byte[91];
            var expectedHash = new byte[32];
            var expectedSignature = new byte[64];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.SignECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(expectedSignature);

            // Act
            var result = await _service.SignAsync(unicodeContent, privateKey, publicKey);

            // Assert
            result.Should().NotBeNull();
            _cryptoServiceMock.Verify(c => c.Sha256Async(It.Is<byte[]>(b => Encoding.UTF8.GetString(b) == unicodeContent)), Times.Once);
        }

        [Fact]
        public async Task SignAsync_ShouldNotLeakArgumentExceptions()
        {
            // Arrange - Test that ArgumentExceptions are not caught by the general exception handler
            var privateKey = new byte[138];
            var publicKey = new byte[91];

            // Act & Assert - Should throw ArgumentNullException, not CryptoException
            var exception = await _service.Invoking(s => s.SignAsync(null, privateKey, publicKey))
                .Should().ThrowAsync<ArgumentNullException>();
            exception.Which.Should().BeOfType<ArgumentNullException>();
        }

        [Fact]
        public async Task SignAsync_ShouldUseCorrectECDSAParameters()
        {
            // Arrange
            var targetHash = "test content";
            var privateKey = new byte[138];
            var publicKey = new byte[91];
            var expectedHash = new byte[32];
            var expectedSignature = new byte[64];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.SignECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(expectedSignature);

            // Act
            await _service.SignAsync(targetHash, privateKey, publicKey);

            // Assert
            _cryptoServiceMock.Verify(c => c.SignECDSAAsync(privateKey, expectedHash, "P-256", "SHA-256"), Times.Once);
        }

        #endregion

        #region SignedTarget Properties Tests

        [Fact]
        public async Task SignAsync_ShouldCreateSignedTargetWithCorrectTimestamp()
        {
            // Arrange
            var targetHash = "test content";
            var privateKey = new byte[138];
            var publicKey = new byte[91];
            var expectedHash = new byte[32];
            var expectedSignature = new byte[64];

            var beforeSigning = DateTime.UtcNow;

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.SignECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(expectedSignature);

            // Act
            var result = await _service.SignAsync(targetHash, privateKey, publicKey);

            // Assert
            var afterSigning = DateTime.UtcNow;
            result.Timestamp.Should().BeOnOrAfter(beforeSigning);
            result.Timestamp.Should().BeOnOrBefore(afterSigning);
        }

        [Fact]
        public async Task SignAsync_ShouldCreateSignedTargetWithCorrectMetadata()
        {
            // Arrange
            var targetHash = "test content";
            var privateKey = new byte[138];
            var publicKey = new byte[91];
            var expectedHash = new byte[32];
            var expectedSignature = new byte[64];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.SignECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(expectedSignature);

            // Act
            var result = await _service.SignAsync(targetHash, privateKey, publicKey);

            // Assert
            result.Algorithm.Should().Be("ECDSA-P256");
            result.Version.Should().Be("1.0");
            result.TargetHash.Should().NotBeNull();
            result.Signature.Should().NotBeNull();
            result.PublicKey.Should().NotBeNull();
        }

        [Fact]
        public async Task SignAsync_ResultShouldBeConvertibleToBase64()
        {
            // Arrange
            var targetHash = "test content";
            var privateKey = new byte[138];
            var publicKey = new byte[91] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91 };
            var expectedHash = new byte[32] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 };
            var expectedSignature = new byte[64];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.SignECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(expectedSignature);

            // Act
            var result = await _service.SignAsync(targetHash, privateKey, publicKey);
            var base64Result = result.ToBase64();

            // Assert
            base64Result.Should().NotBeNull();
            base64Result.TargetHashBase64.Should().Be(Convert.ToBase64String(expectedHash));
            base64Result.SignatureBase64.Should().Be(Convert.ToBase64String(expectedSignature));
            base64Result.PublicKeyBase64.Should().Be(Convert.ToBase64String(publicKey));
            base64Result.Algorithm.Should().Be("ECDSA-P256");
            base64Result.Version.Should().Be("1.0");
            base64Result.Timestamp.Should().Be(result.Timestamp);
        }

        #endregion

        #region Logging Tests

        [Fact]
        public async Task SignAsync_WhenSuccessful_ShouldLogSuccess()
        {
            // Arrange
            var targetHash = "test content";
            var privateKey = new byte[138];
            var publicKey = new byte[91];
            var expectedHash = new byte[32];
            var expectedSignature = new byte[64];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.SignECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(expectedSignature);

            // Act
            await _service.SignAsync(targetHash, privateKey, publicKey);

            // Assert
            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Content successfully signed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SignAsync_ShouldLogDebugMessages()
        {
            // Arrange
            var targetHash = "test content";
            var privateKey = new byte[138];
            var publicKey = new byte[91];
            var expectedHash = new byte[32];
            var expectedSignature = new byte[64];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.SignECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(expectedSignature);

            // Act
            await _service.SignAsync(targetHash, privateKey, publicKey);

            // Assert
            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Starting content signing process")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);

            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Computing SHA-256 hash")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);

            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Signing content hash with ECDSA P-256")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SignAsync_WhenExceptionOccurs_ShouldLogError()
        {
            // Arrange
            var targetHash = "test content";
            var privateKey = new byte[138];
            var publicKey = new byte[91];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ThrowsAsync(new InvalidOperationException("Test exception"));

            // Act & Assert
            await _service.Invoking(s => s.SignAsync(targetHash, privateKey, publicKey))
                .Should().ThrowAsync<CryptoException>();

            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Cryptographic operation failed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        #endregion

        #region Error Message Classification Tests

        [Theory]
        [InlineData("hash", "Failed to hash content")]
        [InlineData("Hash", "Failed to hash content")]
        [InlineData("HASH", "Failed to hash content")]
        [InlineData("sign", "Failed to sign content")]
        [InlineData("Sign", "Failed to sign content")]
        [InlineData("SIGN", "Failed to sign content")]
        [InlineData("signing", "Failed to sign content")]
        [InlineData("other error", "Cryptographic operation failed")]
        public async Task SignAsync_ShouldClassifyExceptionsCorrectly(string errorMessage, string expectedMessage)
        {
            // Arrange
            var targetHash = "test content";
            var privateKey = new byte[138];
            var publicKey = new byte[91];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ThrowsAsync(new InvalidOperationException(errorMessage));

            // Act & Assert
            var exception = await _service.Invoking(s => s.SignAsync(targetHash, privateKey, publicKey))
                .Should().ThrowAsync<CryptoException>();

            exception.Which.Message.Should().Contain(expectedMessage);
        }

        #endregion

        #region Performance Tests

        [Fact]
        public async Task SignAsync_WithMultipleOperations_ShouldMaintainPerformance()
        {
            // Arrange
            var targetHash = "test content";
            var privateKey = new byte[138];
            var publicKey = new byte[91];
            var expectedHash = new byte[32];
            var expectedSignature = new byte[64];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.SignECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(expectedSignature);

            // Act
            var tasks = new Task[10];
            for (int i = 0; i < 10; i++)
            {
                tasks[i] = _service.SignAsync($"{targetHash}_{i}", privateKey, publicKey);
            }

            await Task.WhenAll(tasks);

            // Assert - Should complete without timeout
            tasks.Should().OnlyContain(t => t.IsCompletedSuccessfully);
        }

        #endregion
    }
}