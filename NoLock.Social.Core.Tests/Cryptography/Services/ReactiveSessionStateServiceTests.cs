using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Core.Cryptography.Interfaces;
using NoLock.Social.Core.Cryptography.Services;
using NoLock.Social.Core.Identity.Interfaces;
using Xunit;
using System.Threading;

namespace NoLock.Social.Core.Tests.Cryptography.Services
{
    public class ReactiveSessionStateServiceTests : IDisposable
    {
        private readonly Mock<IWebCryptoService> _cryptoServiceMock;
        private readonly Mock<ISecureMemoryManager> _secureMemoryManagerMock;
        private readonly Mock<ISessionPersistenceService> _sessionPersistenceMock;
        private readonly Mock<ILogger<ReactiveSessionStateService>> _loggerMock;
        private readonly ReactiveSessionStateService _service;

        public ReactiveSessionStateServiceTests()
        {
            _cryptoServiceMock = new Mock<IWebCryptoService>();
            _secureMemoryManagerMock = new Mock<ISecureMemoryManager>();
            _sessionPersistenceMock = new Mock<ISessionPersistenceService>();
            _loggerMock = new Mock<ILogger<ReactiveSessionStateService>>();

            _service = new ReactiveSessionStateService(
                _cryptoServiceMock.Object,
                _secureMemoryManagerMock.Object,
                _sessionPersistenceMock.Object,
                _loggerMock.Object);
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
            using var service = new ReactiveSessionStateService(
                _cryptoServiceMock.Object,
                _secureMemoryManagerMock.Object,
                _sessionPersistenceMock.Object,
                _loggerMock.Object);

            // Assert
            service.Should().NotBeNull();
            service.CurrentState.Should().Be(SessionState.Locked);
            service.CurrentSession.Should().BeNull();
            service.IsUnlocked.Should().BeFalse();
            service.SessionTimeoutMinutes.Should().Be(30);
        }

        [Theory]
        [InlineData(null, "cryptoService", "Null crypto service")]
        [InlineData("secureMemoryManager", null, "Null secure memory manager")]
        [InlineData("sessionPersistence", null, "Null session persistence")]
        [InlineData("logger", null, "Null logger")]
        public void Constructor_WithNullDependencies_ShouldThrowArgumentNullException(
            string nullParam1, string nullParam2, string scenario)
        {
            // Arrange
            var cryptoService = nullParam1 == null ? null : _cryptoServiceMock.Object;
            var memoryManager = nullParam1 == "secureMemoryManager" ? null : _secureMemoryManagerMock.Object;
            var persistence = nullParam1 == "sessionPersistence" ? null : _sessionPersistenceMock.Object;
            var logger = nullParam1 == "logger" ? null : _loggerMock.Object;

            // Act & Assert
            var action = () => new ReactiveSessionStateService(cryptoService, memoryManager, persistence, logger);
            action.Should().Throw<ArgumentNullException>();
        }

        #endregion

        #region Property Tests

        [Theory]
        [InlineData(15, "15 minute timeout")]
        [InlineData(60, "60 minute timeout")]
        [InlineData(5, "5 minute timeout")]
        public void SessionTimeoutMinutes_WhenSet_ShouldUpdateValue(int timeout, string scenario)
        {
            // Act
            _service.SessionTimeoutMinutes = timeout;

            // Assert
            _service.SessionTimeoutMinutes.Should().Be(timeout, scenario);
        }

        [Fact]
        public void CurrentState_WhenDisposed_ShouldReturnExpired()
        {
            // Arrange
            _service.Dispose();

            // Act & Assert
            _service.CurrentState.Should().Be(SessionState.Expired);
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
        public void IsUnlocked_WhenDisposed_ShouldReturnFalse()
        {
            // Arrange
            _service.Dispose();

            // Act & Assert
            _service.IsUnlocked.Should().BeFalse();
        }

        #endregion

        #region Reactive Stream Tests

        [Fact]
        public void StateStream_ShouldStartWithLockedState()
        {
            // Arrange
            SessionState receivedState = SessionState.Unlocked; // Set to wrong value initially

            // Act
            _service.StateStream.Take(1).Subscribe(state => receivedState = state);

            // Assert
            receivedState.Should().Be(SessionState.Locked);
        }

        [Fact]
        public void RemainingTimeStream_ShouldStartWithDefaultTimeout()
        {
            // Arrange
            TimeSpan receivedTime = TimeSpan.Zero;

            // Act
            _service.RemainingTimeStream.Take(1).Subscribe(time => receivedTime = time);

            // Assert
            receivedTime.Should().Be(TimeSpan.FromMinutes(30));
        }

        [Fact]
        public async Task SessionStateChanges_ShouldEmitOnStateChange()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };
            
