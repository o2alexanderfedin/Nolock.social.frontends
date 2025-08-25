using FluentAssertions;
using Moq;
using NoLock.Social.Core.Cryptography.Interfaces;
using NoLock.Social.Core.Cryptography.Services;

namespace NoLock.Social.Core.Tests.Cryptography
{
    public class SessionStateServiceTests : IDisposable
    {
        private readonly SessionStateService _sut;
        private readonly Mock<ISecureMemoryManager> _secureMemoryManagerMock;
        private readonly Mock<IWebCryptoService> _cryptoInteropMock;

        public SessionStateServiceTests()
        {
            _secureMemoryManagerMock = new Mock<ISecureMemoryManager>();
            _cryptoInteropMock = new Mock<IWebCryptoService>();
            _sut = new SessionStateService(_secureMemoryManagerMock.Object, _cryptoInteropMock.Object);
        }

        public void Dispose()
        {
            _sut?.Dispose();
        }

        [Fact]
        public void InitialState_ShouldBeLocked()
        {
            // Assert
            _sut.CurrentState.Should().Be(SessionState.Locked);
            _sut.IsUnlocked.Should().BeFalse();
            _sut.CurrentSession.Should().BeNull();
        }

        [Fact]
        public async Task StartSessionAsync_WithValidData_ShouldCreateSession()
        {
            // Arrange
            var username = "testuser";
            var keyPair = new Ed25519KeyPair
            {
                PublicKey = new byte[32],
                PrivateKey = new byte[64]
            };
            var privateKeyBuffer = Mock.Of<ISecureBuffer>();
            SessionStateChangedEventArgs? capturedArgs = null;
            _sut.SessionStateChanged += (sender, args) => capturedArgs = args;

            // Act
            var result = await _sut.StartSessionAsync(username, keyPair, privateKeyBuffer);

            // Assert
            result.Should().BeTrue();
            _sut.CurrentState.Should().Be(SessionState.Unlocked);
            _sut.IsUnlocked.Should().BeTrue();
            _sut.CurrentSession.Should().NotBeNull();
            _sut.CurrentSession!.Username.Should().Be(username);
            _sut.CurrentSession.PublicKey.Should().BeEquivalentTo(keyPair.PublicKey);
            _sut.CurrentSession.PrivateKeyBuffer.Should().Be(privateKeyBuffer);
            
            capturedArgs.Should().NotBeNull();
            capturedArgs!.OldState.Should().Be(SessionState.Locked);
            capturedArgs.NewState.Should().Be(SessionState.Unlocked);
        }

        [Fact]
        public async Task StartSessionAsync_WhenAlreadyUnlocked_ShouldReturnFalse()
        {
            // Arrange
            var keyPair = new Ed25519KeyPair
            {
                PublicKey = new byte[32],
                PrivateKey = new byte[64]
            };
            var privateKeyBuffer = Mock.Of<ISecureBuffer>();
            await _sut.StartSessionAsync("user1", keyPair, privateKeyBuffer);

            // Act
            var result = await _sut.StartSessionAsync("user2", keyPair, privateKeyBuffer);

            // Assert
            result.Should().BeFalse();
            _sut.CurrentSession!.Username.Should().Be("user1");
        }

        [Fact]
        public async Task LockSessionAsync_WhenUnlocked_ShouldLockSession()
        {
            // Arrange
            var keyPair = new Ed25519KeyPair
            {
                PublicKey = new byte[32],
                PrivateKey = new byte[64]
            };
            var privateKeyBuffer = Mock.Of<ISecureBuffer>();
            await _sut.StartSessionAsync("testuser", keyPair, privateKeyBuffer);
            SessionStateChangedEventArgs? capturedArgs = null;
            _sut.SessionStateChanged += (sender, args) => capturedArgs = args;

            // Act
            await _sut.LockSessionAsync();

            // Assert
            _sut.CurrentState.Should().Be(SessionState.Locked);
            _sut.IsUnlocked.Should().BeFalse();
            _sut.CurrentSession.Should().NotBeNull(); // Session still exists but is locked
            
            capturedArgs.Should().NotBeNull();
            capturedArgs!.OldState.Should().Be(SessionState.Unlocked);
            capturedArgs.NewState.Should().Be(SessionState.Locked);
        }

