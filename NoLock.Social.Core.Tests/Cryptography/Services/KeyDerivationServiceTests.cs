using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Core.Cryptography.Interfaces;
using NoLock.Social.Core.Cryptography.Services;
using Xunit;
using System.Threading;

namespace NoLock.Social.Core.Tests.Cryptography.Services
{
    public class KeyDerivationServiceTests
    {
        private readonly Mock<IWebCryptoService> _webCryptoMock;
        private readonly Mock<ISecureMemoryManager> _secureMemoryManagerMock;
        private readonly Mock<ILogger<KeyDerivationService>> _loggerMock;
        private readonly KeyDerivationService _service;

        public KeyDerivationServiceTests()
        {
            _webCryptoMock = new Mock<IWebCryptoService>();
            _secureMemoryManagerMock = new Mock<ISecureMemoryManager>();
            _loggerMock = new Mock<ILogger<KeyDerivationService>>();

            _service = new KeyDerivationService(_webCryptoMock.Object, _secureMemoryManagerMock.Object, _loggerMock.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidDependencies_ShouldInitialize()
        {
            // Arrange & Act
            var service = new KeyDerivationService(_webCryptoMock.Object, _secureMemoryManagerMock.Object, _loggerMock.Object);

            // Assert
            service.Should().NotBeNull();
            service.Should().BeAssignableTo<IKeyDerivationService>();
        }

        [Theory]
        [InlineData(null, "webCrypto", "Null web crypto service")]
        [InlineData("secureMemoryManager", null, "Null secure memory manager")]
        public void Constructor_WithNullDependencies_ShouldThrowArgumentNullException(
            string nullParam1, string nullParam2, string scenario)
        {
            // Arrange
            var webCrypto = nullParam1 == null ? null : _webCryptoMock.Object;
            var memoryManager = nullParam1 == "secureMemoryManager" ? null : _secureMemoryManagerMock.Object;

            // Act & Assert
            var action = () => new KeyDerivationService(webCrypto, memoryManager, _loggerMock.Object);
            action.Should().Throw<ArgumentNullException>();
        }

        #endregion

        #region GetParameters Tests

        [Fact]
        public void GetParameters_ShouldReturnFixedParameters()
        {
            // Act
            var parameters = _service.GetParameters();

            // Assert
            parameters.Should().NotBeNull();
            parameters.MemoryKiB.Should().Be(65536);
            parameters.Iterations.Should().Be(3);
            parameters.Parallelism.Should().Be(1);
            parameters.HashLength.Should().Be(32);
            parameters.Algorithm.Should().Be("PBKDF2");
            parameters.Version.Should().Be("1.3");
        }

        [Fact]
        public void GetParameters_ShouldReturnSameInstanceOnMultipleCalls()
        {
            // Act
            var parameters1 = _service.GetParameters();
            var parameters2 = _service.GetParameters();

            // Assert
            parameters1.Should().BeSameAs(parameters2);
        }

        #endregion

        #region DeriveMasterKeyAsync Tests

        [Theory]
        [InlineData(null, "testuser", "passphrase", "Null passphrase")]
        [InlineData("", "testuser", "passphrase", "Empty passphrase")]
        [InlineData("  ", "testuser", "passphrase", "Whitespace passphrase")]
        [InlineData("password", null, "username", "Null username")]
        [InlineData("password", "", "username", "Empty username")]
        [InlineData("password", "  ", "username", "Whitespace username")]
        public async Task DeriveMasterKeyAsync_WithInvalidInputs_ShouldThrowArgumentException(
            string passphrase, string username, string paramName, string scenario)
        {
            // Act & Assert
            await _service.Invoking(s => s.DeriveMasterKeyAsync(passphrase, username))
                .Should().ThrowAsync<ArgumentException>()
                .WithParameterName(paramName);
        }

        [Fact]
        public async Task DeriveMasterKeyAsync_WithValidInputs_ShouldDeriveKey()
        {
            // Arrange
            var expectedSalt = new byte[32];
            var expectedDerivedKey = new byte[32];
            var mockSecureBuffer = new Mock<ISecureBuffer>();

            _webCryptoMock.Setup(w => w.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedSalt);

            _webCryptoMock.Setup(w => w.Pbkdf2Async(
                It.IsAny<byte[]>(), 
                It.IsAny<byte[]>(), 
                600000, 
                32, 
                "SHA-256"))
                .ReturnsAsync(expectedDerivedKey);

            _secureMemoryManagerMock.Setup(s => s.CreateSecureBuffer(expectedDerivedKey))
                .Returns(mockSecureBuffer.Object);

            // Act
            var result = await _service.DeriveMasterKeyAsync("password123", "testuser");

            // Assert
            result.Should().BeSameAs(mockSecureBuffer.Object);
            _webCryptoMock.Verify(w => w.Sha256Async(It.Is<byte[]>(b => System.Text.Encoding.UTF8.GetString(b) == "testuser")), Times.Once);
            _webCryptoMock.Verify(w => w.Pbkdf2Async(It.IsAny<byte[]>(), expectedSalt, 600000, 32, "SHA-256"), Times.Once);
        }

        [Fact]
        public async Task DeriveMasterKeyAsync_ShouldNormalizeUsernameToLowercase()
        {
            // Arrange
            var expectedSalt = new byte[32];
            var expectedDerivedKey = new byte[32];
            var mockSecureBuffer = new Mock<ISecureBuffer>();

            _webCryptoMock.Setup(w => w.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedSalt);

            _webCryptoMock.Setup(w => w.Pbkdf2Async(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(expectedDerivedKey);

            _secureMemoryManagerMock.Setup(s => s.CreateSecureBuffer(expectedDerivedKey))
                .Returns(mockSecureBuffer.Object);

            // Act
            await _service.DeriveMasterKeyAsync("password", "TestUser");

            // Assert
            _webCryptoMock.Verify(w => w.Sha256Async(It.Is<byte[]>(b => System.Text.Encoding.UTF8.GetString(b) == "testuser")), Times.Once);
        }

        [Fact]
        public async Task DeriveMasterKeyAsync_ShouldClearOriginalDerivedKey()
        {
            // Arrange
            var expectedSalt = new byte[32];
            var expectedDerivedKey = new byte[32] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 };
            var mockSecureBuffer = new Mock<ISecureBuffer>();

            _webCryptoMock.Setup(w => w.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedSalt);

            _webCryptoMock.Setup(w => w.Pbkdf2Async(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(expectedDerivedKey);

            _secureMemoryManagerMock.Setup(s => s.CreateSecureBuffer(expectedDerivedKey))
                .Returns(mockSecureBuffer.Object);

            // Act
            await _service.DeriveMasterKeyAsync("password", "testuser");

            // Assert
            expectedDerivedKey.Should().AllBeEquivalentTo((byte)0, "Derived key should be cleared after secure buffer creation");
        }

        [Fact]
        public async Task DeriveMasterKeyAsync_ShouldReportProgress()
        {
            // Arrange
            var progressEvents = new List<KeyDerivationProgressEventArgs>();
            _service.DerivationProgress += (sender, args) => progressEvents.Add(args);

            var expectedSalt = new byte[32];
            var expectedDerivedKey = new byte[32];
            var mockSecureBuffer = new Mock<ISecureBuffer>();

            _webCryptoMock.Setup(w => w.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedSalt);

            _webCryptoMock.Setup(w => w.Pbkdf2Async(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(expectedDerivedKey);

            _secureMemoryManagerMock.Setup(s => s.CreateSecureBuffer(expectedDerivedKey))
                .Returns(mockSecureBuffer.Object);

            // Act
            await _service.DeriveMasterKeyAsync("password", "testuser");

            // Assert
            progressEvents.Should().HaveCountGreaterThan(0);
            progressEvents.Should().Contain(e => e.PercentComplete == 10 && e.Message.Contains("Starting"));
            progressEvents.Should().Contain(e => e.PercentComplete == 30 && e.Message.Contains("Deriving"));
            progressEvents.Should().Contain(e => e.PercentComplete == 90 && e.Message.Contains("Securing"));
            progressEvents.Should().Contain(e => e.PercentComplete == 100 && e.Message.Contains("complete"));
        }

        [Fact]
        public async Task DeriveMasterKeyAsync_WhenHashFails_ShouldThrowInvalidOperationException()
        {
            // Arrange
            _webCryptoMock.Setup(w => w.Sha256Async(It.IsAny<byte[]>()))
                .ThrowsAsync(new Exception("Hash failed"));

            // Act & Assert
            await _service.Invoking(s => s.DeriveMasterKeyAsync("password", "testuser"))
                .Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Failed to derive master key");
        }

        [Fact]
        public async Task DeriveMasterKeyAsync_WhenPbkdf2Fails_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var expectedSalt = new byte[32];
            _webCryptoMock.Setup(w => w.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedSalt);

            _webCryptoMock.Setup(w => w.Pbkdf2Async(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("PBKDF2 failed"));

            // Act & Assert
            await _service.Invoking(s => s.DeriveMasterKeyAsync("password", "testuser"))
                .Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Failed to derive master key");
        }

        [Fact]
        public async Task DeriveMasterKeyAsync_WhenTakesTooLong_ShouldLogWarning()
        {
            // Arrange
            var expectedSalt = new byte[32];
            var expectedDerivedKey = new byte[32];
            var mockSecureBuffer = new Mock<ISecureBuffer>();

            _webCryptoMock.Setup(w => w.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedSalt);

            // Simulate slow PBKDF2
            _webCryptoMock.Setup(w => w.Pbkdf2Async(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .Returns(async () =>
                {
                    await Task.Delay(1100); // Exceed 1000ms timeout
                    return expectedDerivedKey;
                });

            _secureMemoryManagerMock.Setup(s => s.CreateSecureBuffer(expectedDerivedKey))
                .Returns(mockSecureBuffer.Object);

            // Act
            await _service.DeriveMasterKeyAsync("password", "testuser");

            // Assert - Verify logger was called with warning
            _loggerMock.Verify(
                x => x.Log(
                    It.Is<LogLevel>(l => l == LogLevel.Warning),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("exceeding 1000ms timeout")),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Once);
        }

        [Fact]
        public async Task DeriveMasterKeyAsync_ShouldReportFailureOnException()
        {
            // Arrange
            var progressEvents = new List<KeyDerivationProgressEventArgs>();
            _service.DerivationProgress += (sender, args) => progressEvents.Add(args);

            _webCryptoMock.Setup(w => w.Sha256Async(It.IsAny<byte[]>()))
                .ThrowsAsync(new Exception("Test failure"));

            // Act & Assert
            await _service.Invoking(s => s.DeriveMasterKeyAsync("password", "testuser"))
                .Should().ThrowAsync<InvalidOperationException>();

            // Assert
            progressEvents.Should().Contain(e => e.PercentComplete == 0 && e.Message.Contains("failed"));
        }

        #endregion

        #region GenerateKeyPairAsync Tests

        [Fact]
        public async Task GenerateKeyPairAsync_WithNullMasterKey_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await _service.Invoking(s => s.GenerateKeyPairAsync(null))
                .Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("masterKey");
        }

        [Theory]
        [InlineData(16, "16 byte key")]
        [InlineData(31, "31 byte key")]
        [InlineData(33, "33 byte key")]
        [InlineData(64, "64 byte key")]
        public async Task GenerateKeyPairAsync_WithInvalidKeySize_ShouldThrowArgumentException(
            int keySize, string scenario)
        {
            // Arrange
            var mockMasterKey = new Mock<ISecureBuffer>();
            mockMasterKey.Setup(k => k.Size).Returns(keySize);

            // Act & Assert
            await _service.Invoking(s => s.GenerateKeyPairAsync(mockMasterKey.Object))
                .Should().ThrowAsync<ArgumentException>()
                .WithParameterName("masterKey")
                .WithMessage("Master key must be exactly 32 bytes*");
        }

        [Fact]
        public async Task GenerateKeyPairAsync_WithValidMasterKey_ShouldGenerateKeyPair()
        {
            // Arrange
            var mockMasterKey = new Mock<ISecureBuffer>();
            mockMasterKey.Setup(k => k.Size).Returns(32);

            var expectedEcdsaKeyPair = new ECDSAKeyPair
            {
                PublicKey = new byte[91], // Typical SPKI format size
                PrivateKey = new byte[138] // Typical PKCS8 format size
            };

            _webCryptoMock.Setup(w => w.GenerateECDSAKeyPairAsync("P-256"))
                .ReturnsAsync(expectedEcdsaKeyPair);

            // Act
            var result = await _service.GenerateKeyPairAsync(mockMasterKey.Object);

            // Assert
            result.Should().NotBeNull();
            result.PublicKey.Should().BeEquivalentTo(expectedEcdsaKeyPair.PublicKey);
            result.PrivateKey.Should().BeEquivalentTo(expectedEcdsaKeyPair.PrivateKey);
        }

        [Fact]
        public async Task GenerateKeyPairAsync_WhenWebCryptoFails_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var mockMasterKey = new Mock<ISecureBuffer>();
            mockMasterKey.Setup(k => k.Size).Returns(32);

            _webCryptoMock.Setup(w => w.GenerateECDSAKeyPairAsync("P-256"))
                .ThrowsAsync(new Exception("Key generation failed"));

            // Act & Assert
            await _service.Invoking(s => s.GenerateKeyPairAsync(mockMasterKey.Object))
                .Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Failed to generate key pair");
        }

        #endregion

        #region DeriveIdentityAsync Tests

        [Theory]
        [InlineData(null, "testuser", "passphrase", "Null passphrase")]
        [InlineData("", "testuser", "passphrase", "Empty passphrase")]
        [InlineData("  ", "testuser", "passphrase", "Whitespace passphrase")]
        [InlineData("password", null, "username", "Null username")]
        [InlineData("password", "", "username", "Empty username")]
        [InlineData("password", "  ", "username", "Whitespace username")]
        public async Task DeriveIdentityAsync_WithInvalidInputs_ShouldThrowArgumentException(
            string passphrase, string username, string paramName, string scenario)
        {
            // Act & Assert
            await _service.Invoking(s => s.DeriveIdentityAsync(passphrase, username))
                .Should().ThrowAsync<ArgumentException>()
                .WithParameterName(paramName);
        }

        [Fact]
        public async Task DeriveIdentityAsync_WithValidInputs_ShouldReturnKeyPairAndBuffer()
        {
            // Arrange
            var expectedSalt = new byte[32];
            var expectedDerivedKey = new byte[32];
            var mockMasterKey = new Mock<ISecureBuffer>();
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();

            var expectedEcdsaKeyPair = new ECDSAKeyPair
            {
                PublicKey = new byte[91],
                PrivateKey = new byte[138] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 93, 94, 95, 96, 97, 98, 99, 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 118, 119, 120, 121, 122, 123, 124, 125, 126, 127, 128, 129, 130, 131, 132, 133, 134, 135, 136, 137, 138 }
            };

            _webCryptoMock.Setup(w => w.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedSalt);

            _webCryptoMock.Setup(w => w.Pbkdf2Async(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(expectedDerivedKey);

            mockMasterKey.Setup(k => k.Size).Returns(32);

            _secureMemoryManagerMock.Setup(s => s.CreateSecureBuffer(expectedDerivedKey))
                .Returns(mockMasterKey.Object);

            _webCryptoMock.Setup(w => w.GenerateECDSAKeyPairAsync("P-256"))
                .ReturnsAsync(expectedEcdsaKeyPair);

            _secureMemoryManagerMock.Setup(s => s.CreateSecureBuffer(expectedEcdsaKeyPair.PrivateKey))
                .Returns(mockPrivateKeyBuffer.Object);

            // Act
            var result = await _service.DeriveIdentityAsync("password", "testuser");

            // Assert
            result.keyPair.Should().NotBeNull();
            result.keyPair.PublicKey.Should().BeEquivalentTo(expectedEcdsaKeyPair.PublicKey);
            result.privateKeyBuffer.Should().BeSameAs(mockPrivateKeyBuffer.Object);
            
            // Verify master key was cleared
            mockMasterKey.Verify(k => k.Clear(), Times.Once);
            
            // Verify private key in keyPair was cleared
            expectedEcdsaKeyPair.PrivateKey.Should().AllBeEquivalentTo((byte)0, "Private key should be cleared");
        }

        [Fact]
        public async Task DeriveIdentityAsync_WhenMasterKeyDerivationFails_ShouldThrowException()
        {
            // Arrange
            _webCryptoMock.Setup(w => w.Sha256Async(It.IsAny<byte[]>()))
                .ThrowsAsync(new Exception("Hash failed"));

            // Act & Assert
            await _service.Invoking(s => s.DeriveIdentityAsync("password", "testuser"))
                .Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task DeriveIdentityAsync_WhenKeyPairGenerationFails_ShouldClearMasterKey()
        {
            // Arrange
            var expectedSalt = new byte[32];
            var expectedDerivedKey = new byte[32];
            var mockMasterKey = new Mock<ISecureBuffer>();

            _webCryptoMock.Setup(w => w.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedSalt);

            _webCryptoMock.Setup(w => w.Pbkdf2Async(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(expectedDerivedKey);

            mockMasterKey.Setup(k => k.Size).Returns(32);

            _secureMemoryManagerMock.Setup(s => s.CreateSecureBuffer(expectedDerivedKey))
                .Returns(mockMasterKey.Object);

            _webCryptoMock.Setup(w => w.GenerateECDSAKeyPairAsync("P-256"))
                .ThrowsAsync(new Exception("Key generation failed"));

            // Act & Assert
            await _service.Invoking(s => s.DeriveIdentityAsync("password", "testuser"))
                .Should().ThrowAsync<InvalidOperationException>();

            // Assert master key was cleared even though operation failed
            mockMasterKey.Verify(k => k.Clear(), Times.Once);
        }

        #endregion

        #region Event Tests

        [Fact]
        public async Task DerivationProgress_WhenSubscribed_ShouldReceiveEvents()
        {
            // Arrange
            var progressEvents = new List<KeyDerivationProgressEventArgs>();
            _service.DerivationProgress += (sender, args) => progressEvents.Add(args);

            var expectedSalt = new byte[32];
            var expectedDerivedKey = new byte[32];
            var mockSecureBuffer = new Mock<ISecureBuffer>();

            _webCryptoMock.Setup(w => w.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedSalt);

            _webCryptoMock.Setup(w => w.Pbkdf2Async(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(expectedDerivedKey);

            _secureMemoryManagerMock.Setup(s => s.CreateSecureBuffer(expectedDerivedKey))
                .Returns(mockSecureBuffer.Object);

            // Act
            await _service.DeriveMasterKeyAsync("password", "testuser");

            // Assert
            progressEvents.Should().HaveCountGreaterThan(0);
            
            // Verify each event has proper data
            foreach (var evt in progressEvents)
            {
                evt.PercentComplete.Should().BeInRange(0, 100);
                evt.Message.Should().NotBeNullOrWhiteSpace();
                evt.ElapsedTime.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
            }
        }

        [Fact]
        public async Task DerivationProgress_WithMultipleSubscribers_ShouldNotifyAll()
        {
            // Arrange
            var progressEvents1 = new List<KeyDerivationProgressEventArgs>();
            var progressEvents2 = new List<KeyDerivationProgressEventArgs>();
            
            _service.DerivationProgress += (sender, args) => progressEvents1.Add(args);
            _service.DerivationProgress += (sender, args) => progressEvents2.Add(args);

            var expectedSalt = new byte[32];
            var expectedDerivedKey = new byte[32];
            var mockSecureBuffer = new Mock<ISecureBuffer>();

            _webCryptoMock.Setup(w => w.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedSalt);

            _webCryptoMock.Setup(w => w.Pbkdf2Async(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(expectedDerivedKey);

            _secureMemoryManagerMock.Setup(s => s.CreateSecureBuffer(expectedDerivedKey))
                .Returns(mockSecureBuffer.Object);

            // Act
            await _service.DeriveMasterKeyAsync("password", "testuser");

            // Assert
            progressEvents1.Should().HaveCountGreaterThan(0);
            progressEvents2.Should().HaveCountGreaterThan(0);
            progressEvents1.Should().HaveCount(progressEvents2.Count);
        }

        [Fact]
        public async Task DerivationProgress_WithNoSubscribers_ShouldNotThrow()
        {
            // Arrange
            var expectedSalt = new byte[32];
            var expectedDerivedKey = new byte[32];
            var mockSecureBuffer = new Mock<ISecureBuffer>();

            _webCryptoMock.Setup(w => w.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedSalt);

            _webCryptoMock.Setup(w => w.Pbkdf2Async(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(expectedDerivedKey);

            _secureMemoryManagerMock.Setup(s => s.CreateSecureBuffer(expectedDerivedKey))
                .Returns(mockSecureBuffer.Object);

            // Act & Assert
            await _service.Invoking(s => s.DeriveMasterKeyAsync("password", "testuser"))
                .Should().NotThrowAsync();
        }

        #endregion

        #region Performance and Security Tests

        [Fact]
        public async Task DeriveMasterKeyAsync_ShouldUseSecureParameters()
        {
            // Arrange
            var expectedSalt = new byte[32];
            var expectedDerivedKey = new byte[32];
            var mockSecureBuffer = new Mock<ISecureBuffer>();

            _webCryptoMock.Setup(w => w.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedSalt);

            _webCryptoMock.Setup(w => w.Pbkdf2Async(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(expectedDerivedKey);

            _secureMemoryManagerMock.Setup(s => s.CreateSecureBuffer(expectedDerivedKey))
                .Returns(mockSecureBuffer.Object);

            // Act
            await _service.DeriveMasterKeyAsync("password", "testuser");

            // Assert - Verify PBKDF2 called with secure parameters
            _webCryptoMock.Verify(w => w.Pbkdf2Async(
                It.IsAny<byte[]>(), 
                It.IsAny<byte[]>(), 
                600000,  // High iteration count
                32,      // 32 byte output
                "SHA-256"), Times.Once);
        }

        [Fact]
        public async Task DeriveIdentityAsync_WithSameInputs_ShouldProduceDeterministicResults()
        {
            // Arrange
            var expectedSalt = new byte[32];
            var expectedDerivedKey = new byte[32] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 };
            var mockMasterKey = new Mock<ISecureBuffer>();
            var mockPrivateKeyBuffer1 = new Mock<ISecureBuffer>();
            var mockPrivateKeyBuffer2 = new Mock<ISecureBuffer>();

            var expectedEcdsaKeyPair = new ECDSAKeyPair
            {
                PublicKey = new byte[91] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91 },
                PrivateKey = new byte[138]
            };

            // Setup mocks to return consistent values
            _webCryptoMock.Setup(w => w.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedSalt);

            _webCryptoMock.Setup(w => w.Pbkdf2Async(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(expectedDerivedKey);

            mockMasterKey.Setup(k => k.Size).Returns(32);

            _secureMemoryManagerMock.Setup(s => s.CreateSecureBuffer(expectedDerivedKey))
                .Returns(mockMasterKey.Object);

            _webCryptoMock.Setup(w => w.GenerateECDSAKeyPairAsync("P-256"))
                .ReturnsAsync(expectedEcdsaKeyPair);

            _secureMemoryManagerMock.SetupSequence(s => s.CreateSecureBuffer(expectedEcdsaKeyPair.PrivateKey))
                .Returns(mockPrivateKeyBuffer1.Object)
                .Returns(mockPrivateKeyBuffer2.Object);

            // Act
            var result1 = await _service.DeriveIdentityAsync("password", "testuser");
            var result2 = await _service.DeriveIdentityAsync("password", "testuser");

            // Assert - Should produce consistent results
            result1.keyPair.PublicKey.Should().BeEquivalentTo(result2.keyPair.PublicKey);
            
            // Verify deterministic salt generation
            _webCryptoMock.Verify(w => w.Sha256Async(It.Is<byte[]>(b => System.Text.Encoding.UTF8.GetString(b) == "testuser")), Times.Exactly(2));
        }

        #endregion

        #region Edge Cases and Error Handling

        [Fact]
        public async Task GenerateKeyPairAsync_WhenWebCryptoReturnsNull_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var mockMasterKey = new Mock<ISecureBuffer>();
            mockMasterKey.Setup(k => k.Size).Returns(32);

            _webCryptoMock.Setup(w => w.GenerateECDSAKeyPairAsync("P-256"))
                .ReturnsAsync((ECDSAKeyPair)null);

            // Act & Assert
            await _service.Invoking(s => s.GenerateKeyPairAsync(mockMasterKey.Object))
                .Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Failed to generate key pair");
        }

        [Theory]
        [InlineData("Password123", "User1", "Different password")]
        [InlineData("password", "User1", "Different username case")]
        [InlineData("password", "user2", "Different username")]
        public async Task DeriveMasterKeyAsync_WithDifferentInputs_ShouldCallCryptoWithDifferentParameters(
            string passphrase, string username, string scenario)
        {
            // Arrange
            var expectedSalt = new byte[32];
            var expectedDerivedKey = new byte[32];
            var mockSecureBuffer = new Mock<ISecureBuffer>();

            _webCryptoMock.Setup(w => w.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedSalt);

            _webCryptoMock.Setup(w => w.Pbkdf2Async(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(expectedDerivedKey);

            _secureMemoryManagerMock.Setup(s => s.CreateSecureBuffer(expectedDerivedKey))
                .Returns(mockSecureBuffer.Object);

            // Act
            await _service.DeriveMasterKeyAsync(passphrase, username);

            // Assert
            _webCryptoMock.Verify(w => w.Sha256Async(It.Is<byte[]>(b => System.Text.Encoding.UTF8.GetString(b) == username.ToLowerInvariant())), Times.Once, scenario);
        }

        #endregion
    }
}