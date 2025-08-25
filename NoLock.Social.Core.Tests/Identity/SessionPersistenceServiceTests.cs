using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using NoLock.Social.Core.Cryptography.Interfaces;
using NoLock.Social.Core.Identity.Configuration;
using NoLock.Social.Core.Identity.Interfaces;
using NoLock.Social.Core.Identity.Services;

namespace NoLock.Social.Core.Tests.Identity
{
    /// <summary>
    /// Unit tests for session persistence functionality
    /// </summary>
    public class SessionPersistenceServiceTests
    {
        private readonly Mock<IJSRuntime> _jsRuntimeMock;
        private readonly Mock<IWebCryptoService> _webCryptoServiceMock;
        private readonly Mock<ISecureMemoryManager> _secureMemoryManagerMock;
        private readonly Mock<ILogger<SecureSessionPersistenceService>> _loggerMock;
        private readonly SecureSessionPersistenceService _service;

        public SessionPersistenceServiceTests()
        {
            _jsRuntimeMock = new Mock<IJSRuntime>();
            _webCryptoServiceMock = new Mock<IWebCryptoService>();
            _secureMemoryManagerMock = new Mock<ISecureMemoryManager>();
            _loggerMock = new Mock<ILogger<SecureSessionPersistenceService>>();

            _service = new SecureSessionPersistenceService(
                _jsRuntimeMock.Object,
                _webCryptoServiceMock.Object,
                _secureMemoryManagerMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task PersistSessionAsync_ValidData_StoresSessionMetadata()
        {
            // Arrange
            var sessionData = new PersistedSessionData
            {
                SessionId = "test-session-123",
                Username = "testuser",
                PublicKey = new byte[] { 1, 2, 3, 4, 5 },
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
                State = SessionState.Unlocked,
                Version = 1
            };

            var encryptionKey = new byte[32];
            string? capturedKey = null;
            string? capturedValue = null;

            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                    It.IsAny<string>(),
                    It.IsAny<object[]>()))
                .Callback<string, object[]>((identifier, args) =>
                {
                    if (identifier == "sessionStorage.setItem" && args.Length >= 2)
                    {
                        capturedKey = args[0] as string;
                        capturedValue = args[1] as string;
                    }
                })
                .Returns(new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());

            // Act
            var result = await _service.PersistSessionAsync(sessionData, encryptionKey, 30);

            // Assert
            Assert.True(result);
            Assert.Equal(SessionPersistenceConfiguration.SessionMetadataKey, capturedKey);
            Assert.NotNull(capturedValue);

            // Verify the stored metadata
            var metadata = JsonSerializer.Deserialize<SecureSessionMetadata>(
                capturedValue, SessionPersistenceConfiguration.JsonOptions);
            Assert.NotNull(metadata);
            Assert.Equal(sessionData.SessionId, metadata.SessionId);
            Assert.Equal(sessionData.Username, metadata.Username);
            Assert.Equal(2, metadata.Version); // Secure version
        }

        [Fact]
        public async Task GetPersistedSessionAsync_NoSession_ReturnsNull()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(
                    "sessionStorage.getItem",
                    It.IsAny<object[]>()))
                .ReturnsAsync((string?)null);

