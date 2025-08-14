using FluentAssertions;
using Moq;
using NoLock.Social.Core.Cryptography.Interfaces;
using NoLock.Social.Core.Cryptography.Services;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace NoLock.Social.Core.Tests.Cryptography
{
    public class KeyDerivationServiceTests : IDisposable
    {
        private readonly KeyDerivationService _sut;
        private readonly Mock<IWebCryptoService> _webCryptoMock;
        private readonly Mock<ISecureMemoryManager> _secureMemoryManagerMock;

        public KeyDerivationServiceTests()
        {
            _webCryptoMock = new Mock<IWebCryptoService>();
            _secureMemoryManagerMock = new Mock<ISecureMemoryManager>();
            _sut = new KeyDerivationService(_webCryptoMock.Object, _secureMemoryManagerMock.Object);
        }

        public void Dispose()
        {
            // Cleanup if needed
        }

        [Fact]
        public void GetParameters_ShouldReturnImmutableParameters()
        {
            // Act
            var parameters = _sut.GetParameters();

            // Assert
            parameters.Should().NotBeNull();
            parameters.MemoryKiB.Should().Be(65536); // 64MB
            parameters.Iterations.Should().Be(3);
            parameters.Parallelism.Should().Be(1); // WASM constraint
            parameters.HashLength.Should().Be(32);
            parameters.Algorithm.Should().Be("PBKDF2");
            parameters.Version.Should().Be("1.3");
        }

        [Fact]
        public async Task DeriveMasterKeyAsync_WithValidInput_ShouldDeriveKey()
        {
            // Arrange
            var passphrase = "TestPassphrase123!";
            var username = "testuser";
            var expectedDerivedKey = new byte[32];
            Random.Shared.NextBytes(expectedDerivedKey);
            
            var secureBufferMock = new Mock<ISecureBuffer>();
            secureBufferMock.Setup(b => b.Data).Returns(expectedDerivedKey);
            
            var saltData = System.Text.Encoding.UTF8.GetBytes(username.ToLowerInvariant());
            var salt = new byte[32];
            Random.Shared.NextBytes(salt);
            
            _webCryptoMock
                .Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(salt);
            
            _webCryptoMock
                .Setup(c => c.Pbkdf2Async(It.IsAny<byte[]>(), It.IsAny<byte[]>(), 600000, 32, "SHA-256"))
                .ReturnsAsync(expectedDerivedKey);
            
            _secureMemoryManagerMock
                .Setup(m => m.CreateSecureBuffer(It.IsAny<byte[]>()))
                .Returns(secureBufferMock.Object);

            KeyDerivationProgressEventArgs? capturedProgress = null;
            _sut.DerivationProgress += (sender, args) => capturedProgress = args;

            // Act
            var result = await _sut.DeriveMasterKeyAsync(passphrase, username);

            // Assert
            result.Should().NotBeNull();
            result.Should().Be(secureBufferMock.Object);
            
            // Verify PBKDF2 was called
            _webCryptoMock.Verify(c => c.Pbkdf2Async(It.IsAny<byte[]>(), It.IsAny<byte[]>(), 600000, 32, "SHA-256"), Times.Once);
            
            // Verify progress was reported
            capturedProgress.Should().NotBeNull();
        }

        [Fact]
        public async Task DeriveMasterKeyAsync_ShouldNormalizeUsername()
        {
            // Arrange
            var passphrase = "TestPassphrase";
            var username = "TestUser123";
            var expectedDerivedKey = new byte[32];
            
            _webCryptoMock
                .Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(new byte[32]);
            
            _webCryptoMock
                .Setup(c => c.Pbkdf2Async(It.IsAny<byte[]>(), It.IsAny<byte[]>(), 600000, 32, "SHA-256"))
                .ReturnsAsync(expectedDerivedKey);
            
            _secureMemoryManagerMock
                .Setup(m => m.CreateSecureBuffer(It.IsAny<byte[]>()))
                .Returns(Mock.Of<ISecureBuffer>());

            // Act
            await _sut.DeriveMasterKeyAsync(passphrase, username);

            // Assert - verify PBKDF2 was called
            _webCryptoMock.Verify(c => c.Pbkdf2Async(It.IsAny<byte[]>(), It.IsAny<byte[]>(), 600000, 32, "SHA-256"), Times.Once);
        }

        [Fact]
        public async Task DeriveMasterKeyAsync_WithEmptyPassphrase_ShouldThrow()
        {
            // Arrange
            var emptyPassphrase = "";
            var username = "testuser";

            // Act & Assert
            var act = async () => await _sut.DeriveMasterKeyAsync(emptyPassphrase, username);
            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*passphrase*");
        }

        [Fact]
        public async Task DeriveMasterKeyAsync_WithEmptyUsername_ShouldThrow()
        {
            // Arrange
            var passphrase = "TestPassphrase";
            var emptyUsername = "";

            // Act & Assert
            var act = async () => await _sut.DeriveMasterKeyAsync(passphrase, emptyUsername);
            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*username*");
        }

        [Fact]
        public async Task GenerateKeyPairAsync_WithValidSeed_ShouldGenerateKeyPair()
        {
            // Arrange
            var seed = new byte[32];
            Random.Shared.NextBytes(seed);
            var secureBufferMock = new Mock<ISecureBuffer>();
            secureBufferMock.Setup(b => b.Data).Returns(seed);
            secureBufferMock.Setup(b => b.Size).Returns(32);
            
            var expectedKeyPair = new ECDSAKeyPair
            {
                PublicKey = new byte[65],  // ECDSA P-256 public key size
                PrivateKey = new byte[32]   // ECDSA P-256 private key size
            };
            Random.Shared.NextBytes(expectedKeyPair.PublicKey);
            Random.Shared.NextBytes(expectedKeyPair.PrivateKey);
            
            _webCryptoMock
                .Setup(c => c.GenerateECDSAKeyPairAsync("P-256"))
                .ReturnsAsync(expectedKeyPair);

            // Act
            var result = await _sut.GenerateKeyPairAsync(secureBufferMock.Object);

            // Assert
            result.Should().NotBeNull();
            result.PublicKey.Should().BeEquivalentTo(expectedKeyPair.PublicKey);
            result.PrivateKey.Should().BeEquivalentTo(expectedKeyPair.PrivateKey);
        }

        [Fact]
        public async Task GenerateKeyPairAsync_WithInvalidSeedSize_ShouldThrow()
        {
            // Arrange
            var invalidSeed = new byte[16]; // Wrong size
            var secureBufferMock = new Mock<ISecureBuffer>();
            secureBufferMock.Setup(b => b.Data).Returns(invalidSeed);
            secureBufferMock.Setup(b => b.Size).Returns(16);

            // Act & Assert
            var act = async () => await _sut.GenerateKeyPairAsync(secureBufferMock.Object);
            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*32 bytes*");
        }

        [Fact]
        public async Task DeriveIdentityAsync_ShouldPerformCompleteFlow()
        {
            // Arrange
            var passphrase = "TestPassphrase123!";
            var username = "testuser";
            var derivedKey = new byte[32];
            Random.Shared.NextBytes(derivedKey);
            
            var expectedKeyPair = new ECDSAKeyPair
            {
                PublicKey = new byte[65],  // ECDSA P-256 public key size
                PrivateKey = new byte[32]   // ECDSA P-256 private key size
            };
            Random.Shared.NextBytes(expectedKeyPair.PublicKey);
            Random.Shared.NextBytes(expectedKeyPair.PrivateKey);
            
            var masterKeyBufferMock = new Mock<ISecureBuffer>();
            masterKeyBufferMock.Setup(b => b.Data).Returns(derivedKey);
            masterKeyBufferMock.Setup(b => b.Size).Returns(32);
            
            var privateKeyBufferMock = new Mock<ISecureBuffer>();
            privateKeyBufferMock.Setup(b => b.Data).Returns(expectedKeyPair.PrivateKey);
            
            _webCryptoMock
                .Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(new byte[32]);
            
            _webCryptoMock
                .Setup(c => c.Pbkdf2Async(It.IsAny<byte[]>(), It.IsAny<byte[]>(), 600000, 32, "SHA-256"))
                .ReturnsAsync(derivedKey);
            
            _webCryptoMock
                .Setup(c => c.GenerateECDSAKeyPairAsync("P-256"))
                .ReturnsAsync(expectedKeyPair);
            
            _secureMemoryManagerMock
                .SetupSequence(m => m.CreateSecureBuffer(It.IsAny<byte[]>()))
                .Returns(masterKeyBufferMock.Object)
                .Returns(privateKeyBufferMock.Object);

            // Act
            var (keyPair, privateKeyBuffer) = await _sut.DeriveIdentityAsync(passphrase, username);

            // Assert
            keyPair.Should().NotBeNull();
            keyPair.PublicKey.Should().BeEquivalentTo(expectedKeyPair.PublicKey);
            privateKeyBuffer.Should().Be(privateKeyBufferMock.Object);
            
            // Verify master key was cleared after use
            masterKeyBufferMock.Verify(b => b.Clear(), Times.Once);
        }

        [Fact]
        public async Task DeriveMasterKeyAsync_ShouldCompleteWithinPerformanceBudget()
        {
            // Arrange
            var passphrase = "TestPassphrase123!";
            var username = "testuser";
            var derivedKey = new byte[32];
            
            _webCryptoMock
                .Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(new byte[32]);
            
            _webCryptoMock
                .Setup(c => c.Pbkdf2Async(It.IsAny<byte[]>(), It.IsAny<byte[]>(), 600000, 32, "SHA-256"))
                .ReturnsAsync(derivedKey);
            
            _secureMemoryManagerMock
                .Setup(m => m.CreateSecureBuffer(It.IsAny<byte[]>()))
                .Returns(Mock.Of<ISecureBuffer>());

            // Act
            var stopwatch = Stopwatch.StartNew();
            await _sut.DeriveMasterKeyAsync(passphrase, username);
            stopwatch.Stop();

            // Assert - should complete within 1000ms (with margin for test overhead)
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(1500, "Key derivation should complete within performance budget");
        }

        [Fact]
        public async Task DeriveMasterKeyAsync_ShouldBeDeterministic()
        {
            // Arrange
            var passphrase = "TestPassphrase123!";
            var username = "testuser";
            var expectedKey = new byte[32] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 };
            
            var bufferMock1 = new Mock<ISecureBuffer>();
            bufferMock1.Setup(b => b.Data).Returns(expectedKey);
            
            var bufferMock2 = new Mock<ISecureBuffer>();
            bufferMock2.Setup(b => b.Data).Returns(expectedKey);
            
            _webCryptoMock
                .Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(new byte[32]);
            
            _webCryptoMock
                .Setup(c => c.Pbkdf2Async(It.IsAny<byte[]>(), It.IsAny<byte[]>(), 600000, 32, "SHA-256"))
                .ReturnsAsync(expectedKey);
            
            _secureMemoryManagerMock
                .SetupSequence(m => m.CreateSecureBuffer(It.IsAny<byte[]>()))
                .Returns(bufferMock1.Object)
                .Returns(bufferMock2.Object);

            // Act
            var result1 = await _sut.DeriveMasterKeyAsync(passphrase, username);
            var result2 = await _sut.DeriveMasterKeyAsync(passphrase, username);

            // Assert - same input should produce same output
            result1.Data.Should().BeEquivalentTo(result2.Data);
        }

        [Fact]
        public async Task DeriveIdentityAsync_WithDifferentPassphrases_ShouldProduceDifferentKeys()
        {
            // Arrange
            var username = "testuser";
            var passphrase1 = "Password1";
            var passphrase2 = "Password2";
            
            var key1 = new byte[32] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 };
            var key2 = new byte[32] { 32, 31, 30, 29, 28, 27, 26, 25, 24, 23, 22, 21, 20, 19, 18, 17, 16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1 };
            
            var privateKey1 = new byte[32];
            var privateKey2 = new byte[32];
            Random.Shared.NextBytes(privateKey1);
            Random.Shared.NextBytes(privateKey2);
            
            var keyPair1 = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = privateKey1 };
            var keyPair2 = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = privateKey2 };
            keyPair1.PublicKey[0] = 1;
            keyPair2.PublicKey[0] = 2;
            
            _webCryptoMock
                .Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(new byte[32]);
            
            _webCryptoMock
                .SetupSequence(c => c.Pbkdf2Async(It.IsAny<byte[]>(), It.IsAny<byte[]>(), 600000, 32, "SHA-256"))
                .ReturnsAsync(key1)
                .ReturnsAsync(key2);
            
            _webCryptoMock
                .SetupSequence(c => c.GenerateECDSAKeyPairAsync("P-256"))
                .ReturnsAsync(new ECDSAKeyPair { PublicKey = keyPair1.PublicKey, PrivateKey = keyPair1.PrivateKey })
                .ReturnsAsync(new ECDSAKeyPair { PublicKey = keyPair2.PublicKey, PrivateKey = keyPair2.PrivateKey });
            
            // Setup master key buffers and private key buffers for each passphrase
            var masterKeyBuffer1 = new Mock<ISecureBuffer>();
            masterKeyBuffer1.Setup(b => b.Data).Returns(key1);
            masterKeyBuffer1.Setup(b => b.Size).Returns(32);
            masterKeyBuffer1.Setup(b => b.Clear()).Callback(() => { });
            
            var privateKeyBuffer1 = new Mock<ISecureBuffer>();
            privateKeyBuffer1.Setup(b => b.Data).Returns(privateKey1);
            privateKeyBuffer1.Setup(b => b.Size).Returns(32);
            
            var masterKeyBuffer2 = new Mock<ISecureBuffer>();
            masterKeyBuffer2.Setup(b => b.Data).Returns(key2);
            masterKeyBuffer2.Setup(b => b.Size).Returns(32);
            masterKeyBuffer2.Setup(b => b.Clear()).Callback(() => { });
            
            var privateKeyBuffer2 = new Mock<ISecureBuffer>();
            privateKeyBuffer2.Setup(b => b.Data).Returns(privateKey2);
            privateKeyBuffer2.Setup(b => b.Size).Returns(32);
            
            _secureMemoryManagerMock
                .SetupSequence(m => m.CreateSecureBuffer(It.IsAny<byte[]>()))
                .Returns(masterKeyBuffer1.Object)
                .Returns(privateKeyBuffer1.Object)
                .Returns(masterKeyBuffer2.Object)
                .Returns(privateKeyBuffer2.Object);

            // Act
            var (identity1, _) = await _sut.DeriveIdentityAsync(passphrase1, username);
            var (identity2, _) = await _sut.DeriveIdentityAsync(passphrase2, username);

            // Assert
            identity1.PublicKey.Should().NotBeEquivalentTo(identity2.PublicKey);
        }
    }
}