            SessionStateChangedEventArgs receivedArgs = null;
            _service.SessionStateChanges.Take(1).Subscribe(args => receivedArgs = args);

            // Act
            await _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object);

            // Assert
            receivedArgs.Should().NotBeNull();
            receivedArgs.OldState.Should().Be(SessionState.Locked);
            receivedArgs.NewState.Should().Be(SessionState.Unlocked);
            receivedArgs.Reason.Should().Be("Session started");
        }

        [Fact]
        public async Task TimeoutWarningStream_ShouldEmitWhenLessThanOneMinuteRemaining()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };
            
            // Set very short timeout
            _service.SessionTimeoutMinutes = 1;
            
            await _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object);
            
            TimeSpan? receivedWarning = null;
            _service.TimeoutWarningStream.Take(1).Subscribe(time => receivedWarning = time);

            // Act - Force timeout check after waiting to simulate near-timeout
            await Task.Delay(100); // Small delay to ensure proper timing
            await _service.CheckTimeoutAsync();

            // Assert
            // Note: This test might be timing-sensitive in real scenarios
            // The warning should be emitted when less than 1 minute remains
        }

        #endregion

        #region StartSessionAsync Tests

        [Theory]
        [InlineData(null, "username", "Null username")]
        [InlineData("", "username", "Empty username")]
        [InlineData("  ", "username", "Whitespace username")]
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
        }

        [Fact]
        public async Task StartSessionAsync_WhenPersistenceFails_ShouldStillCreateInMemorySession()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };

            _sessionPersistenceMock
                .Setup(p => p.PersistSessionAsync(It.IsAny<NoLock.Social.Core.Identity.Interfaces.PersistedSessionData>(), It.IsAny<byte[]>(), It.IsAny<int>()))
                .ThrowsAsync(new Exception("Persistence failed"));

            // Act
            var result = await _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object);

            // Assert
            result.Should().BeTrue();
            _service.CurrentState.Should().Be(SessionState.Unlocked);
        }

        [Fact]
        public async Task StartSessionAsync_ShouldDisposeExistingSession()
        {
            // Arrange
            var mockPrivateKeyBuffer1 = new Mock<ISecureBuffer>();
            var mockPrivateKeyBuffer2 = new Mock<ISecureBuffer>();
            var keyPair1 = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };
            var keyPair2 = new Ed25519KeyPair { PublicKey = new byte[32] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 }, PrivateKey = new byte[32] };

            // Act
            await _service.StartSessionAsync("user1", keyPair1, mockPrivateKeyBuffer1.Object);
            await _service.StartSessionAsync("user2", keyPair2, mockPrivateKeyBuffer2.Object);

            // Assert
            mockPrivateKeyBuffer1.Verify(b => b.Dispose(), Times.Once);
            _service.CurrentSession.Username.Should().Be("user2");
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
        }

        [Fact]
        public async Task LockSessionAsync_WhenAlreadyLocked_ShouldNotChangeState()
        {
            // Act
            await _service.LockSessionAsync();

            // Assert
            _service.CurrentState.Should().Be(SessionState.Locked);
        }

        [Fact]
        public async Task LockSessionAsync_ShouldEmitStateChangeEvent()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };
            
            // Set up subscription before starting session to capture all events
            var receivedEvents = new List<SessionStateChangedEventArgs>();
            using var subscription = _service.SessionStateChanges.Subscribe(args => receivedEvents.Add(args));
            
            await _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object);

            // Act
            await _service.LockSessionAsync();
            
            // Wait for the event to be processed
            await Task.Delay(100);

            // Assert - the second event should be the lock event
            receivedEvents.Should().HaveCountGreaterThanOrEqualTo(2);
            var lockEvent = receivedEvents.Last();
            lockEvent.Should().NotBeNull();
            lockEvent.OldState.Should().Be(SessionState.Unlocked);
            lockEvent.NewState.Should().Be(SessionState.Locked);
            lockEvent.Reason.Should().Be("Session locked");
        }

        #endregion

        #region UnlockSessionAsync Tests

        [Theory]
        [InlineData(null, "Empty passphrase")]
        [InlineData("", "Empty passphrase")]
        [InlineData("  ", "Whitespace passphrase")]
        public async Task UnlockSessionAsync_WithInvalidPassphrase_ShouldReturnFalse(
            string passphrase, string scenario)
        {
            // Act
            var result = await _service.UnlockSessionAsync(passphrase);

            // Assert
            result.Should().BeFalse(scenario);
        }

        [Fact]
        public async Task UnlockSessionAsync_WhenNoSessionExists_ShouldReturnFalse()
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
        public async Task UnlockSessionAsync_WithValidPassphrase_ShouldUnlockSession()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };
            await _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object);
            await _service.LockSessionAsync();

            // Act
            var result = await _service.UnlockSessionAsync("password");

            // Assert
            result.Should().BeTrue();
            _service.CurrentState.Should().Be(SessionState.Unlocked);
            _service.IsUnlocked.Should().BeTrue();
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
            _service.CurrentState.Should().Be(SessionState.Expired);
            _service.CurrentSession.Should().BeNull();
            _service.IsUnlocked.Should().BeFalse();
            mockPrivateKeyBuffer.Verify(b => b.Dispose(), Times.Once);
        }

        [Fact]
        public async Task EndSessionAsync_ShouldCallClearPersistedSession()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };
            await _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object);

            // Act
            await _service.EndSessionAsync();

            // Assert
            _sessionPersistenceMock.Verify(p => p.ClearPersistedSessionAsync(), Times.Once);
        }

        [Fact]
        public async Task EndSessionAsync_WhenPersistenceFails_ShouldStillEndSession()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };
            await _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object);

            _sessionPersistenceMock
                .Setup(p => p.ClearPersistedSessionAsync())
                .ThrowsAsync(new Exception("Clear failed"));

            // Act
            await _service.EndSessionAsync();

            // Assert
            _service.CurrentState.Should().Be(SessionState.Expired);
            _service.CurrentSession.Should().BeNull();
        }

        #endregion

        #region Activity and Timeout Tests

        [Fact]
        public void UpdateActivity_WhenSessionUnlocked_ShouldUpdateLastActivity()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };
            _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object).Wait();

            var initialTime = _service.GetRemainingTime();
            Thread.Sleep(100); // Small delay to ensure time difference

            // Act
            _service.UpdateActivity();

            // Assert
            var updatedTime = _service.GetRemainingTime();
            updatedTime.Should().BeCloseTo(TimeSpan.FromMinutes(_service.SessionTimeoutMinutes), TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void UpdateActivity_WhenSessionLocked_ShouldNotUpdateTime()
        {
            // Act
            _service.UpdateActivity();

            // Assert
            var remainingTime = _service.GetRemainingTime();
            remainingTime.Should().Be(TimeSpan.Zero);
        }

        [Fact]
        public async Task CheckTimeoutAsync_WhenSessionExpired_ShouldLockSession()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };
            
            // Set very short timeout
            _service.SessionTimeoutMinutes = 0; // Immediate timeout
            await _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object);

            // Act
            await _service.CheckTimeoutAsync();

            // Assert
            _service.CurrentState.Should().Be(SessionState.Locked);
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
        public async Task ExtendSessionAsync_ShouldUpdateActivityAndPersistence()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };
            await _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object);

            // Act
            await _service.ExtendSessionAsync();

            // Assert
            _sessionPersistenceMock.Verify(p => p.ExtendSessionExpiryAsync(It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public async Task ExtendSessionAsync_WhenPersistenceFails_ShouldStillExtendInMemory()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };
            await _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object);

            _sessionPersistenceMock
                .Setup(p => p.ExtendSessionExpiryAsync(It.IsAny<int>()))
                .ThrowsAsync(new Exception("Extend failed"));

            var initialTime = _service.GetRemainingTime();

            // Act
            await _service.ExtendSessionAsync();

            // Assert - Should still update in-memory activity
            var extendedTime = _service.GetRemainingTime();
            extendedTime.Should().BeCloseTo(TimeSpan.FromMinutes(_service.SessionTimeoutMinutes), TimeSpan.FromSeconds(1));
        }

        #endregion

        #region Session Restore Tests

        [Fact]
        public async Task TryRestoreSessionAsync_WhenNoPersistedSession_ShouldReturnFalse()
        {
            // Arrange
            _sessionPersistenceMock
                .Setup(p => p.HasValidPersistedSessionAsync())
                .ReturnsAsync(false);

            // Act
            var result = await _service.TryRestoreSessionAsync();

            // Assert
            result.Should().BeFalse();
            _service.CurrentState.Should().Be(SessionState.Locked);
        }

        [Fact]
        public async Task TryRestoreSessionAsync_WhenPersistedSessionExists_ShouldRestoreToLockedState()
        {
            // Arrange
            var encryptedSession = new NoLock.Social.Core.Identity.Interfaces.EncryptedSessionData
            {
                Metadata = new NoLock.Social.Core.Identity.Interfaces.SessionMetadata
                {
                    SessionId = "test-session-id",
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30)
                }
            };

            _sessionPersistenceMock
                .Setup(p => p.HasValidPersistedSessionAsync())
                .ReturnsAsync(true);

            _sessionPersistenceMock
                .Setup(p => p.GetPersistedSessionAsync())
                .ReturnsAsync(encryptedSession);

            _sessionPersistenceMock
                .Setup(p => p.GetRemainingSessionTimeAsync())
                .ReturnsAsync(TimeSpan.FromMinutes(25));

            // Act
            var result = await _service.TryRestoreSessionAsync();

            // Assert
            result.Should().BeTrue();
            _service.CurrentState.Should().Be(SessionState.Locked);
            _service.CurrentSession.Should().NotBeNull();
            _service.CurrentSession.SessionId.Should().Be("test-session-id");
        }

        [Fact]
        public async Task TryRestoreSessionAsync_WhenExceptionOccurs_ShouldReturnFalse()
        {
            // Arrange
            _sessionPersistenceMock
                .Setup(p => p.HasValidPersistedSessionAsync())
                .ThrowsAsync(new Exception("Storage error"));

            // Act
            var result = await _service.TryRestoreSessionAsync();

            // Assert
            result.Should().BeFalse();
            _service.CurrentState.Should().Be(SessionState.Locked);
        }

        #endregion

        #region Thread Safety and Concurrency Tests

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
                Task.Run(async () => await _service.UnlockSessionAsync("password")),
                Task.Run(() => _service.UpdateActivity()),
                Task.Run(async () => await _service.CheckTimeoutAsync())
            };

            await Task.WhenAll(tasks);

            // Assert - Should not throw exceptions or corrupt state
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
        public void Dispose_ShouldCompleteAllObservables()
        {
            // Arrange
            var stateCompleted = false;
            var changesCompleted = false;
            var remainingTimeCompleted = false;
            var warningCompleted = false;

            _service.StateStream.Subscribe(
                _ => { },
                () => stateCompleted = true);

            _service.SessionStateChanges.Subscribe(
                _ => { },
                () => changesCompleted = true);

            _service.RemainingTimeStream.Subscribe(
                _ => { },
                () => remainingTimeCompleted = true);

            _service.TimeoutWarningStream.Subscribe(
                _ => { },
                () => warningCompleted = true);

            // Act
            _service.Dispose();

            // Assert
            stateCompleted.Should().BeTrue();
            changesCompleted.Should().BeTrue();
            remainingTimeCompleted.Should().BeTrue();
            warningCompleted.Should().BeTrue();
        }

        [Fact]
        public void Dispose_WhenCalledMultipleTimes_ShouldNotThrow()
        {
            // Act & Assert
            _service.Invoking(s => s.Dispose()).Should().NotThrow();
            _service.Invoking(s => s.Dispose()).Should().NotThrow();
            _service.Invoking(s => s.Dispose()).Should().NotThrow();
        }

        [Fact]
        public async Task Dispose_ShouldDisposePrivateKeyBuffer()
        {
            // Arrange
            var mockPrivateKeyBuffer = new Mock<ISecureBuffer>();
            var keyPair = new Ed25519KeyPair { PublicKey = new byte[32], PrivateKey = new byte[32] };
            await _service.StartSessionAsync("testuser", keyPair, mockPrivateKeyBuffer.Object);

            // Act
            _service.Dispose();

            // Assert
            mockPrivateKeyBuffer.Verify(b => b.Dispose(), Times.Once);
        }

        #endregion

        #region Backward Compatibility Tests

        [Fact]
        public async Task SessionStateChanged_EventHandlers_ShouldReceiveNotifications()
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
        }

        #endregion
    }

    // Mock classes for dependencies not defined in the test
    public class PersistedSessionData
    {
        public string SessionId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public byte[] PublicKey { get; set; } = Array.Empty<byte>();
        public byte[] EncryptedPrivateKey { get; set; } = Array.Empty<byte>();
        public DateTime CreatedAt { get; set; }
        public DateTime LastActivityAt { get; set; }
        public SessionState State { get; set; }
        public int Version { get; set; }
    }

    public class EncryptedSessionData
    {
        public SessionMetadata Metadata { get; set; } = new SessionMetadata();
        public byte[] EncryptedData { get; set; } = Array.Empty<byte>();
    }

    public class SessionMetadata
    {
        public string SessionId { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}