            // Act
            var result = await _service.GetPersistedSessionAsync();

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetPersistedSessionAsync_ExpiredSession_ClearsAndReturnsNull()
        {
            // Arrange
            var expiredMetadata = new SecureSessionMetadata
            {
                SessionId = "expired-session",
                Username = "testuser",
                ExpiresAt = DateTime.UtcNow.AddMinutes(-10), // Expired
                Version = 2
            };

            var json = JsonSerializer.Serialize(expiredMetadata, SessionPersistenceConfiguration.JsonOptions);

            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(
                    "sessionStorage.getItem",
                    It.IsAny<object[]>()))
                .ReturnsAsync(json);

            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                    It.IsAny<string>(),
                    It.IsAny<object[]>()))
                .Returns(new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());

            // Act
            var result = await _service.GetPersistedSessionAsync();

            // Assert
            Assert.Null(result);
            
            // Verify that clear was called
            _jsRuntimeMock.Verify(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "sessionStorage.removeItem",
                It.IsAny<object[]>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task GetPersistedSessionAsync_ValidSession_ReturnsSessionData()
        {
            // Arrange
            var validMetadata = new SecureSessionMetadata
            {
                SessionId = "valid-session",
                Username = "testuser",
                PublicKeyBase64 = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                VerificationHash = "test-hash",
                Version = 2
            };

            var json = JsonSerializer.Serialize(validMetadata, SessionPersistenceConfiguration.JsonOptions);

            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(
                    "sessionStorage.getItem",
                    It.IsAny<object[]>()))
                .ReturnsAsync(json);

            // Act
            var result = await _service.GetPersistedSessionAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(validMetadata.SessionId, result.Metadata.SessionId);
            Assert.Equal(validMetadata.ExpiresAt, result.Metadata.ExpiresAt);
            Assert.Equal(validMetadata.VerificationHash, result.IntegrityCheck);
        }

        [Fact]
        public async Task ClearPersistedSessionAsync_RemovesAllSessionData()
        {
            // Arrange
            var removeItemCalls = 0;

            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                    It.Is<string>(s => s.Contains("removeItem")),
                    It.IsAny<object[]>()))
                .Callback(() => removeItemCalls++)
                .Returns(new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());

            // Act
            await _service.ClearPersistedSessionAsync();

            // Assert
            Assert.Equal(2, removeItemCalls); // Should remove both storage key and metadata key
        }

        [Fact]
        public async Task HasValidPersistedSessionAsync_NoSession_ReturnsFalse()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(
                    "sessionStorage.getItem",
                    It.IsAny<object[]>()))
                .ReturnsAsync((string?)null);

            // Act
            var result = await _service.HasValidPersistedSessionAsync();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task HasValidPersistedSessionAsync_ValidSession_ReturnsTrue()
        {
            // Arrange
            var validMetadata = new SecureSessionMetadata
            {
                SessionId = "valid-session",
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                Version = 2
            };

            var json = JsonSerializer.Serialize(validMetadata, SessionPersistenceConfiguration.JsonOptions);

            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(
                    "sessionStorage.getItem",
                    It.IsAny<object[]>()))
                .ReturnsAsync(json);

            // Act
            var result = await _service.HasValidPersistedSessionAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task ExtendSessionExpiryAsync_ExtendsValidSession()
        {
            // Arrange
            var metadata = new SecureSessionMetadata
            {
                SessionId = "test-session",
                Username = "testuser",
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                LastActivityAt = DateTime.UtcNow.AddMinutes(-5),
                Version = 2
            };

            var json = JsonSerializer.Serialize(metadata, SessionPersistenceConfiguration.JsonOptions);

            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(
                    "sessionStorage.getItem",
                    It.IsAny<object[]>()))
                .ReturnsAsync(json);

            string? updatedJson = null;
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                    "sessionStorage.setItem",
                    It.IsAny<object[]>()))
                .Callback<string, object[]>((_, args) =>
                {
                    if (args.Length >= 2)
                        updatedJson = args[1] as string;
                })
                .Returns(new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());

            // Act
            await _service.ExtendSessionExpiryAsync(15);

            // Assert
            Assert.NotNull(updatedJson);
            var updatedMetadata = JsonSerializer.Deserialize<SecureSessionMetadata>(
                updatedJson, SessionPersistenceConfiguration.JsonOptions);
            Assert.NotNull(updatedMetadata);
            
            // Verify expiry was extended
            Assert.True(updatedMetadata.ExpiresAt > metadata.ExpiresAt);
            
            // Verify last activity was updated
            Assert.True(updatedMetadata.LastActivityAt > metadata.LastActivityAt);
        }

        [Fact]
        public async Task GetRemainingSessionTimeAsync_ValidSession_ReturnsCorrectTime()
        {
            // Arrange
            var futureTime = DateTime.UtcNow.AddMinutes(15);
            var metadata = new SecureSessionMetadata
            {
                SessionId = "test-session",
                ExpiresAt = futureTime,
                Version = 2
            };

            var json = JsonSerializer.Serialize(metadata, SessionPersistenceConfiguration.JsonOptions);

            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(
                    "sessionStorage.getItem",
                    It.IsAny<object[]>()))
                .ReturnsAsync(json);

            // Act
            var remainingTime = await _service.GetRemainingSessionTimeAsync();

            // Assert
            Assert.True(remainingTime.TotalMinutes > 14);
            Assert.True(remainingTime.TotalMinutes <= 15);
        }

        [Fact]
        public async Task GetRemainingSessionTimeAsync_NoSession_ReturnsZero()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(
                    "sessionStorage.getItem",
                    It.IsAny<object[]>()))
                .ReturnsAsync((string?)null);

            // Act
            var remainingTime = await _service.GetRemainingSessionTimeAsync();

            // Assert
            Assert.Equal(TimeSpan.Zero, remainingTime);
        }

        [Fact]
        public async Task GetSessionMetadataAsync_ReturnsStoredMetadata()
        {
            // Arrange
            var metadata = new SecureSessionMetadata
            {
                SessionId = "test-session",
                Username = "testuser",
                PublicKeyBase64 = "test-key",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                Version = 2
            };

            var json = JsonSerializer.Serialize(metadata, SessionPersistenceConfiguration.JsonOptions);

            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(
                    "sessionStorage.getItem",
                    It.IsAny<object[]>()))
                .ReturnsAsync(json);

            // Act
            var result = await _service.GetSessionMetadataAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(metadata.SessionId, result.SessionId);
            Assert.Equal(metadata.Username, result.Username);
            Assert.Equal(metadata.PublicKeyBase64, result.PublicKeyBase64);
        }
    }
}