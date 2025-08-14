using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Core.Cryptography.Interfaces;
using NoLock.Social.Core.Cryptography.Services;
using NoLock.Social.Core.Identity.Interfaces;
using Xunit;

namespace NoLock.Social.Core.Tests.Identity
{
    /// <summary>
    /// Integration tests for session persistence with ReactiveSessionStateService
    /// </summary>
    public class SessionPersistenceIntegrationTests
    {
        private readonly Mock<IWebCryptoService> _webCryptoServiceMock;
        private readonly Mock<ISecureMemoryManager> _secureMemoryManagerMock;
        private readonly Mock<ISessionPersistenceService> _sessionPersistenceMock;
        private readonly Mock<ILogger<ReactiveSessionStateService>> _loggerMock;
        private readonly ReactiveSessionStateService _sessionStateService;

        public SessionPersistenceIntegrationTests()
        {
            _webCryptoServiceMock = new Mock<IWebCryptoService>();
            _secureMemoryManagerMock = new Mock<ISecureMemoryManager>();
            _sessionPersistenceMock = new Mock<ISessionPersistenceService>();
            _loggerMock = new Mock<ILogger<ReactiveSessionStateService>>();

            _sessionStateService = new ReactiveSessionStateService(
                _webCryptoServiceMock.Object,
                _secureMemoryManagerMock.Object,
                _sessionPersistenceMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task StartSessionAsync_PersistsSessionData()
        {
            // Arrange
            var username = "testuser";
            var keyPair = new Ed25519KeyPair
            {
                PublicKey = new byte[] { 1, 2, 3, 4, 5 },
                PrivateKey = new byte[] { 6, 7, 8, 9, 10 }
            };

            var privateKeyBufferMock = new Mock<ISecureBuffer>();
            privateKeyBufferMock.Setup(x => x.Data).Returns(keyPair.PrivateKey);

            PersistedSessionData? capturedSessionData = null;
            _sessionPersistenceMock
                .Setup(x => x.PersistSessionAsync(
                    It.IsAny<PersistedSessionData>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<int>()))
                .Callback<PersistedSessionData, byte[], int>((data, key, timeout) =>
                {
                    capturedSessionData = data;
                })
                .ReturnsAsync(true);

            // Act
            var result = await _sessionStateService.StartSessionAsync(
                username, keyPair, privateKeyBufferMock.Object);

            // Assert
            Assert.True(result);
            Assert.NotNull(capturedSessionData);
            Assert.Equal(username, capturedSessionData.Username);
            Assert.Equal(keyPair.PublicKey, capturedSessionData.PublicKey);
            Assert.Equal(SessionState.Unlocked, capturedSessionData.State);

            _sessionPersistenceMock.Verify(x => x.PersistSessionAsync(
                It.IsAny<PersistedSessionData>(),
                It.IsAny<byte[]>(),
                It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public async Task EndSessionAsync_ClearsPersistedSession()
        {
            // Arrange
            var username = "testuser";
            var keyPair = new Ed25519KeyPair
            {
                PublicKey = new byte[] { 1, 2, 3, 4, 5 },
                PrivateKey = new byte[] { 6, 7, 8, 9, 10 }
            };

            var privateKeyBufferMock = new Mock<ISecureBuffer>();
            privateKeyBufferMock.Setup(x => x.Data).Returns(keyPair.PrivateKey);

            _sessionPersistenceMock
                .Setup(x => x.PersistSessionAsync(
                    It.IsAny<PersistedSessionData>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<int>()))
                .ReturnsAsync(true);

            _sessionPersistenceMock
                .Setup(x => x.ClearPersistedSessionAsync())
                .Returns(Task.CompletedTask);

            // Start a session first
            await _sessionStateService.StartSessionAsync(
                username, keyPair, privateKeyBufferMock.Object);

            // Act
            await _sessionStateService.EndSessionAsync();

            // Assert
            _sessionPersistenceMock.Verify(x => x.ClearPersistedSessionAsync(), Times.Once);
            Assert.Equal(SessionState.Expired, _sessionStateService.CurrentState);
        }

        [Fact]
        public async Task TryRestoreSessionAsync_NoPersistedSession_ReturnsFalse()
        {
            // Arrange
            _sessionPersistenceMock
                .Setup(x => x.HasValidPersistedSessionAsync())
                .ReturnsAsync(false);

            // Act
            var result = await _sessionStateService.TryRestoreSessionAsync();

            // Assert
            Assert.False(result);
            _sessionPersistenceMock.Verify(x => x.HasValidPersistedSessionAsync(), Times.Once);
        }

        [Fact]
        public async Task TryRestoreSessionAsync_ValidPersistedSession_RestoresInLockedState()
        {
            // Arrange
            var encryptedSession = new EncryptedSessionData
            {
                EncryptedPayload = "",
                IntegrityCheck = "test-hash",
                Metadata = new SessionMetadata
                {
                    SessionId = "restored-session",
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                    Version = 2
                }
            };

            _sessionPersistenceMock
                .Setup(x => x.HasValidPersistedSessionAsync())
                .ReturnsAsync(true);

            _sessionPersistenceMock
                .Setup(x => x.GetPersistedSessionAsync())
                .ReturnsAsync(encryptedSession);

            _sessionPersistenceMock
                .Setup(x => x.GetRemainingSessionTimeAsync())
                .ReturnsAsync(TimeSpan.FromMinutes(30));

            // Act
            var result = await _sessionStateService.TryRestoreSessionAsync();

            // Assert
            Assert.True(result);
            Assert.Equal(SessionState.Locked, _sessionStateService.CurrentState);
            Assert.NotNull(_sessionStateService.CurrentSession);
            Assert.Equal(encryptedSession.Metadata.SessionId, _sessionStateService.CurrentSession.SessionId);

            _sessionPersistenceMock.Verify(x => x.HasValidPersistedSessionAsync(), Times.Once);
            _sessionPersistenceMock.Verify(x => x.GetPersistedSessionAsync(), Times.Once);
        }

        [Fact]
        public async Task ExtendSessionAsync_ExtendsPersistedSession()
        {
            // Arrange
            var username = "testuser";
            var keyPair = new Ed25519KeyPair
            {
                PublicKey = new byte[] { 1, 2, 3, 4, 5 },
                PrivateKey = new byte[] { 6, 7, 8, 9, 10 }
            };

            var privateKeyBufferMock = new Mock<ISecureBuffer>();
            privateKeyBufferMock.Setup(x => x.Data).Returns(keyPair.PrivateKey);

            _sessionPersistenceMock
                .Setup(x => x.PersistSessionAsync(
                    It.IsAny<PersistedSessionData>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<int>()))
                .ReturnsAsync(true);

            _sessionPersistenceMock
                .Setup(x => x.ExtendSessionExpiryAsync(It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            // Start a session first
            await _sessionStateService.StartSessionAsync(
                username, keyPair, privateKeyBufferMock.Object);

            // Act
            await _sessionStateService.ExtendSessionAsync();

            // Assert
            _sessionPersistenceMock.Verify(x => x.ExtendSessionExpiryAsync(
                _sessionStateService.SessionTimeoutMinutes), Times.Once);
        }

        [Fact]
        public async Task SessionStateChanges_EmitEventsOnRestore()
        {
            // Arrange
            var encryptedSession = new EncryptedSessionData
            {
                Metadata = new SessionMetadata
                {
                    SessionId = "test-session",
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                    Version = 2
                }
            };

            _sessionPersistenceMock
                .Setup(x => x.HasValidPersistedSessionAsync())
                .ReturnsAsync(true);

            _sessionPersistenceMock
                .Setup(x => x.GetPersistedSessionAsync())
                .ReturnsAsync(encryptedSession);

            _sessionPersistenceMock
                .Setup(x => x.GetRemainingSessionTimeAsync())
                .ReturnsAsync(TimeSpan.FromMinutes(30));

            SessionStateChangedEventArgs? capturedArgs = null;
            _sessionStateService.SessionStateChanged += (sender, args) =>
            {
                capturedArgs = args;
            };

            // Act
            await _sessionStateService.TryRestoreSessionAsync();

            // Assert
            Assert.NotNull(capturedArgs);
            Assert.Equal(SessionState.Locked, capturedArgs.NewState);
            Assert.Contains("restored", capturedArgs.Reason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SessionTimeout_DoesNotAffectPersistedSession()
        {
            // Arrange
            var username = "testuser";
            var keyPair = new Ed25519KeyPair
            {
                PublicKey = new byte[] { 1, 2, 3, 4, 5 },
                PrivateKey = new byte[] { 6, 7, 8, 9, 10 }
            };

            var privateKeyBufferMock = new Mock<ISecureBuffer>();
            privateKeyBufferMock.Setup(x => x.Data).Returns(keyPair.PrivateKey);

            _sessionPersistenceMock
                .Setup(x => x.PersistSessionAsync(
                    It.IsAny<PersistedSessionData>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<int>()))
                .ReturnsAsync(true);

            // Set a very short timeout for testing
            _sessionStateService.SessionTimeoutMinutes = 0;

            // Start a session
            await _sessionStateService.StartSessionAsync(
                username, keyPair, privateKeyBufferMock.Object);

            // Act - trigger timeout check
            await _sessionStateService.CheckTimeoutAsync();

            // Assert - session should be locked due to timeout
            Assert.Equal(SessionState.Locked, _sessionStateService.CurrentState);
            
            // But persistence service should not be cleared
            _sessionPersistenceMock.Verify(x => x.ClearPersistedSessionAsync(), Times.Never);
        }
    }
}