        [Fact]
        public async Task EndSessionAsync_ShouldClearAllData()
        {
            // Arrange
            var keyPair = new Ed25519KeyPair
            {
                PublicKey = new byte[32],
                PrivateKey = new byte[64]
            };
            var privateKeyBufferMock = new Mock<ISecureBuffer>();
            await _sut.StartSessionAsync("testuser", keyPair, privateKeyBufferMock.Object);

            // Act
            await _sut.EndSessionAsync();

            // Assert
            _sut.CurrentState.Should().Be(SessionState.Locked);
            _sut.IsUnlocked.Should().BeFalse();
            _sut.CurrentSession.Should().BeNull();
            privateKeyBufferMock.Verify(b => b.Clear(), Times.Once);
        }

        [Fact]
        public void UpdateActivity_ShouldUpdateLastActivityTime()
        {
            // Arrange
            var keyPair = new Ed25519KeyPair
            {
                PublicKey = new byte[32],
                PrivateKey = new byte[64]
            };
            var privateKeyBuffer = Mock.Of<ISecureBuffer>();
            _sut.StartSessionAsync("testuser", keyPair, privateKeyBuffer).Wait();
            var initialActivity = _sut.CurrentSession!.LastActivityAt;
            System.Threading.Thread.Sleep(10); // Small delay to ensure time difference

            // Act
            _sut.UpdateActivity();

            // Assert
            _sut.CurrentSession.LastActivityAt.Should().BeAfter(initialActivity);
        }

        [Fact]
        public async Task CheckTimeoutAsync_WhenTimedOut_ShouldLockSession()
        {
            // Arrange
            var keyPair = new Ed25519KeyPair
            {
                PublicKey = new byte[32],
                PrivateKey = new byte[64]
            };
            var privateKeyBuffer = Mock.Of<ISecureBuffer>();
            _sut.SessionTimeoutMinutes = 0; // Set to 0 for immediate timeout
            await _sut.StartSessionAsync("testuser", keyPair, privateKeyBuffer);
            _sut.CurrentSession!.LastActivityAt = DateTime.UtcNow.AddMinutes(-1); // Force timeout

            // Act
            await _sut.CheckTimeoutAsync();

            // Assert
            _sut.CurrentState.Should().Be(SessionState.Expired);
            _sut.IsUnlocked.Should().BeFalse();
        }

        [Fact]
        public async Task CheckTimeoutAsync_WhenNotTimedOut_ShouldRemainUnlocked()
        {
            // Arrange
            var keyPair = new Ed25519KeyPair
            {
                PublicKey = new byte[32],
                PrivateKey = new byte[64]
            };
            var privateKeyBuffer = Mock.Of<ISecureBuffer>();
            _sut.SessionTimeoutMinutes = 15;
            await _sut.StartSessionAsync("testuser", keyPair, privateKeyBuffer);

            // Act
            await _sut.CheckTimeoutAsync();

            // Assert
            _sut.CurrentState.Should().Be(SessionState.Unlocked);
            _sut.IsUnlocked.Should().BeTrue();
        }

        [Fact]
        public async Task GetRemainingTime_ShouldReturnCorrectTimeSpan()
        {
            // Arrange
            var keyPair = new Ed25519KeyPair
            {
                PublicKey = new byte[32],
                PrivateKey = new byte[64]
            };
            var privateKeyBuffer = Mock.Of<ISecureBuffer>();
            _sut.SessionTimeoutMinutes = 15;
            await _sut.StartSessionAsync("testuser", keyPair, privateKeyBuffer);

            // Act
            var remainingTime = _sut.GetRemainingTime();

            // Assert
            remainingTime.Should().BeLessThanOrEqualTo(TimeSpan.FromMinutes(15));
            remainingTime.Should().BeGreaterThan(TimeSpan.FromMinutes(14));
        }

