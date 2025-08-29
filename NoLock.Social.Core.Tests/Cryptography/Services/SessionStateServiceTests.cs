using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NoLock.Social.Core.Cryptography.Interfaces;
using NoLock.Social.Core.Cryptography.Services;
using Xunit;
using System.Threading;

namespace NoLock.Social.Core.Tests.Cryptography.Services
{
    public class SessionStateServiceTests : IDisposable
    {
        private readonly Mock<ISecureMemoryManager> _secureMemoryManagerMock;
        private readonly Mock<IWebCryptoService> _cryptoInteropMock;
        private readonly SessionStateService _service;

        public SessionStateServiceTests()
        {
            _secureMemoryManagerMock = new Mock<ISecureMemoryManager>();
            _cryptoInteropMock = new Mock<IWebCryptoService>();

            _service = new SessionStateService(_secureMemoryManagerMock.Object, _cryptoInteropMock.Object);
        }

        public void Dispose()
        {
            _service?.Dispose();
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidDependencies_ShouldInitialize()
        {
            // Arrange & Act
            var service = new SessionStateService(_secureMemoryManagerMock.Object, _cryptoInteropMock.Object);

            // Assert
            service.Should().NotBeNull();
            service.CurrentState.Should().Be(SessionState.Locked);
            service.CurrentSession.Should().BeNull();
            service.IsUnlocked.Should().BeFalse();
            service.SessionTimeoutMinutes.Should().Be(15);
        }

        [Theory]
        [InlineData(null, "secureMemoryManager", "Null secure memory manager")]
        [InlineData("cryptoInterop", null, "Null crypto interop")]
        public void Constructor_WithNullDependencies_ShouldThrowArgumentNullException(
            string nullParam1, string nullParam2, string scenario)
        {
            // Arrange
            var memoryManager = nullParam1 == null ? null : _secureMemoryManagerMock.Object;
            var cryptoInterop = nullParam1 == "cryptoInterop" ? null : _cryptoInteropMock.Object;

            // Act & Assert
            var action = () => new SessionStateService(memoryManager, cryptoInterop);
            action.Should().Throw<ArgumentNullException>();
        }

        #endregion

        #region Property Tests

        [Theory]
        [InlineData(10, "10 minute timeout")]
        [InlineData(30, "30 minute timeout")]
        [InlineData(60, "60 minute timeout")]
        public void SessionTimeoutMinutes_WhenSet_ShouldUpdateValue(int timeout, string scenario)
        {
            // Act
            _service.SessionTimeoutMinutes = timeout;

            // Assert
            _service.SessionTimeoutMinutes.Should().Be(timeout, scenario);
        }

        [Fact]
        public void CurrentState_WhenDisposed_ShouldReturnLocked()
        {
            // Arrange
            _service.Dispose();

            // Act & Assert
            _service.CurrentState.Should().Be(SessionState.Locked);
        }

        [Fact]
        public void CurrentSession_WhenDisposed_ShouldReturnNull()
        {
            // Arrange
            _service.Dispose();

            // Act & Assert
            _service.CurrentSession.Should().BeNull();
        }

        [Fact]
        public void IsUnlocked_WhenSessionUnlocked_ShouldReturnTrue()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };
            _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object).Wait();

