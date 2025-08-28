using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using NoLock.Social.Core.Common.Extensions;
using NoLock.Social.Core.Cryptography.Interfaces;
using NoLock.Social.Core.Identity.Configuration;
using NoLock.Social.Core.Identity.Interfaces;
using NoLock.Social.Core.Identity.Services;
using Xunit;

namespace NoLock.Social.Core.Tests.Identity.Services
{
    public class SessionPersistenceServiceTests
    {
        private readonly Mock<IJSRuntime> _jsRuntimeMock;
        private readonly Mock<IWebCryptoService> _webCryptoServiceMock;
        private readonly Mock<ISecureMemoryManager> _secureMemoryManagerMock;
        private readonly Mock<ILogger<SessionPersistenceService>> _loggerMock;
        private readonly SessionPersistenceService _service;

        public SessionPersistenceServiceTests()
        {
            _jsRuntimeMock = new Mock<IJSRuntime>();
            _webCryptoServiceMock = new Mock<IWebCryptoService>();
            _secureMemoryManagerMock = new Mock<ISecureMemoryManager>();
            _loggerMock = new Mock<ILogger<SessionPersistenceService>>();
            
            _service = new SessionPersistenceService(
                _jsRuntimeMock.Object,
                _webCryptoServiceMock.Object,
                _secureMemoryManagerMock.Object,
                _loggerMock.Object
            );
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_ShouldThrowArgumentNullException_WhenJSRuntimeIsNull()
        {
            // Act & Assert
            var act = () => new SessionPersistenceService(
                null!,
                _webCryptoServiceMock.Object,
                _secureMemoryManagerMock.Object,
                _loggerMock.Object
            );

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("jsRuntime");
        }

        [Fact]
        public void Constructor_ShouldThrowArgumentNullException_WhenWebCryptoServiceIsNull()
        {
            // Act & Assert
            var act = () => new SessionPersistenceService(
                _jsRuntimeMock.Object,
                null!,
                _secureMemoryManagerMock.Object,
                _loggerMock.Object
            );

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("webCryptoService");
        }

        [Fact]
        public void Constructor_ShouldThrowArgumentNullException_WhenSecureMemoryManagerIsNull()
        {
            // Act & Assert
            var act = () => new SessionPersistenceService(
                _jsRuntimeMock.Object,
                _webCryptoServiceMock.Object,
                null!,
                _loggerMock.Object
            );

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("secureMemoryManager");
        }

        [Fact]
        public void Constructor_ShouldThrowArgumentNullException_WhenLoggerIsNull()
        {
            // Act & Assert
            var act = () => new SessionPersistenceService(
                _jsRuntimeMock.Object,
                _webCryptoServiceMock.Object,
                _secureMemoryManagerMock.Object,
                null!
            );

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("logger");
        }

        #endregion

        #region PersistSessionAsync Tests

        [Fact]
        public async Task PersistSessionAsync_ShouldThrowArgumentNullException_WhenSessionDataIsNull()
        {
            // Arrange
            byte[] encryptionKey = new byte[32];

            // Act
            var act = async () => await _service.PersistSessionAsync(null!, encryptionKey);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("sessionData");
        }

        [Fact]
        public async Task PersistSessionAsync_ShouldThrowArgumentException_WhenEncryptionKeyIsNull()
        {
            // Arrange
            var sessionData = CreateTestSessionData();

            // Act
            var act = async () => await _service.PersistSessionAsync(sessionData, null!);

            // Assert
            await act.Should().ThrowAsync<ArgumentException>()
                .WithParameterName("encryptionKey");
        }

        [Fact]
        public async Task PersistSessionAsync_ShouldThrowArgumentException_WhenEncryptionKeyIsEmpty()
        {
            // Arrange
            var sessionData = CreateTestSessionData();
            byte[] emptyKey = Array.Empty<byte>();

            // Act
            var act = async () => await _service.PersistSessionAsync(sessionData, emptyKey);

            // Assert
            await act.Should().ThrowAsync<ArgumentException>()
                .WithParameterName("encryptionKey");
        }

        [Theory]
        [InlineData(30, 30)]
        [InlineData(60, 60)]
        [InlineData(120, 120)]
        [InlineData(500, 240)] // Should cap at max (240 minutes)
        public async Task PersistSessionAsync_ShouldRespectMaxExpiryTime(int requestedMinutes, int expectedMinutes)
        {
            // Arrange
            var sessionData = CreateTestSessionData();
            var encryptionKey = new byte[32];
            
            _jsRuntimeMock
                .Setup(x => x.InvokeVoidAsync(It.IsAny<string>(), It.IsAny<object?[]?>()))
                .Returns(ValueTask.CompletedTask);

            // Act
            var result = await _service.PersistSessionAsync(sessionData, encryptionKey, requestedMinutes);

            // Assert
            result.Should().BeTrue();
            sessionData.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(expectedMinutes), TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task PersistSessionAsync_ShouldStoreInSessionStorage_WhenConfiguredToUseSessionStorage()
        {
            // Arrange
            var sessionData = CreateTestSessionData();
            var encryptionKey = new byte[32];
            
            _jsRuntimeMock
                .Setup(x => x.InvokeVoidAsync(It.IsAny<string>(), It.IsAny<object?[]?>()))
                .Returns(ValueTask.CompletedTask);

            // Act
            var result = await _service.PersistSessionAsync(sessionData, encryptionKey);

            // Assert
            result.Should().BeTrue();
            
            var storageType = SessionPersistenceConfiguration.UseSessionStorage ? "sessionStorage" : "localStorage";
            _jsRuntimeMock.Verify(x => x.InvokeVoidAsync(
                $"{storageType}.setItem",
                It.IsAny<object?[]?>()),
                Times.Exactly(2)); // Once for data, once for metadata
        }

        [Fact]
        public async Task PersistSessionAsync_ShouldGenerateSaltAndIV()
        {
            // Arrange
            var sessionData = CreateTestSessionData();
            var encryptionKey = new byte[32];
            
            _jsRuntimeMock
                .Setup(x => x.InvokeVoidAsync(It.IsAny<string>(), It.IsAny<object?[]?>()))
                .Returns(ValueTask.CompletedTask);

            // Act
            var result = await _service.PersistSessionAsync(sessionData, encryptionKey);

            // Assert
            result.Should().BeTrue();
            sessionData.Salt.Should().NotBeNullOrEmpty();
            sessionData.Salt.Length.Should().Be(SessionPersistenceConfiguration.SaltSize);
            sessionData.IV.Should().NotBeNullOrEmpty();
            sessionData.IV.Length.Should().Be(SessionPersistenceConfiguration.IVSize);
        }

        [Fact]
        public async Task PersistSessionAsync_ShouldReturnFalse_WhenStorageFails()
        {
            // Arrange
            var sessionData = CreateTestSessionData();
            var encryptionKey = new byte[32];
            
            _jsRuntimeMock
                .Setup(x => x.InvokeVoidAsync(It.IsAny<string>(), It.IsAny<object?[]?>()))
                .ThrowsAsync(new JSException("Storage error"));

            // Act
            var result = await _service.PersistSessionAsync(sessionData, encryptionKey);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region GetPersistedSessionAsync Tests

        [Fact]
        public async Task GetPersistedSessionAsync_ShouldReturnNull_WhenNoSessionStored()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(It.IsAny<string>(), It.IsAny<object?[]?>()))
                .ReturnsAsync((string?)null);

            // Act
            var result = await _service.GetPersistedSessionAsync();

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetPersistedSessionAsync_ShouldReturnNull_WhenSessionExpired()
        {
            // Arrange
            var expiredSession = new EncryptedSessionData
            {
                EncryptedPayload = "test",
                IntegrityCheck = "test",
                Metadata = new SessionMetadata
                {
                    SessionId = "test",
                    ExpiresAt = DateTime.UtcNow.AddHours(-1),
                    Version = 1
                }
            };
            
            var json = JsonSerializer.Serialize(expiredSession, SessionPersistenceConfiguration.JsonOptions);
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(It.IsAny<string>(), It.IsAny<object?[]?>()))
                .ReturnsAsync(json);
            
            _jsRuntimeMock
                .Setup(x => x.InvokeVoidAsync(It.IsAny<string>(), It.IsAny<object?[]?>()))
                .Returns(ValueTask.CompletedTask);

            // Act
            var result = await _service.GetPersistedSessionAsync();

            // Assert
            result.Should().BeNull();
            _jsRuntimeMock.Verify(x => x.InvokeVoidAsync(
                It.Is<string>(s => s.Contains(".removeItem")),
                It.IsAny<object?[]?>()),
                Times.Exactly(2)); // Clear both storage keys
        }

        [Fact]
        public async Task GetPersistedSessionAsync_ShouldReturnSession_WhenValidSessionExists()
        {
            // Arrange
            var validSession = new EncryptedSessionData
            {
                EncryptedPayload = "encrypted_data",
                IntegrityCheck = "hmac_value",
                Metadata = new SessionMetadata
                {
                    SessionId = "session123",
                    ExpiresAt = DateTime.UtcNow.AddHours(1),
                    Version = 1
                }
            };
            
            var json = JsonSerializer.Serialize(validSession, SessionPersistenceConfiguration.JsonOptions);
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(It.IsAny<string>(), It.IsAny<object?[]?>()))
                .ReturnsAsync(json);

            // Act
            var result = await _service.GetPersistedSessionAsync();

            // Assert
            result.Should().NotBeNull();
            result!.EncryptedPayload.Should().Be("encrypted_data");
            result.IntegrityCheck.Should().Be("hmac_value");
            result.Metadata.SessionId.Should().Be("session123");
        }