        [Fact]
        public async Task UnlockSessionAsync_WithLockedSession_ShouldUnlock()
        {
            // Arrange
            var username = "testuser";
            var passphrase = "testpassphrase";
            var keyPair = new Ed25519KeyPair
            {
                PublicKey = new byte[32],
                PrivateKey = new byte[64]
            };
            var privateKeyBuffer = Mock.Of<ISecureBuffer>();
            
            // Setup the crypto interop to return matching key for unlock
            _cryptoInteropMock
                .Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(new byte[32]);
            _cryptoInteropMock
                .Setup(c => c.Pbkdf2Async(It.IsAny<byte[]>(), It.IsAny<byte[]>(), 600000, 32, "SHA-256"))
                .ReturnsAsync(new byte[32]);
            _cryptoInteropMock
                .Setup(c => c.GenerateECDSAKeyPairAsync("P-256"))
                .ReturnsAsync(new ECDSAKeyPair { PublicKey = keyPair.PublicKey, PrivateKey = keyPair.PrivateKey });

            await _sut.StartSessionAsync(username, keyPair, privateKeyBuffer);
            await _sut.LockSessionAsync();

            // Act
            var result = await _sut.UnlockSessionAsync(passphrase);

            // Assert
            result.Should().BeTrue();
            _sut.CurrentState.Should().Be(SessionState.Unlocked);
            _sut.IsUnlocked.Should().BeTrue();
        }

        [Fact]
        public async Task UnlockSessionAsync_WithWrongPassphrase_ShouldRemainLocked()
        {
            // Arrange
            var username = "testuser";
            var correctPassphrase = "correctpassphrase";
            var wrongPassphrase = "wrongpassphrase";
            var keyPair = new Ed25519KeyPair
            {
                PublicKey = new byte[32],
                PrivateKey = new byte[64]
            };
            var wrongKeyPair = new Ed25519KeyPair
            {
                PublicKey = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 }, // Different public key
                PrivateKey = new byte[64]
            };
            var privateKeyBuffer = Mock.Of<ISecureBuffer>();
            
            // Setup the crypto interop to return different keys for wrong passphrase
            _cryptoInteropMock
                .Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(new byte[32]);
            _cryptoInteropMock
                .Setup(c => c.Pbkdf2Async(It.IsAny<byte[]>(), It.IsAny<byte[]>(), 600000, 32, "SHA-256"))
                .ReturnsAsync(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 });
            _cryptoInteropMock
                .Setup(c => c.GenerateECDSAKeyPairAsync("P-256"))
                .ReturnsAsync(new ECDSAKeyPair { PublicKey = wrongKeyPair.PublicKey, PrivateKey = wrongKeyPair.PrivateKey });

            await _sut.StartSessionAsync(username, keyPair, privateKeyBuffer);
            await _sut.LockSessionAsync();

            // Act
            var result = await _sut.UnlockSessionAsync(wrongPassphrase);

            // Assert
            result.Should().BeFalse();
            _sut.CurrentState.Should().Be(SessionState.Locked);
            _sut.IsUnlocked.Should().BeFalse();
        }

        [Fact]
        public void Dispose_ShouldClearSession()
        {
            // Arrange
            var keyPair = new Ed25519KeyPair
            {
                PublicKey = new byte[32],
                PrivateKey = new byte[64]
            };
            var privateKeyBufferMock = new Mock<ISecureBuffer>();
            var service = new SessionStateService(_secureMemoryManagerMock.Object, _cryptoInteropMock.Object);
            service.StartSessionAsync("testuser", keyPair, privateKeyBufferMock.Object).Wait();

            // Act
            service.Dispose();

            // Assert
            privateKeyBufferMock.Verify(b => b.Clear(), Times.Once);
            service.CurrentState.Should().Be(SessionState.Locked);
            service.CurrentSession.Should().BeNull();
        }
    }
}