            // Act & Assert
            _service.IsUnlocked.Should().BeTrue();
        }

        [Fact]
        public void IsUnlocked_WhenSessionLocked_ShouldReturnFalse()
        {
            // Act & Assert
            _service.IsUnlocked.Should().BeFalse();
        }

        #endregion

        #region StartSessionAsync Tests

        [Theory]
        [InlineData(null, "username", "Null username")]
        [InlineData("", "username", "Empty username")]
        public async Task StartSessionAsync_WithInvalidUsername_ShouldThrowArgumentException(
            string username, string paramName, string scenario)
        {
            // Arrange
            var keyPair = new Ed25519KeyPair();
            var mockBuffer = new Mock<ISecureBuffer>();

            // Act & Assert
            await _service.Invoking(s => s.StartSessionAsync(username, keyPair, mockBuffer.Object))
                .Should().ThrowAsync<ArgumentException>()
                .WithParameterName(paramName);
        }

        [Fact]
        public async Task StartSessionAsync_WithNullKeyPair_ShouldThrowArgumentNullException()
        {
            // Arrange
            var mockBuffer = new Mock<ISecureBuffer>();

            // Act & Assert
            await _service.Invoking(s => s.StartSessionAsync("testuser", null, mockBuffer.Object))
                .Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("keyPair");
        }

        [Fact]
        public async Task StartSessionAsync_WithNullPrivateKeyBuffer_ShouldThrowArgumentNullException()
        {
            // Arrange
            var keyPair = new Ed25519KeyPair();

            // Act & Assert
            await _service.Invoking(s => s.StartSessionAsync("testuser", keyPair, null))
                .Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("privateKeyBuffer");
        }

        [Fact]
        public async Task StartSessionAsync_WithValidInputs_ShouldCreateSession()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair 
            { 
                PublicKey = new byte[32] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 },
                PrivateKey = new byte[32]
            };

            // Act
            var result = await _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object);

            // Assert
            result.Should().BeTrue();
            _service.CurrentState.Should().Be(SessionState.Unlocked);
            _service.IsUnlocked.Should().BeTrue();
            _service.CurrentSession.Should().NotBeNull();
            _service.CurrentSession.Username.Should().Be("testuser");
            _service.CurrentSession.PublicKey.Should().BeEquivalentTo(keyPair.PublicKey);
            _service.CurrentSession.IsLocked.Should().BeFalse();
        }

        [Fact]
        public async Task StartSessionAsync_WhenSessionAlreadyExists_ShouldReturnFalse()
        {
            // Arrange
            var mockPrivateKeyBuffer1 = new Mock<ISecureBuffer>();
            var mockPrivateKeyBuffer2 = new Mock<ISecureBuffer>();
            var keyPair1 = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };
            var keyPair2 = new Ed25519KeyPair { PublicKey = new byte[32] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 }, PrivateKey = new byte[32] };

            await _service.StartSessionAsync("user1", keyPair1, mockPrivateKeyBuffer1.Object);

            // Act
            var result = await _service.StartSessionAsync("user2", keyPair2, mockPrivateKeyBuffer2.Object);

            // Assert
            result.Should().BeFalse();
            _service.CurrentSession.Username.Should().Be("user1"); // Should keep original session
        }

        [Fact]
        public async Task StartSessionAsync_WhenExistingSessionLocked_ShouldReplaceSession()
        {
            // Arrange
            var mockPrivateKeyBuffer1 = new Mock<ISecureBuffer>();
            var mockPrivateKeyBuffer2 = new Mock<ISecureBuffer>();
            var keyPair1 = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };
            var keyPair2 = new Ed25519KeyPair { PublicKey = new byte[32] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 }, PrivateKey = new byte[32] };

            await _service.StartSessionAsync("user1", keyPair1, mockPrivateKeyBuffer1.Object);
            await _service.LockSessionAsync();

            // Act
            var result = await _service.StartSessionAsync("user2", keyPair2, mockPrivateKeyBuffer2.Object);

            // Assert
            result.Should().BeTrue();
            _service.CurrentSession.Username.Should().Be("user2");
        }

        [Fact]
        public async Task StartSessionAsync_ShouldRaiseStateChangedEvent()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };

            SessionStateChangedEventArgs receivedArgs = null;
            _service.SessionStateChanged += (sender, args) => receivedArgs = args;

            // Act
            await _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object);

            // Assert
            receivedArgs.Should().NotBeNull();
            receivedArgs.OldState.Should().Be(SessionState.Locked);
            receivedArgs.NewState.Should().Be(SessionState.Unlocked);
            receivedArgs.Reason.Should().Be("Session started");
        }

        #endregion

        #region LockSessionAsync Tests

        [Fact]
        public async Task LockSessionAsync_WhenSessionUnlocked_ShouldLockSession()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };
            await _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object);

            // Act
            await _service.LockSessionAsync();

            // Assert
            _service.CurrentState.Should().Be(SessionState.Locked);
            _service.IsUnlocked.Should().BeFalse();
            _service.CurrentSession.IsLocked.Should().BeTrue();
        }

        [Fact]
        public async Task LockSessionAsync_WhenNoSession_ShouldNotChangeState()
        {
            // Act
            await _service.LockSessionAsync();

            // Assert
            _service.CurrentState.Should().Be(SessionState.Locked);
        }

        [Fact]
        public async Task LockSessionAsync_WhenAlreadyLocked_ShouldNotChangeState()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };
            await _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object);
            await _service.LockSessionAsync();

            // Act
            await _service.LockSessionAsync();

            // Assert
            _service.CurrentState.Should().Be(SessionState.Locked);
        }

        [Fact]
        public async Task LockSessionAsync_ShouldRaiseStateChangedEvent()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };
            await _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object);

            SessionStateChangedEventArgs receivedArgs = null;
            _service.SessionStateChanged += (sender, args) => receivedArgs = args;

            // Act
            await _service.LockSessionAsync();

            // Assert
            receivedArgs.Should().NotBeNull();
            receivedArgs.OldState.Should().Be(SessionState.Unlocked);
            receivedArgs.NewState.Should().Be(SessionState.Locked);
            receivedArgs.Reason.Should().Be("Session locked");
        }

        #endregion

        #region UnlockSessionAsync Tests

        [Theory]
        [InlineData(null, "passphrase", "Null passphrase")]
        [InlineData("", "passphrase", "Empty passphrase")]
        public async Task UnlockSessionAsync_WithInvalidPassphrase_ShouldThrowArgumentException(
            string passphrase, string paramName, string scenario)
        {
            // Act & Assert
            await _service.Invoking(s => s.UnlockSessionAsync(passphrase))
                .Should().ThrowAsync<ArgumentException>()
                .WithParameterName(paramName);
        }

        [Fact]
        public async Task UnlockSessionAsync_WhenNoSession_ShouldReturnFalse()
        {
            // Act
            var result = await _service.UnlockSessionAsync("password");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task UnlockSessionAsync_WhenSessionNotLocked_ShouldReturnFalse()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };
            await _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object);

            // Act - try to unlock already unlocked session
            var result = await _service.UnlockSessionAsync("password");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task UnlockSessionAsync_WithValidCrypto_ShouldUnlockSession()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair 
            { 
                PublicKey = new byte[32] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 },
                PrivateKey = new byte[32]
            };

            await _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object);
            await _service.LockSessionAsync();

            // Setup crypto mocks
            _cryptoInteropMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(new byte[32]);

            _cryptoInteropMock.Setup(c => c.Pbkdf2Async(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(new byte[32]);

            _cryptoInteropMock.Setup(c => c.GenerateECDSAKeyPairAsync(It.IsAny<string>()))
                .ReturnsAsync(new ECDSAKeyPair 
                { 
                    PublicKey = keyPair.PublicKey, // Same public key for verification
                    PrivateKey = new byte[32] 
                });

            // Act
            var result = await _service.UnlockSessionAsync("password");

            // Assert
            result.Should().BeTrue();
            _service.CurrentState.Should().Be(SessionState.Unlocked);
            _service.IsUnlocked.Should().BeTrue();
            _service.CurrentSession.IsLocked.Should().BeFalse();
        }

        [Fact]
        public async Task UnlockSessionAsync_WithInvalidPublicKey_ShouldReturnFalse()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair 
            { 
                PublicKey = new byte[32] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 },
                PrivateKey = new byte[32]
            };

            await _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object);
            await _service.LockSessionAsync();

            // Setup crypto mocks with different public key
            _cryptoInteropMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(new byte[32]);

            _cryptoInteropMock.Setup(c => c.Pbkdf2Async(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(new byte[32]);

            _cryptoInteropMock.Setup(c => c.GenerateECDSAKeyPairAsync(It.IsAny<string>()))
                .ReturnsAsync(new ECDSAKeyPair 
                { 
                    PublicKey = new byte[32] { 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99 }, // Different public key
                    PrivateKey = new byte[32] 
                });

            // Act
            var result = await _service.UnlockSessionAsync("password");

            // Assert
            result.Should().BeFalse();
            _service.CurrentState.Should().Be(SessionState.Locked);
        }

        [Fact]
        public async Task UnlockSessionAsync_WhenCryptoThrows_ShouldReturnFalse()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };
            await _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object);
            await _service.LockSessionAsync();

            _cryptoInteropMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ThrowsAsync(new Exception("Crypto error"));

            // Act
            var result = await _service.UnlockSessionAsync("password");

            // Assert
            result.Should().BeFalse();
            _service.CurrentState.Should().Be(SessionState.Locked);
        }

        [Fact]
        public async Task UnlockSessionAsync_ShouldRaiseStateChangedEvents()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair 
            { 
                PublicKey = new byte[32] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 },
                PrivateKey = new byte[32]
            };

            await _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object);
            await _service.LockSessionAsync();

            // Setup crypto mocks
            _cryptoInteropMock.Setup(c => c.Sha256Async(It.IsAny<byte[]>()))
                .ReturnsAsync(new byte[32]);
            _cryptoInteropMock.Setup(c => c.Pbkdf2Async(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(new byte[32]);
            _cryptoInteropMock.Setup(c => c.GenerateECDSAKeyPairAsync(It.IsAny<string>()))
                .ReturnsAsync(new ECDSAKeyPair { PublicKey = keyPair.PublicKey, PrivateKey = new byte[32] });

            var stateChanges = new List<SessionStateChangedEventArgs>();
            _service.SessionStateChanged += (sender, args) => stateChanges.Add(args);

            // Act
            await _service.UnlockSessionAsync("password");

            // Assert
            stateChanges.Should().HaveCount(2);
            stateChanges[0].NewState.Should().Be(SessionState.Unlocking);
            stateChanges[1].NewState.Should().Be(SessionState.Unlocked);
        }

        #endregion

        #region EndSessionAsync Tests

        [Fact]
        public async Task EndSessionAsync_ShouldClearSessionAndChangeState()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };
            await _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object);

            // Act
            await _service.EndSessionAsync();

            // Assert
            _service.CurrentState.Should().Be(SessionState.Locked);
            _service.CurrentSession.Should().BeNull();
            _service.IsUnlocked.Should().BeFalse();
            mockPrivateKeyBuffer.Verify(b => b.Clear(), Times.Once);
        }

        [Fact]
        public async Task EndSessionAsync_WhenNoSession_ShouldNotThrow()
        {
            // Act & Assert
            await _service.Invoking(s => s.EndSessionAsync()).Should().NotThrowAsync();
            _service.CurrentState.Should().Be(SessionState.Locked);
        }

        [Fact]
        public async Task EndSessionAsync_ShouldClearPublicKeyArray()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var publicKey = new byte[32] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 };
            var keyPair = new Ed25519KeyPair { PublicKey = publicKey, PrivateKey = new byte[32] };
            await _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object);

            // Act
            await _service.EndSessionAsync();

            // Assert
            publicKey.Should().AllBeEquivalentTo((byte)0, "Public key should be cleared");
        }

        [Fact]
        public async Task EndSessionAsync_ShouldRaiseStateChangedEvent()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };
            await _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object);

            SessionStateChangedEventArgs receivedArgs = null;
            _service.SessionStateChanged += (sender, args) => receivedArgs = args;

            // Act
            await _service.EndSessionAsync();

            // Assert
            receivedArgs.Should().NotBeNull();
            receivedArgs.OldState.Should().Be(SessionState.Unlocked);
            receivedArgs.NewState.Should().Be(SessionState.Locked);
            receivedArgs.Reason.Should().Be("Session ended");
        }

        #endregion

        #region Activity Management Tests

        [Fact]
        public void UpdateActivity_WhenSessionUnlocked_ShouldUpdateLastActivity()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };
            _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object).Wait();

            var initialTime = _service.CurrentSession.LastActivityAt;
            Thread.Sleep(100); // Small delay to ensure time difference

            // Act
            _service.UpdateActivity();

            // Assert
            _service.CurrentSession.LastActivityAt.Should().BeAfter(initialTime);
        }

        [Fact]
        public void UpdateActivity_WhenSessionLocked_ShouldNotUpdateTime()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };
            _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object).Wait();
            _service.LockSessionAsync().Wait();

            var initialTime = _service.CurrentSession.LastActivityAt;
            Thread.Sleep(10);

            // Act
            _service.UpdateActivity();

            // Assert
            _service.CurrentSession.LastActivityAt.Should().Be(initialTime);
        }

        [Fact]
        public void UpdateActivity_WhenNoSession_ShouldNotThrow()
        {
            // Act & Assert
            _service.Invoking(s => s.UpdateActivity()).Should().NotThrow();
        }

        #endregion

        #region Timeout Tests

        [Fact]
        public async Task CheckTimeoutAsync_WhenSessionExpired_ShouldExpireSession()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };
            
            _service.SessionTimeoutMinutes = 0; // Immediate timeout
            await _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object);

            // Act
            await _service.CheckTimeoutAsync();

            // Assert
            _service.CurrentState.Should().Be(SessionState.Expired);
            _service.CurrentSession.IsLocked.Should().BeTrue();
        }

        [Fact]
        public async Task CheckTimeoutAsync_WhenSessionNotExpired_ShouldNotChangeState()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };
            
            _service.SessionTimeoutMinutes = 60; // Long timeout
            await _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object);

            // Act
            await _service.CheckTimeoutAsync();

            // Assert
            _service.CurrentState.Should().Be(SessionState.Unlocked);
        }

        [Fact]
        public async Task CheckTimeoutAsync_WhenNoSession_ShouldNotThrow()
        {
            // Act & Assert
            await _service.Invoking(s => s.CheckTimeoutAsync()).Should().NotThrowAsync();
        }

        [Fact]
        public async Task CheckTimeoutAsync_WhenSessionLocked_ShouldNotChangeState()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };
            await _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object);
            await _service.LockSessionAsync();

            // Act
            await _service.CheckTimeoutAsync();

            // Assert
            _service.CurrentState.Should().Be(SessionState.Locked);
        }

        #endregion

        #region GetRemainingTime Tests

        [Fact]
        public void GetRemainingTime_WhenSessionUnlocked_ShouldReturnRemainingTime()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };
            _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object).Wait();

            // Act
            var remainingTime = _service.GetRemainingTime();

            // Assert
            remainingTime.Should().BeCloseTo(TimeSpan.FromMinutes(_service.SessionTimeoutMinutes), TimeSpan.FromSeconds(1));
            remainingTime.Should().BeGreaterThan(TimeSpan.Zero);
        }

        [Fact]
        public void GetRemainingTime_WhenSessionLocked_ShouldReturnZero()
        {
            // Act
            var remainingTime = _service.GetRemainingTime();

            // Assert
            remainingTime.Should().Be(TimeSpan.Zero);
        }

        [Fact]
        public void GetRemainingTime_WhenNoSession_ShouldReturnZero()
        {
            // Act
            var remainingTime = _service.GetRemainingTime();

            // Assert
            remainingTime.Should().Be(TimeSpan.Zero);
        }

        [Fact]
        public void GetRemainingTime_WhenTimeoutPassed_ShouldReturnZero()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };
            
            _service.SessionTimeoutMinutes = 0; // Immediate timeout
            _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object).Wait();

            // Act
            var remainingTime = _service.GetRemainingTime();

            // Assert
            remainingTime.Should().Be(TimeSpan.Zero);
        }

        #endregion

        #region ExtendSessionAsync Tests

        [Fact]
        public async Task ExtendSessionAsync_WhenSessionUnlocked_ShouldUpdateActivity()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };
            await _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object);

            var initialActivity = _service.CurrentSession.LastActivityAt;
            Thread.Sleep(10);

            // Act
            await _service.ExtendSessionAsync();

            // Assert
            _service.CurrentSession.LastActivityAt.Should().BeAfter(initialActivity);
        }

        [Fact]
        public async Task ExtendSessionAsync_WhenSessionLocked_ShouldNotThrow()
        {
            // Act & Assert
            await _service.Invoking(s => s.ExtendSessionAsync()).Should().NotThrowAsync();
        }

        [Fact]
        public async Task ExtendSessionAsync_WhenNoSession_ShouldNotThrow()
        {
            // Act & Assert
            await _service.Invoking(s => s.ExtendSessionAsync()).Should().NotThrowAsync();
        }

        #endregion

        #region Thread Safety Tests

        [Fact]
        public async Task ConcurrentOperations_ShouldMaintainConsistency()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };

            // Act - Perform concurrent operations
            var tasks = new[]
            {
                Task.Run(async () => await _service.StartSessionAsync("user1", keyPair, mockPrivateKeyBuffer.Object)),
                Task.Run(async () => await _service.LockSessionAsync()),
                Task.Run(() => _service.UpdateActivity()),
                Task.Run(async () => await _service.CheckTimeoutAsync()),
                Task.Run(async () => await _service.ExtendSessionAsync())
            };

            // Assert - Should not throw exceptions
            var action = () => Task.WhenAll(tasks);
            await action.Should().NotThrowAsync();
            _service.CurrentState.Should().BeOneOf(SessionState.Locked, SessionState.Unlocked, SessionState.Expired);
        }

        [Fact]
        public void PropertyAccess_WhenDisposed_ShouldNotThrow()
        {
            // Arrange
            _service.Dispose();

            // Act & Assert
            _service.Invoking(s => _ = s.CurrentState).Should().NotThrow();
            _service.Invoking(s => _ = s.CurrentSession).Should().NotThrow();
            _service.Invoking(s => _ = s.IsUnlocked).Should().NotThrow();
            _service.Invoking(s => _ = s.GetRemainingTime()).Should().NotThrow();
        }

        #endregion

        #region Dispose Tests

        [Fact]
        public void Dispose_WhenCalledMultipleTimes_ShouldNotThrow()
        {
            // Act & Assert
            _service.Invoking(s => s.Dispose()).Should().NotThrow();
            _service.Invoking(s => s.Dispose()).Should().NotThrow();
            _service.Invoking(s => s.Dispose()).Should().NotThrow();
        }

        [Fact]
        public async Task Dispose_ShouldClearPrivateKeyBuffer()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };
            await _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object);

            // Act
            _service.Dispose();

            // Assert
            mockPrivateKeyBuffer.Verify(b => b.Clear(), Times.Once);
        }

        [Fact]
        public async Task DisposeAsync_ShouldEndSessionAndDispose()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };
            await _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object);

            // Act
            await _service.DisposeAsync();

            // Assert
            _service.CurrentSession.Should().BeNull();
            _service.CurrentState.Should().Be(SessionState.Locked);
        }

        #endregion
    }
}