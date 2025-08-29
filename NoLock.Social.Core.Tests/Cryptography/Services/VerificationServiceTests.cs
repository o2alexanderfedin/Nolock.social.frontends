using System;
using System.Linq;
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
    public class VerificationServiceTests
    {
        private readonly Mock<IWebCryptoService> _cryptoServiceMock;
        private readonly Mock<ILogger<VerificationService>> _loggerMock;
        private readonly VerificationService _service;

        public VerificationServiceTests()
        {
            _cryptoServiceMock = new Mock<IWebCryptoService>();
            _loggerMock = new Mock<ILogger<VerificationService>>();

            _service = new VerificationService(_cryptoServiceMock.Object, _loggerMock.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidDependencies_ShouldInitialize()
        {
            // Arrange & Act
            var service = new VerificationService(_cryptoServiceMock.Object, _loggerMock.Object);

            // Assert
            service.Should().NotBeNull();
            service.Should().BeAssignableTo<IVerificationService>();
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
            var action = () => new VerificationService(cryptoService, logger);
            action.Should().Throw<ArgumentNullException>();
        }

        #endregion

        #region VerifySignatureAsync (byte arrays) Tests

        [Fact]
        public async Task VerifySignatureAsync_WithNullContent_ShouldThrowArgumentNullException()
        {
            // Arrange
            var signature = new byte[64];
            var publicKey = new byte[91];

            // Act & Assert
            await _service.Invoking(s => s.VerifySignatureAsync(null, signature, publicKey))
                .Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("content");
        }

        [Fact]
        public async Task VerifySignatureAsync_WithEmptyContent_ShouldThrowArgumentException()
        {
            // Arrange
            var signature = new byte[64];
            var publicKey = new byte[91];

            // Act & Assert
            await _service.Invoking(s => s.VerifySignatureAsync("", signature, publicKey))
                .Should().ThrowAsync<ArgumentException>()
                .WithParameterName("content")
                .WithMessage("Content cannot be empty*");
        }

        [Fact]
        public async Task VerifySignatureAsync_WithNullSignature_ShouldThrowArgumentNullException()
        {
            // Arrange
            var publicKey = new byte[91];

            // Act & Assert
            await _service.Invoking(s => s.VerifySignatureAsync("test content", null, publicKey))
                .Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("signature");
        }

        [Fact]
        public async Task VerifySignatureAsync_WithNullPublicKey_ShouldThrowArgumentNullException()
        {
            // Arrange
            var signature = new byte[64];

            // Act & Assert
            await _service.Invoking(s => s.VerifySignatureAsync("test content", signature, null))
                .Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("publicKey");
        }

        [Theory]
        [InlineData(32, "32 byte signature")]
        [InlineData(48, "48 byte signature")]
        [InlineData(63, "63 byte signature")]
        public async Task VerifySignatureAsync_WithInvalidSignatureSize_ShouldThrowArgumentException(
            int signatureSize, string scenario)
        {
            // Arrange
            var signature = new byte[signatureSize];
            var publicKey = new byte[91];

            // Act & Assert
            await _service.Invoking(s => s.VerifySignatureAsync("test content", signature, publicKey))
                .Should().ThrowAsync<ArgumentException>()
                .WithParameterName("signature")
                .WithMessage("Signature must be at least 64 bytes for ECDSA P-256*");
        }

        [Theory]
        [InlineData(32, "32 byte public key")]
        [InlineData(64, "64 byte public key")]
        [InlineData(79, "79 byte public key")]
        public async Task VerifySignatureAsync_WithInvalidPublicKeySize_ShouldThrowArgumentException(
            int publicKeySize, string scenario)
        {
            // Arrange
            var signature = new byte[64];
            var publicKey = new byte[publicKeySize];

            // Act & Assert
            await _service.Invoking(s => s.VerifySignatureAsync("test content", signature, publicKey))
                .Should().ThrowAsync<ArgumentException>()
                .WithParameterName("publicKey")
                .WithMessage("Public key must be at least 80 bytes for ECDSA P-256 SPKI format*");
        }

        [Theory]
        [InlineData(true, "Valid signature")]
        [InlineData(false, "Invalid signature")]
        public async Task VerifySignatureAsync_WithValidInputs_ShouldReturnExpectedResult(
            bool expectedResult, string scenario)
        {
            // Arrange
            var content = "Hello, World!";
            var signature = new byte[64];
            var publicKey = new byte[91];
            var expectedHash = new byte[32];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.VerifyECDSAAsync(
                It.IsAny<byte[]>(), 
                It.IsAny<byte[]>(), 
                It.IsAny<byte[]>(), 
                "P-256", 
                "SHA-256"))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _service.VerifySignatureAsync(content, signature, publicKey);

            // Assert
            result.Should().Be(expectedResult, scenario);
        }

        [Fact]
        public async Task VerifySignatureAsync_ShouldHashContentCorrectly()
        {
            // Arrange
            var content = "Test content for hashing";
            var signature = new byte[64];
            var publicKey = new byte[91];
            var expectedHash = new byte[32];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.VerifyECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            await _service.VerifySignatureAsync(content, signature, publicKey);

            // Assert
            _cryptoServiceMock.Verify(c => c.Sha256Async(It.Is<byte[]>(b => Encoding.UTF8.GetString(b) == content)), Times.Once);
        }

        [Fact]
        public async Task VerifySignatureAsync_WhenHashingFails_ShouldThrowCryptoException()
        {
            // Arrange
            var content = "test content";
            var signature = new byte[64];
            var publicKey = new byte[91];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ThrowsAsync(new InvalidOperationException("Hashing failed"));

            // Act & Assert
            await _service.Invoking(s => s.VerifySignatureAsync(content, signature, publicKey))
                .Should().ThrowAsync<CryptoException>()
                .WithMessage("Failed to hash content during verification")
;
        }

        [Fact]
        public async Task VerifySignatureAsync_WhenHashReturnsNull_ShouldThrowCryptoException()
        {
            // Arrange
            var content = "test content";
            var signature = new byte[64];
            var publicKey = new byte[91];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync((byte[])null);

            // Act & Assert
            await _service.Invoking(s => s.VerifySignatureAsync(content, signature, publicKey))
                .Should().ThrowAsync<CryptoException>()
                .WithMessage("Failed to compute SHA-256 hash");
        }

        [Fact]
        public async Task VerifySignatureAsync_WhenHashReturnsWrongSize_ShouldThrowCryptoException()
        {
            // Arrange
            var content = "test content";
            var signature = new byte[64];
            var publicKey = new byte[91];
            var invalidHash = new byte[16]; // Wrong size

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(invalidHash);

            // Act & Assert
            await _service.Invoking(s => s.VerifySignatureAsync(content, signature, publicKey))
                .Should().ThrowAsync<CryptoException>()
                .WithMessage("Failed to compute SHA-256 hash");
        }

        [Fact]
        public async Task VerifySignatureAsync_WhenVerificationFails_ShouldThrowCryptoException()
        {
            // Arrange
            var content = "test content";
            var signature = new byte[64];
            var publicKey = new byte[91];
            var expectedHash = new byte[32];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.VerifyECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Verification failed"));

            // Act & Assert
            await _service.Invoking(s => s.VerifySignatureAsync(content, signature, publicKey))
                .Should().ThrowAsync<CryptoException>()
                .WithMessage("Failed to perform signature verification")
;
        }

        [Fact]
        public async Task VerifySignatureAsync_WhenUnexpectedExceptionOccurs_ShouldThrowCryptoException()
        {
            // Arrange
            var content = "test content";
            var signature = new byte[64];
            var publicKey = new byte[91];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ThrowsAsync(new Exception("Unexpected error"));

            // Act & Assert
            await _service.Invoking(s => s.VerifySignatureAsync(content, signature, publicKey))
                .Should().ThrowAsync<CryptoException>()
                .WithMessage("An unexpected error occurred during verification")
;
        }

        [Theory]
        [InlineData("Simple content", "Simple content verification")]
        [InlineData("Content with special chars: !@#$%^&*()", "Special characters")]
        [InlineData("Multi\nline\ncontent", "Multi-line content")]
        [InlineData("Very long content that exceeds typical message sizes and contains a lot of text to test the hashing and verification process with larger payloads", "Long content")]
        [InlineData("", "Empty content should not reach here due to validation")]
        public async Task VerifySignatureAsync_WithVariousContentTypes_ShouldProcessCorrectly(
            string content, string scenario)
        {
            // Arrange
            var signature = new byte[64];
            var publicKey = new byte[91];
            var expectedHash = new byte[32];

            if (string.IsNullOrEmpty(content))
            {
                // This should be caught by validation
                await _service.Invoking(s => s.VerifySignatureAsync(content, signature, publicKey))
                    .Should().ThrowAsync<ArgumentException>();
                return;
            }

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.VerifyECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.VerifySignatureAsync(content, signature, publicKey);

            // Assert
            result.Should().BeTrue(scenario);
        }

        #endregion

        #region VerifySignatureAsync (base64 strings) Tests

        [Theory]
        [InlineData(null, "validBase64Sig", "publicKeyBase64", "content", "Null content")]
        [InlineData("", "validBase64Sig", "publicKeyBase64", "content", "Empty content")]
        [InlineData("content", null, "publicKeyBase64", "signatureBase64", "Null signature base64")]
        [InlineData("content", "", "publicKeyBase64", "signatureBase64", "Empty signature base64")]
        [InlineData("content", "validBase64Sig", null, "publicKeyBase64", "Null public key base64")]
        [InlineData("content", "validBase64Sig", "", "publicKeyBase64", "Empty public key base64")]
        public async Task VerifySignatureAsync_Base64_WithInvalidInputs_ShouldThrowArgumentException(
            string content, string signatureBase64, string publicKeyBase64, string paramName, string scenario)
        {
            // Arrange
            var validContent = content ?? "default";
            var validSig = signatureBase64 ?? Convert.ToBase64String(new byte[64]);
            var validKey = publicKeyBase64 ?? Convert.ToBase64String(new byte[91]);

            if (content == null)
            {
                // Act & Assert
                await _service.Invoking(s => s.VerifySignatureAsync(null, validSig, validKey))
                    .Should().ThrowAsync<ArgumentNullException>()
                    .WithParameterName("content");
                return;
            }

            // Act & Assert
            await _service.Invoking(s => s.VerifySignatureAsync(validContent, signatureBase64, publicKeyBase64))
                .Should().ThrowAsync<ArgumentException>()
                .WithParameterName(paramName);
        }

        [Fact]
        public async Task VerifySignatureAsync_Base64_WithValidInputs_ShouldDecodeAndVerify()
        {
            // Arrange
            var content = "test content";
            var signature = new byte[64];
            var publicKey = new byte[91];
            var signatureBase64 = Convert.ToBase64String(signature);
            var publicKeyBase64 = Convert.ToBase64String(publicKey);
            var expectedHash = new byte[32];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.VerifyECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.VerifySignatureAsync(content, signatureBase64, publicKeyBase64);

            // Assert
            result.Should().BeTrue();
            _cryptoServiceMock.Verify(c => c.VerifyECDSAAsync(publicKey, signature, expectedHash, "P-256", "SHA-256"), Times.Once);
        }

        [Theory]
        [InlineData("invalid base64!", "Invalid signature base64")]
        [InlineData("validBase64==", "Valid signature, invalid public key")]
        public async Task VerifySignatureAsync_Base64_WithInvalidBase64_ShouldThrowFormatException(
            string invalidBase64, string scenario)
        {
            // Arrange
            var content = "test content";
            var validSignature = Convert.ToBase64String(new byte[64]);
            var validPublicKey = Convert.ToBase64String(new byte[91]);

            string signatureBase64, publicKeyBase64;
            if (scenario.Contains("signature"))
            {
                signatureBase64 = invalidBase64;
                publicKeyBase64 = validPublicKey;
            }
            else
            {
                signatureBase64 = validSignature;
                publicKeyBase64 = invalidBase64;
            }

            // Act & Assert
            await _service.Invoking(s => s.VerifySignatureAsync(content, signatureBase64, publicKeyBase64))
                .Should().ThrowAsync<FormatException>()
                .WithMessage("Invalid base64 format in signature or public key*");
        }

        [Fact]
        public async Task VerifySignatureAsync_Base64_WhenDecodedSizesInvalid_ShouldThrowArgumentException()
        {
            // Arrange
            var content = "test content";
            var smallSignature = new byte[32]; // Too small
            var smallPublicKey = new byte[32]; // Too small
            var signatureBase64 = Convert.ToBase64String(smallSignature);
            var publicKeyBase64 = Convert.ToBase64String(smallPublicKey);

            // Act & Assert
            await _service.Invoking(s => s.VerifySignatureAsync(content, signatureBase64, publicKeyBase64))
                .Should().ThrowAsync<ArgumentException>()
                .WithParameterName("signature");
        }

        #endregion

        #region VerifySignedContentAsync Tests

        [Fact]
        public async Task VerifySignedContentAsync_WithNullSignedTarget_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await _service.Invoking(s => s.VerifySignedContentAsync(null))
                .Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("signedTarget");
        }

        [Theory]
        [InlineData("Ed25519", "Unsupported Ed25519 algorithm")]
        [InlineData("RSA-PSS", "Unsupported RSA-PSS algorithm")]
        [InlineData("HMAC-SHA256", "Unsupported HMAC algorithm")]
        [InlineData("", "Empty algorithm")]
        [InlineData(null, "Null algorithm")]
        public async Task VerifySignedContentAsync_WithUnsupportedAlgorithm_ShouldThrowNotSupportedException(
            string algorithm, string scenario)
        {
            // Arrange
            var signedTarget = new SignedTarget
            {
                Algorithm = algorithm,
                Version = "1.0"
            };

            // Act & Assert
            await _service.Invoking(s => s.VerifySignedContentAsync(signedTarget))
                .Should().ThrowAsync<NotSupportedException>()
                .WithMessage($"Algorithm '{algorithm}' is not supported. Only ECDSA-P256 is supported.");
        }

        [Theory]
        [InlineData("1.1", "Unsupported version 1.1")]
        [InlineData("2.0", "Unsupported version 2.0")]
        [InlineData("", "Empty version")]
        [InlineData(null, "Null version")]
        public async Task VerifySignedContentAsync_WithUnsupportedVersion_ShouldThrowNotSupportedException(
            string version, string scenario)
        {
            // Arrange
            var signedTarget = new SignedTarget
            {
                Algorithm = "ECDSA-P256",
                Version = version
            };

            // Act & Assert
            await _service.Invoking(s => s.VerifySignedContentAsync(signedTarget))
                .Should().ThrowAsync<NotSupportedException>()
                .WithMessage($"Version '{version}' is not supported. Only version 1.0 is supported.");
        }

        [Theory]
        [InlineData(true, "Valid signed content")]
        [InlineData(false, "Invalid signed content")]
        public async Task VerifySignedContentAsync_WithValidSignedTarget_ShouldReturnExpectedResult(
            bool expectedResult, string scenario)
        {
            // Arrange
            var signedTarget = new SignedTarget
            {
                Algorithm = "ECDSA-P256",
                Version = "1.0",
                TargetHash = new byte[32],
                Signature = new byte[64],
                PublicKey = new byte[91],
                Timestamp = DateTime.UtcNow
            };

            _cryptoServiceMock.Setup(c => c.VerifyECDSAAsync(
                signedTarget.PublicKey,
                signedTarget.Signature,
                signedTarget.TargetHash,
                "P-256",
                "SHA-256"))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _service.VerifySignedContentAsync(signedTarget);

            // Assert
            result.Should().Be(expectedResult, scenario);
        }

        [Fact]
        public async Task VerifySignedContentAsync_ShouldVerifyAgainstTargetHashDirectly()
        {
            // Arrange
            var targetHash = new byte[32] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 };
            var signature = new byte[64];
            var publicKey = new byte[91];

            var signedTarget = new SignedTarget
            {
                Algorithm = "ECDSA-P256",
                Version = "1.0",
                TargetHash = targetHash,
                Signature = signature,
                PublicKey = publicKey
            };

            _cryptoServiceMock.Setup(c => c.VerifyECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            await _service.VerifySignedContentAsync(signedTarget);

            // Assert
            _cryptoServiceMock.Verify(c => c.VerifyECDSAAsync(publicKey, signature, targetHash, "P-256", "SHA-256"), Times.Once);
        }

        [Fact]
        public async Task VerifySignedContentAsync_WhenVerificationFails_ShouldThrowCryptoException()
        {
            // Arrange
            var signedTarget = new SignedTarget
            {
                Algorithm = "ECDSA-P256",
                Version = "1.0",
                TargetHash = new byte[32],
                Signature = new byte[64],
                PublicKey = new byte[91]
            };

            _cryptoServiceMock.Setup(c => c.VerifyECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Verification error"));

            // Act & Assert
            await _service.Invoking(s => s.VerifySignedContentAsync(signedTarget))
                .Should().ThrowAsync<CryptoException>()
                .WithMessage("Failed to verify signed target")
;
        }

        #endregion

        #region Edge Cases and Security Tests

        [Fact]
        public async Task VerifySignatureAsync_WithLargeContent_ShouldProcessCorrectly()
        {
            // Arrange
            var largeContent = new string('A', 10000); // 10KB content
            var signature = new byte[64];
            var publicKey = new byte[91];
            var expectedHash = new byte[32];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.VerifyECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.VerifySignatureAsync(largeContent, signature, publicKey);

            // Assert
            result.Should().BeTrue();
            _cryptoServiceMock.Verify(c => c.Sha256Async(It.Is<byte[]>(b => b.Length == 10000)), Times.Once);
        }

        [Fact]
        public async Task VerifySignatureAsync_WithMinimumValidSizes_ShouldWork()
        {
            // Arrange
            var content = "test";
            var signature = new byte[64]; // Minimum valid size
            var publicKey = new byte[80]; // Minimum valid size
            var expectedHash = new byte[32];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.VerifyECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.VerifySignatureAsync(content, signature, publicKey);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task VerifySignatureAsync_WithMaximumSizes_ShouldWork()
        {
            // Arrange
            var content = "test";
            var signature = new byte[1024]; // Large signature
            var publicKey = new byte[2048]; // Large public key
            var expectedHash = new byte[32];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.VerifyECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.VerifySignatureAsync(content, signature, publicKey);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task VerifySignatureAsync_WithUnicodeContent_ShouldProcessCorrectly()
        {
            // Arrange
            var unicodeContent = "Hello 世界 🌍 Emoji test ñáéíóú";
            var signature = new byte[64];
            var publicKey = new byte[91];
            var expectedHash = new byte[32];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.VerifyECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.VerifySignatureAsync(unicodeContent, signature, publicKey);

            // Assert
            result.Should().BeTrue();
            _cryptoServiceMock.Verify(c => c.Sha256Async(It.Is<byte[]>(b => Encoding.UTF8.GetString(b) == unicodeContent)), Times.Once);
        }

        [Fact]
        public async Task VerifySignatureAsync_ShouldNotLeakArgumentExceptions()
        {
            // Arrange - Test that ArgumentExceptions are not caught by the general exception handler
            var content = "test";
            var signature = new byte[64];
            var publicKey = new byte[91];

            // Act & Assert - Should throw ArgumentNullException, not CryptoException
            var exception = await _service.Invoking(s => s.VerifySignatureAsync(null, signature, publicKey))
                .Should().ThrowAsync<ArgumentNullException>();
            exception.Which.Should().BeOfType<ArgumentNullException>();
        }

        #endregion

        #region Logging Tests

        [Fact]
        public async Task VerifySignatureAsync_WhenSuccessful_ShouldLogSuccess()
        {
            // Arrange
            var content = "test content";
            var signature = new byte[64];
            var publicKey = new byte[91];
            var expectedHash = new byte[32];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.VerifyECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            await _service.VerifySignatureAsync(content, signature, publicKey);

            // Assert
            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Signature verification successful")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task VerifySignatureAsync_WhenFailed_ShouldLogWarning()
        {
            // Arrange
            var content = "test content";
            var signature = new byte[64];
            var publicKey = new byte[91];
            var expectedHash = new byte[32];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.VerifyECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(false);

            // Act
            await _service.VerifySignatureAsync(content, signature, publicKey);

            // Assert
            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Signature verification failed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task VerifySignatureAsync_WhenExceptionOccurs_ShouldLogError()
        {
            // Arrange
            var content = "test content";
            var signature = new byte[64];
            var publicKey = new byte[91];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ThrowsAsync(new InvalidOperationException("Test exception"));

            // Act & Assert
            await _service.Invoking(s => s.VerifySignatureAsync(content, signature, publicKey))
                .Should().ThrowAsync<CryptoException>();

            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Cryptographic operation failed during verification")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task VerifySignedContentAsync_WhenSuccessful_ShouldLogDebugAndInfo()
        {
            // Arrange
            var signedTarget = new SignedTarget
            {
                Algorithm = "ECDSA-P256",
                Version = "1.0",
                TargetHash = new byte[32],
                Signature = new byte[64],
                PublicKey = new byte[91]
            };

            _cryptoServiceMock.Setup(c => c.VerifyECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            await _service.VerifySignedContentAsync(signedTarget);

            // Assert
            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Verifying signed content with algorithm")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);

            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Signature verification successful")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task VerifySignedContentAsync_WhenFailed_ShouldLogDebugAndWarning()
        {
            // Arrange
            var signedTarget = new SignedTarget
            {
                Algorithm = "ECDSA-P256",
                Version = "1.0",
                TargetHash = new byte[32],
                Signature = new byte[64],
                PublicKey = new byte[91]
            };

            _cryptoServiceMock.Setup(c => c.VerifyECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(false);

            // Act
            await _service.VerifySignedContentAsync(signedTarget);

            // Assert
            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Verifying signed content with algorithm")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);

            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Signature verification failed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task VerifySignatureAsync_ShouldLogDebugMessages()
        {
            // Arrange
            var content = "test content";
            var signature = new byte[64];
            var publicKey = new byte[91];
            var expectedHash = new byte[32];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.VerifyECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            await _service.VerifySignatureAsync(content, signature, publicKey);

            // Assert
            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Starting signature verification")),
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
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Verifying ECDSA P-256 signature")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task VerifySignedContentAsync_ShouldLogDetailedDebugForSignedTarget()
        {
            // Arrange
            var signedTarget = new SignedTarget
            {
                Algorithm = "ECDSA-P256",
                Version = "1.0",
                TargetHash = new byte[32],
                Signature = new byte[64],
                PublicKey = new byte[91]
            };

            _cryptoServiceMock.Setup(c => c.VerifyECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            await _service.VerifySignedContentAsync(signedTarget);

            // Assert
            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Verifying ECDSA P-256 signature against target hash")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        #endregion

        #region Timeout and Cancellation Tests

        [Fact]
        public async Task VerifySignatureAsync_WhenCryptoServiceDelays_ShouldNotTimeout()
        {
            // Arrange
            var content = "test content";
            var signature = new byte[64];
            var publicKey = new byte[91];
            var expectedHash = new byte[32];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .Returns(async () =>
                {
                    await Task.Delay(100); // Simulate small delay
                    return expectedHash;
                });

            _cryptoServiceMock.Setup(c => c.VerifyECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(async () =>
                {
                    await Task.Delay(100); // Simulate small delay
                    return true;
                });

            // Act
            var result = await _service.VerifySignatureAsync(content, signature, publicKey);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task VerifySignatureAsync_WithSlowCryptoOperations_ShouldEventuallyComplete()
        {
            // Arrange
            var content = "test content";
            var signature = new byte[64];
            var publicKey = new byte[91];
            var expectedHash = new byte[32];

            // Setup slower operations
            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .Returns(async () =>
                {
                    await Task.Delay(50);
                    return expectedHash;
                });

            _cryptoServiceMock.Setup(c => c.VerifyECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(async () =>
                {
                    await Task.Delay(50);
                    return false;
                });

            // Act & Assert
            var result = await _service.VerifySignatureAsync(content, signature, publicKey);
            result.Should().BeFalse();
        }

        #endregion

        #region Additional Security Tests

        [Fact]
        public async Task VerifySignatureAsync_WithDifferentKeySizesAboveMinimum_ShouldSucceed()
        {
            // Arrange & Act & Assert
            var testCases = new[]
            {
                new { SignatureSize = 64, PublicKeySize = 80, Description = "Minimum sizes" },
                new { SignatureSize = 64, PublicKeySize = 91, Description = "Standard ECDSA sizes" },
                new { SignatureSize = 128, PublicKeySize = 150, Description = "Larger sizes" },
                new { SignatureSize = 256, PublicKeySize = 256, Description = "Very large sizes" }
            };

            foreach (var testCase in testCases)
            {
                // Arrange
                var content = "test content";
                var signature = new byte[testCase.SignatureSize];
                var publicKey = new byte[testCase.PublicKeySize];
                var expectedHash = new byte[32];

                _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                    .ReturnsAsync(expectedHash);

                _cryptoServiceMock.Setup(c => c.VerifyECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(true);

                // Act
                var result = await _service.VerifySignatureAsync(content, signature, publicKey);

                // Assert
                result.Should().BeTrue(testCase.Description);
            }
        }

        [Fact]
        public async Task VerifySignatureAsync_WithZeroFilledSignatureAndKey_ShouldProcessCorrectly()
        {
            // Arrange
            var content = "test content";
            var signature = new byte[64]; // All zeros
            var publicKey = new byte[91]; // All zeros
            var expectedHash = new byte[32];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.VerifyECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(false); // Zero-filled signature should not verify

            // Act
            var result = await _service.VerifySignatureAsync(content, signature, publicKey);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task VerifySignatureAsync_WithMaxFilledSignatureAndKey_ShouldProcessCorrectly()
        {
            // Arrange
            var content = "test content";
            var signature = Enumerable.Range(0, 64).Select(i => (byte)255).ToArray(); // All 0xFF
            var publicKey = Enumerable.Range(0, 91).Select(i => (byte)255).ToArray(); // All 0xFF
            var expectedHash = new byte[32];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.VerifyECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(false); // Max-filled signature should likely not verify for arbitrary content

            // Act
            var result = await _service.VerifySignatureAsync(content, signature, publicKey);

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData("Content with null bytes\0in\0middle", "Content with null bytes")]
        [InlineData("Content\twith\ttabs", "Content with tabs")]
        [InlineData("Content\nwith\nnewlines\r\n", "Content with various newlines")]
        [InlineData("Content with very long repeated text: " + 
                    "This is a very long string that repeats multiple times to test handling of repetitive content patterns. " +
                    "This is a very long string that repeats multiple times to test handling of repetitive content patterns. " +
                    "This is a very long string that repeats multiple times to test handling of repetitive content patterns.", 
                    "Content with repetitive patterns")]
        public async Task VerifySignatureAsync_WithSpecialContentPatterns_ShouldProcessCorrectly(
            string content, string scenario)
        {
            // Arrange
            var signature = new byte[64];
            var publicKey = new byte[91];
            var expectedHash = new byte[32];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.VerifyECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.VerifySignatureAsync(content, signature, publicKey);

            // Assert
            result.Should().BeTrue(scenario);
            _cryptoServiceMock.Verify(c => c.Sha256Async(It.Is<byte[]>(b => Encoding.UTF8.GetString(b) == content)), Times.Once);
        }

        #endregion

        #region Error Message Classification Tests

        [Theory]
        [InlineData("hash", "Failed to hash content during verification")]
        [InlineData("Hash", "Failed to hash content during verification")]
        [InlineData("HASH", "Failed to hash content during verification")]
        [InlineData("verif", "Failed to perform signature verification")]
        [InlineData("Verif", "Failed to perform signature verification")]
        [InlineData("VERIF", "Failed to perform signature verification")]
        [InlineData("verification", "Failed to perform signature verification")]
        [InlineData("other error", "Cryptographic operation failed during verification")]
        public async Task VerifySignatureAsync_ShouldClassifyExceptionsCorrectly(string errorMessage, string expectedMessage)
        {
            // Arrange
            var content = "test content";
            var signature = new byte[64];
            var publicKey = new byte[91];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ThrowsAsync(new InvalidOperationException(errorMessage));

            // Act & Assert
            var exception = await _service.Invoking(s => s.VerifySignatureAsync(content, signature, publicKey))
                .Should().ThrowAsync<CryptoException>();

            exception.Which.Message.Should().Contain(expectedMessage);
        }

        #endregion

        #region Performance Tests

        [Fact]
        public async Task VerifySignatureAsync_WithMultipleOperations_ShouldMaintainPerformance()
        {
            // Arrange
            var content = "test content";
            var signature = new byte[64];
            var publicKey = new byte[91];
            var expectedHash = new byte[32];

            _cryptoServiceMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            _cryptoServiceMock.Setup(c => c.VerifyECDSAAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var tasks = new Task<bool>[10];
            for (int i = 0; i < 10; i++)
            {
                tasks[i] = _service.VerifySignatureAsync($"{content}_{i}", signature, publicKey);
            }

            var results = await Task.WhenAll(tasks);

            // Assert - Should complete without timeout
            results.Should().OnlyContain(r => r == true);
            tasks.Should().OnlyContain(t => t.IsCompletedSuccessfully);
        }

        #endregion
    }
}