        [Fact]
        public async Task GetPersistedSessionAsync_ShouldReturnNull_WhenDeserializationFails()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(It.IsAny<string>(), It.IsAny<object?[]?>()))
                .ReturnsAsync("invalid json");

            // Act
            var result = await _service.GetPersistedSessionAsync();

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region DecryptSessionAsync Tests

        [Fact]
        public async Task DecryptSessionAsync_ShouldThrowArgumentNullException_WhenEncryptedDataIsNull()
        {
            // Arrange
            byte[] decryptionKey = new byte[32];

            // Act
            var act = async () => await _service.DecryptSessionAsync(null!, decryptionKey);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("encryptedData");
        }

        [Fact]
        public async Task DecryptSessionAsync_ShouldThrowArgumentException_WhenDecryptionKeyIsNull()
        {
            // Arrange
            var encryptedData = new EncryptedSessionData();

            // Act
            var act = async () => await _service.DecryptSessionAsync(encryptedData, null!);

            // Assert
            await act.Should().ThrowAsync<ArgumentException>()
                .WithParameterName("decryptionKey");
        }

        [Fact]
        public async Task DecryptSessionAsync_ShouldReturnNull_CurrentImplementationIsSimplified()
        {
            // Arrange
            var encryptedData = new EncryptedSessionData
            {
                EncryptedPayload = Convert.ToBase64String(new byte[32]),
                IntegrityCheck = Convert.ToBase64String(new byte[32])
            };
            var decryptionKey = new byte[32];

            // Act
            var result = await _service.DecryptSessionAsync(encryptedData, decryptionKey);

            // Assert
            result.Should().BeNull(); // Current implementation returns null
        }

        #endregion

        #region ClearPersistedSessionAsync Tests

        [Fact]
        public async Task ClearPersistedSessionAsync_ShouldRemoveBothStorageKeys()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeVoidAsync(It.IsAny<string>(), It.IsAny<object?[]?>()))
                .Returns(ValueTask.CompletedTask);

            // Act
            await _service.ClearPersistedSessionAsync();

            // Assert
            var storageType = SessionPersistenceConfiguration.UseSessionStorage ? "sessionStorage" : "localStorage";
            _jsRuntimeMock.Verify(x => x.InvokeVoidAsync(
                $"{storageType}.removeItem",
                It.Is<object?[]?>(args => args!.Contains(SessionPersistenceConfiguration.SessionStorageKey))),
                Times.Once);
            
            _jsRuntimeMock.Verify(x => x.InvokeVoidAsync(
                $"{storageType}.removeItem",
                It.Is<object?[]?>(args => args!.Contains(SessionPersistenceConfiguration.SessionMetadataKey))),
                Times.Once);
        }

        [Fact]
        public async Task ClearPersistedSessionAsync_ShouldNotThrow_WhenClearFails()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeVoidAsync(It.IsAny<string>(), It.IsAny<object?[]?>()))
                .ThrowsAsync(new JSException("Storage error"));

            // Act
            var act = async () => await _service.ClearPersistedSessionAsync();

            // Assert
            await act.Should().NotThrowAsync();
        }

        #endregion

        #region HasValidPersistedSessionAsync Tests

        [Fact]
        public async Task HasValidPersistedSessionAsync_ShouldReturnTrue_WhenValidSessionExists()
        {
            // Arrange
            var validSession = new EncryptedSessionData
            {
                Metadata = new SessionMetadata
                {
                    ExpiresAt = DateTime.UtcNow.AddHours(1)
                }
            };
            
            var json = JsonSerializer.Serialize(validSession, SessionPersistenceConfiguration.JsonOptions);
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(It.IsAny<string>(), It.IsAny<object?[]?>()))
                .ReturnsAsync(json);

            // Act
            var result = await _service.HasValidPersistedSessionAsync();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task HasValidPersistedSessionAsync_ShouldReturnFalse_WhenNoSessionExists()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(It.IsAny<string>(), It.IsAny<object?[]?>()))
                .ReturnsAsync((string?)null);

            // Act
            var result = await _service.HasValidPersistedSessionAsync();

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task HasValidPersistedSessionAsync_ShouldReturnFalse_WhenExceptionOccurs()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(It.IsAny<string>(), It.IsAny<object?[]?>()))
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _service.HasValidPersistedSessionAsync();

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region ExtendSessionExpiryAsync Tests

        [Fact]
        public async Task ExtendSessionExpiryAsync_ShouldExtendExpiry_WhenSessionExists()
        {
            // Arrange
            var session = new EncryptedSessionData
            {
                Metadata = new SessionMetadata
                {
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30)
                }
            };
            
            var json = JsonSerializer.Serialize(session, SessionPersistenceConfiguration.JsonOptions);
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(It.IsAny<string>(), It.IsAny<object?[]?>()))
                .ReturnsAsync(json);
            
            _jsRuntimeMock
                .Setup(x => x.InvokeVoidAsync(It.IsAny<string>(), It.IsAny<object?[]?>()))
                .Returns(ValueTask.CompletedTask);

            // Act
            await _service.ExtendSessionExpiryAsync(30);

            // Assert
            _jsRuntimeMock.Verify(x => x.InvokeVoidAsync(
                It.Is<string>(s => s.Contains(".setItem")),
                It.IsAny<object?[]?>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task ExtendSessionExpiryAsync_ShouldNotExceedMaxExpiry()
        {
            // Arrange
            var session = new EncryptedSessionData
            {
                Metadata = new SessionMetadata
                {
                    ExpiresAt = DateTime.UtcNow.AddMinutes(230) // Near max
                }
            };
            
            var json = JsonSerializer.Serialize(session, SessionPersistenceConfiguration.JsonOptions);
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(It.IsAny<string>(), It.IsAny<object?[]?>()))
                .ReturnsAsync(json);
            
            _jsRuntimeMock
                .Setup(x => x.InvokeVoidAsync(It.IsAny<string>(), It.IsAny<object?[]?>()))
                .Returns(ValueTask.CompletedTask);

            // Act
            await _service.ExtendSessionExpiryAsync(30);

            // Assert
            var maxExpiry = DateTime.UtcNow.AddMinutes(SessionPersistenceConfiguration.MaxSessionExpiryMinutes);
            session.Metadata.ExpiresAt.Should().BeOnOrBefore(maxExpiry);
        }

        [Fact]
        public async Task ExtendSessionExpiryAsync_ShouldNotThrow_WhenSessionDoesNotExist()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(It.IsAny<string>(), It.IsAny<object?[]?>()))
                .ReturnsAsync((string?)null);

            // Act
            var act = async () => await _service.ExtendSessionExpiryAsync(30);

            // Assert
            await act.Should().NotThrowAsync();
        }

        #endregion

        #region GetRemainingSessionTimeAsync Tests

        [Fact]
        public async Task GetRemainingSessionTimeAsync_ShouldReturnRemainingTime_WhenSessionValid()
        {
            // Arrange
            var futureTime = DateTime.UtcNow.AddMinutes(30);
            var session = new EncryptedSessionData
            {
                Metadata = new SessionMetadata
                {
                    ExpiresAt = futureTime
                }
            };
            
            var json = JsonSerializer.Serialize(session, SessionPersistenceConfiguration.JsonOptions);
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(It.IsAny<string>(), It.IsAny<object?[]?>()))
                .ReturnsAsync(json);

            // Act
            var result = await _service.GetRemainingSessionTimeAsync();

            // Assert
            result.Should().BeGreaterThan(TimeSpan.Zero);
            result.Should().BeCloseTo(TimeSpan.FromMinutes(30), TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task GetRemainingSessionTimeAsync_ShouldReturnZero_WhenSessionExpired()
        {
            // Arrange
            var session = new EncryptedSessionData
            {
                Metadata = new SessionMetadata
                {
                    ExpiresAt = DateTime.UtcNow.AddMinutes(-5)
                }
            };
            
            var json = JsonSerializer.Serialize(session, SessionPersistenceConfiguration.JsonOptions);
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(It.IsAny<string>(), It.IsAny<object?[]?>()))
                .ReturnsAsync(json);

            // Act
            var result = await _service.GetRemainingSessionTimeAsync();

            // Assert
            result.Should().Be(TimeSpan.Zero);
        }

        [Fact]
        public async Task GetRemainingSessionTimeAsync_ShouldReturnZero_WhenNoSession()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(It.IsAny<string>(), It.IsAny<object?[]?>()))
                .ReturnsAsync((string?)null);

            // Act
            var result = await _service.GetRemainingSessionTimeAsync();

            // Assert
            result.Should().Be(TimeSpan.Zero);
        }

        [Fact]
        public async Task GetRemainingSessionTimeAsync_ShouldReturnZero_WhenExceptionOccurs()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(It.IsAny<string>(), It.IsAny<object?[]?>()))
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _service.GetRemainingSessionTimeAsync();

            // Assert
            result.Should().Be(TimeSpan.Zero);
        }

        #endregion

        #region Helper Methods

        private PersistedSessionData CreateTestSessionData()
        {
            return new PersistedSessionData
            {
                SessionId = "test-session-123",
                Username = "testuser",
                PublicKey = new byte[32],
                EncryptedPrivateKey = new byte[64],
                Salt = new byte[16],
                IV = new byte[16],
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                LastActivityAt = DateTime.UtcNow,
                State = SessionState.Unlocked,
                Version = 1
            };
        }

        #endregion
    }
}