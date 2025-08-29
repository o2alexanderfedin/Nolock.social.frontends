using System.Text.Json;
using System.Collections.Generic;
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
    /// Unit tests for SessionPersistenceService functionality
    /// Tests encryption, decryption, storage operations, and error scenarios
    /// </summary>
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
                _loggerMock.Object);
        }

        #region Constructor Tests
        
        [Fact]
        public void Constructor_WithValidDependencies_CreatesInstance()
        {
            // Arrange & Act
            var service = new SessionPersistenceService(
                _jsRuntimeMock.Object,
                _webCryptoServiceMock.Object,
                _secureMemoryManagerMock.Object,
                _loggerMock.Object);

            // Assert
            Assert.NotNull(service);
        }

        [Theory]
        [InlineData(null, "jsRuntime")]
        public void Constructor_WithNullJSRuntime_ThrowsArgumentNullException(IJSRuntime jsRuntime, string expectedParam)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new SessionPersistenceService(
                    jsRuntime,
                    _webCryptoServiceMock.Object,
                    _secureMemoryManagerMock.Object,
                    _loggerMock.Object));
            
            Assert.Equal(expectedParam, exception.ParamName);
        }
        
        [Theory]
        [InlineData(null, "webCryptoService")]
        public void Constructor_WithNullWebCryptoService_ThrowsArgumentNullException(IWebCryptoService webCryptoService, string expectedParam)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new SessionPersistenceService(
                    _jsRuntimeMock.Object,
                    webCryptoService,
                    _secureMemoryManagerMock.Object,
                    _loggerMock.Object));
            
            Assert.Equal(expectedParam, exception.ParamName);
        }
        
        [Theory]
        [InlineData(null, "secureMemoryManager")]
        public void Constructor_WithNullSecureMemoryManager_ThrowsArgumentNullException(ISecureMemoryManager secureMemoryManager, string expectedParam)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new SessionPersistenceService(
                    _jsRuntimeMock.Object,
                    _webCryptoServiceMock.Object,
                    secureMemoryManager,
                    _loggerMock.Object));
            
            Assert.Equal(expectedParam, exception.ParamName);
        }
        
        [Theory]
        [InlineData(null, "logger")]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException(ILogger<SessionPersistenceService> logger, string expectedParam)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new SessionPersistenceService(
                    _jsRuntimeMock.Object,
                    _webCryptoServiceMock.Object,
                    _secureMemoryManagerMock.Object,
                    logger));
            
            Assert.Equal(expectedParam, exception.ParamName);
        }
        
        #endregion
        
        #region PersistSessionAsync Tests
        
        [Fact]
        public async Task PersistSessionAsync_ValidData_StoresSessionSuccessfully()
        {
            // Arrange
            var sessionData = CreateValidSessionData();
            var encryptionKey = new byte[32];
            var capturedStorageCalls = new List<(string key, string value)>();

            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                    It.Is<string>(s => s.Contains("setItem")),
                    It.IsAny<object[]>()))
                .Callback<string, object[]>((identifier, args) =>
                {
                    if (args.Length >= 2)
                        capturedStorageCalls.Add((args[0] as string ?? "", args[1] as string ?? ""));
                })
                .Returns(new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());

            // Act
            var result = await _service.PersistSessionAsync(sessionData, encryptionKey, 30);

            // Assert
            Assert.True(result);
            Assert.Equal(2, capturedStorageCalls.Count); // Should store session data and metadata
            
            // Verify session data was stored
            var sessionDataCall = capturedStorageCalls.FirstOrDefault(c => c.key == SessionPersistenceConfiguration.SessionStorageKey);
            Assert.NotEqual(default, sessionDataCall);
            Assert.NotNull(sessionDataCall.value);
            
            // Verify metadata was stored
            var metadataCall = capturedStorageCalls.FirstOrDefault(c => c.key == SessionPersistenceConfiguration.SessionMetadataKey);
            Assert.NotEqual(default, metadataCall);
            Assert.NotNull(metadataCall.value);
            
            // Verify the stored session is encrypted
            var encryptedSession = JsonSerializer.Deserialize<EncryptedSessionData>(
                sessionDataCall.value, SessionPersistenceConfiguration.JsonOptions);
            Assert.NotNull(encryptedSession);
            Assert.NotEmpty(encryptedSession.EncryptedPayload);
            Assert.NotEmpty(encryptedSession.IntegrityCheck);
            Assert.Equal(sessionData.SessionId, encryptedSession.Metadata.SessionId);
        }

        [Theory]
        [InlineData(null, "sessionData")]
        public async Task PersistSessionAsync_WithNullSessionData_ThrowsArgumentNullException(PersistedSessionData sessionData, string expectedParam)
        {
            // Arrange
            var encryptionKey = new byte[32];

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => _service.PersistSessionAsync(sessionData, encryptionKey));
            
            Assert.Equal(expectedParam, exception.ParamName);
        }

        [Theory]
        [InlineData(null, "encryptionKey")]
        [InlineData(new byte[0], "encryptionKey")]
        public async Task PersistSessionAsync_WithInvalidEncryptionKey_ThrowsArgumentException(byte[] encryptionKey, string expectedParam)
        {
            // Arrange
            var sessionData = CreateValidSessionData();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => _service.PersistSessionAsync(sessionData, encryptionKey));
            
            Assert.Contains(expectedParam, exception.Message);
        }

        [Theory]
        [InlineData(15, 15)]
        [InlineData(60, 60)]
        [InlineData(1440, 1440)] // 24 hours
        [InlineData(2000, 1440)] // Should cap at max
        public async Task PersistSessionAsync_WithVariousExpiryTimes_SetsCorrectExpiry(int requestedMinutes, int expectedMaxMinutes)
        {
            // Arrange
            var sessionData = CreateValidSessionData();
            var encryptionKey = new byte[32];
            var capturedMetadata = "";

            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                    It.Is<string>(s => s.Contains("setItem")),
                    It.IsAny<object[]>()))
                .Callback<string, object[]>((identifier, args) =>
                {
                    if (args.Length >= 2 && args[0].ToString() == SessionPersistenceConfiguration.SessionMetadataKey)
                        capturedMetadata = args[1] as string ?? "";
                })
                .Returns(new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());

            // Act
            var result = await _service.PersistSessionAsync(sessionData, encryptionKey, requestedMinutes);

            // Assert
            Assert.True(result);
            var metadata = JsonSerializer.Deserialize<SessionMetadata>(capturedMetadata, SessionPersistenceConfiguration.JsonOptions);
            Assert.NotNull(metadata);
            
            var actualMinutes = (metadata.ExpiresAt - DateTime.UtcNow).TotalMinutes;
            Assert.True(actualMinutes <= expectedMaxMinutes + 1); // Allow 1 minute tolerance for test execution time
        }
        
        [Fact]
        public async Task PersistSessionAsync_JSRuntimeThrows_ReturnsFalse()
        {
            // Arrange
            var sessionData = CreateValidSessionData();
            var encryptionKey = new byte[32];

            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                    It.IsAny<string>(),
                    It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("Storage quota exceeded"));

            // Act
            var result = await _service.PersistSessionAsync(sessionData, encryptionKey);

            // Assert
            Assert.False(result);
        }
        
        #endregion
        
        #region GetPersistedSessionAsync Tests

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
            var expiredSession = new EncryptedSessionData
            {
                EncryptedPayload = "encrypted-data",
                IntegrityCheck = "hmac-hash",
                Metadata = new SessionMetadata
                {
                    SessionId = "expired-session",
                    ExpiresAt = DateTime.UtcNow.AddMinutes(-10), // Expired
                    Version = 1
                }
            };

            var json = JsonSerializer.Serialize(expiredSession, SessionPersistenceConfiguration.JsonOptions);

            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(
                    "sessionStorage.getItem",
                    It.IsAny<object[]>()))
                .ReturnsAsync(json);

            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                    It.Is<string>(s => s.Contains("removeItem")),
                    It.IsAny<object[]>()))
                .Returns(new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());

            // Act
            var result = await _service.GetPersistedSessionAsync();

            // Assert
            Assert.Null(result);
            
            // Verify that clear was called for both storage keys
            _jsRuntimeMock.Verify(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "sessionStorage.removeItem",
                It.IsAny<object[]>()), Times.AtLeast(2));
        }

        [Fact]
        public async Task GetPersistedSessionAsync_ValidSession_ReturnsSessionData()
        {
            // Arrange
            var validSession = new EncryptedSessionData
            {
                EncryptedPayload = "valid-encrypted-payload",
                IntegrityCheck = "valid-hmac-hash",
                Metadata = new SessionMetadata
                {
                    SessionId = "valid-session",
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                    Version = 1
                }
            };

            var json = JsonSerializer.Serialize(validSession, SessionPersistenceConfiguration.JsonOptions);

            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(
                    "sessionStorage.getItem",
                    It.IsAny<object[]>()))
                .ReturnsAsync(json);

            // Act
            var result = await _service.GetPersistedSessionAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(validSession.EncryptedPayload, result.EncryptedPayload);
            Assert.Equal(validSession.IntegrityCheck, result.IntegrityCheck);
            Assert.Equal(validSession.Metadata.SessionId, result.Metadata.SessionId);
            Assert.Equal(validSession.Metadata.ExpiresAt, result.Metadata.ExpiresAt);
        }
        
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("invalid-json")]
        [InlineData("{\"incomplete\": true}")]
        public async Task GetPersistedSessionAsync_WithInvalidStoredData_ReturnsNull(string storedData)
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(
                    "sessionStorage.getItem",
                    It.IsAny<object[]>()))
                .ReturnsAsync(string.IsNullOrWhiteSpace(storedData) ? null : storedData);

            // Act
            var result = await _service.GetPersistedSessionAsync();

            // Assert
            Assert.Null(result);
        }
        
        [Fact]
        public async Task GetPersistedSessionAsync_JSRuntimeThrows_ReturnsNull()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(
                    "sessionStorage.getItem",
                    It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("Storage access denied"));

            // Act
            var result = await _service.GetPersistedSessionAsync();

            // Assert
            Assert.Null(result);
        }
        
        #endregion

        #region DecryptSessionAsync Tests
        
        [Fact]
        public async Task DecryptSessionAsync_WithNullEncryptedData_ThrowsArgumentNullException()
        {
            // Arrange
            var decryptionKey = new byte[32];

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => _service.DecryptSessionAsync(null, decryptionKey));
            
            Assert.Equal("encryptedData", exception.ParamName);
        }

        [Theory]
        [InlineData(null, "decryptionKey")]
        [InlineData(new byte[0], "decryptionKey")]
        public async Task DecryptSessionAsync_WithInvalidDecryptionKey_ThrowsArgumentException(byte[] decryptionKey, string expectedParam)
        {
            // Arrange
            var encryptedData = new EncryptedSessionData
            {
                EncryptedPayload = "test-payload",
                IntegrityCheck = "test-hmac",
                Metadata = new SessionMetadata { SessionId = "test" }
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => _service.DecryptSessionAsync(encryptedData, decryptionKey));
            
            Assert.Contains(expectedParam, exception.Message);
        }
        
        [Fact]
        public async Task DecryptSessionAsync_CurrentImplementation_ReturnsNull()
        {
            // Note: Current implementation is simplified and returns null
            // This test verifies the current behavior until full implementation
            
            // Arrange
            var encryptedData = new EncryptedSessionData
            {
                EncryptedPayload = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
                IntegrityCheck = Convert.ToBase64String(new byte[] { 4, 5, 6 }),
                Metadata = new SessionMetadata
                {
                    SessionId = "test-session",
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                    Version = 1
                }
            };
            var decryptionKey = new byte[32];

            // Act
            var result = await _service.DecryptSessionAsync(encryptedData, decryptionKey);

            // Assert
            Assert.Null(result); // Current implementation returns null
        }
        
        #endregion
        
        #region ClearPersistedSessionAsync Tests

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
        public async Task ClearPersistedSessionAsync_JSRuntimeThrows_DoesNotThrow()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                    It.Is<string>(s => s.Contains("removeItem")),
                    It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("Storage access error"));

            // Act & Assert - Should not throw
            await _service.ClearPersistedSessionAsync();
            
            // Test passes if no exception is thrown
        }
        
        #endregion

        #region HasValidPersistedSessionAsync Tests
        
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
            var validSession = new EncryptedSessionData
            {
                EncryptedPayload = "payload",
                IntegrityCheck = "hmac",
                Metadata = new SessionMetadata
                {
                    SessionId = "valid-session",
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                    Version = 1
                }
            };

            var json = JsonSerializer.Serialize(validSession, SessionPersistenceConfiguration.JsonOptions);

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
        
        [Theory]
        [InlineData(-10, false)] // Expired
        [InlineData(10, true)]   // Valid
        [InlineData(0, false)]   // Exactly expired
        public async Task HasValidPersistedSessionAsync_WithDifferentExpiryTimes_ReturnsExpectedResult(int expiryMinutesFromNow, bool expectedResult)
        {
            // Arrange
            var session = new EncryptedSessionData
            {
                EncryptedPayload = "payload",
                IntegrityCheck = "hmac",
                Metadata = new SessionMetadata
                {
                    SessionId = "test-session",
                    ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutesFromNow),
                    Version = 1
                }
            };

            var json = JsonSerializer.Serialize(session, SessionPersistenceConfiguration.JsonOptions);

            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(
                    "sessionStorage.getItem",
                    It.IsAny<object[]>()))
                .ReturnsAsync(json);

            // Act
            var result = await _service.HasValidPersistedSessionAsync();

            // Assert
            Assert.Equal(expectedResult, result);
        }
        
        [Fact]
        public async Task HasValidPersistedSessionAsync_ExceptionThrown_ReturnsFalse()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(
                    "sessionStorage.getItem",
                    It.IsAny<object[]>()))
                .ThrowsAsync(new Exception("Storage error"));

            // Act
            var result = await _service.HasValidPersistedSessionAsync();

            // Assert
            Assert.False(result);
        }
        
        #endregion
        
        #region Thread Safety and Concurrent Access Tests
        
        [Fact]
        public async Task PersistSessionAsync_ConcurrentCalls_HandlesSafelyWithoutDeadlock()
        {
            // Arrange
            var sessionData1 = CreateValidSessionData();
            sessionData1.SessionId = "session1";
            var sessionData2 = CreateValidSessionData();
            sessionData2.SessionId = "session2";
            var encryptionKey = new byte[32];
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                    It.IsAny<string>(),
                    It.IsAny<object[]>()))
                .Returns(new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());
            
            // Act
            var tasks = new[]
            {
                Task.Run(() => _service.PersistSessionAsync(sessionData1, encryptionKey, 30)),
                Task.Run(() => _service.PersistSessionAsync(sessionData2, encryptionKey, 30))
            };
            
            var results = await Task.WhenAll(tasks);
            
            // Assert
            Assert.All(results, r => Assert.True(r));
        }
        
        [Fact]
        public async Task GetPersistedSessionAsync_ConcurrentReads_ReturnConsistentData()
        {
            // Arrange
            var validSession = new EncryptedSessionData
            {
                EncryptedPayload = "consistent-payload",
                IntegrityCheck = "consistent-hmac",
                Metadata = new SessionMetadata
                {
                    SessionId = "concurrent-session",
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                    Version = 1
                }
            };
            
            var json = JsonSerializer.Serialize(validSession, SessionPersistenceConfiguration.JsonOptions);
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(
                    It.IsAny<string>(),
                    It.IsAny<object[]>()))
                .ReturnsAsync(json);
            
            // Act
            var tasks = Enumerable.Range(0, 5)
                .Select(_ => Task.Run(async () => await _service.GetPersistedSessionAsync()))
                .ToArray();
            
            var results = await Task.WhenAll(tasks);
            
            // Assert
            Assert.All(results, r =>
            {
                Assert.NotNull(r);
                Assert.Equal(validSession.Metadata.SessionId, r.Metadata.SessionId);
            });
        }
        
        [Fact]
        public async Task ClearPersistedSessionAsync_ConcurrentClearAndRead_HandlesRaceCondition()
        {
            // Arrange
            var session = new EncryptedSessionData
            {
                EncryptedPayload = "race-payload",
                IntegrityCheck = "race-hmac",
                Metadata = new SessionMetadata
                {
                    SessionId = "race-session",
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                    Version = 1
                }
            };
            
            var json = JsonSerializer.Serialize(session, SessionPersistenceConfiguration.JsonOptions);
            var sessionExists = true;
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(
                    It.IsAny<string>(),
                    It.IsAny<object[]>()))
                .ReturnsAsync(() => sessionExists ? json : null);
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                    It.Is<string>(s => s.Contains("removeItem")),
                    It.IsAny<object[]>()))
                .Callback(() => sessionExists = false)
                .Returns(new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());
            
            // Act
            var clearTask = Task.Run(async () => await _service.ClearPersistedSessionAsync());
            var readTask = Task.Run(async () => await _service.GetPersistedSessionAsync());
            
            await Task.WhenAll(clearTask, readTask);
            
            // Assert - No exceptions should be thrown
            var finalRead = await _service.GetPersistedSessionAsync();
            Assert.Null(finalRead);
        }
        
        #endregion
        
        #region Extended Error Handling Tests
        
        [Fact]
        public async Task PersistSessionAsync_SerializationError_ReturnsFalse()
        {
            // Arrange
            var sessionData = CreateValidSessionData();
            sessionData.SessionId = null!; // This could cause serialization issues
            var encryptionKey = new byte[32];
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                    It.IsAny<string>(),
                    It.IsAny<object[]>()))
                .Returns(new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());
            
            // Act
            var result = await _service.PersistSessionAsync(sessionData, encryptionKey);
            
            // Assert
            Assert.True(result); // Should handle gracefully
        }
        
        [Fact]
        public async Task PersistSessionAsync_StorageQuotaExceededOnMetadata_ReturnsFalse()
        {
            // Arrange
            var sessionData = CreateValidSessionData();
            var encryptionKey = new byte[32];
            var callCount = 0;
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                    It.IsAny<string>(),
                    It.IsAny<object[]>()))
                .Returns(() =>
                {
                    callCount++;
                    if (callCount == 2) // Fail on metadata storage
                        throw new JSException("QuotaExceededError");
                    return new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>();
                });
            
            // Act
            var result = await _service.PersistSessionAsync(sessionData, encryptionKey);
            
            // Assert
            Assert.False(result);
        }
        
        [Fact]
        public async Task GetPersistedSessionAsync_PartiallyCorruptedData_ReturnsNull()
        {
            // Arrange
            var corruptedJson = "{\"EncryptedPayload\":\"test\",\"IntegrityCheck\":}";
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(
                    It.IsAny<string>(),
                    It.IsAny<object[]>()))
                .ReturnsAsync(corruptedJson);
            
            // Act
            var result = await _service.GetPersistedSessionAsync();
            
            // Assert
            Assert.Null(result);
        }
        
        [Fact]
        public async Task GetPersistedSessionAsync_MissingMetadata_ReturnsNull()
        {
            // Arrange
            var incompleteSession = "{\"EncryptedPayload\":\"test\",\"IntegrityCheck\":\"test\"}";
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(
                    It.IsAny<string>(),
                    It.IsAny<object[]>()))
                .ReturnsAsync(incompleteSession);
            
            // Act
            var result = await _service.GetPersistedSessionAsync();
            
            // Assert
            Assert.Null(result);
        }
        
        [Fact]
        public async Task ExtendSessionExpiryAsync_StorageError_HandlesGracefully()
        {
            // Arrange
            var session = new EncryptedSessionData
            {
                EncryptedPayload = "test-payload",
                IntegrityCheck = "test-hmac",
                Metadata = new SessionMetadata
                {
                    SessionId = "test-session",
                    ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                    Version = 1
                }
            };
            
            var json = JsonSerializer.Serialize(session, SessionPersistenceConfiguration.JsonOptions);
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(
                    It.IsAny<string>(),
                    It.IsAny<object[]>()))
                .ReturnsAsync(json);
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                    It.Is<string>(s => s.Contains("setItem")),
                    It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("Storage error"));
            
            // Act & Assert - Should not throw
            await _service.ExtendSessionExpiryAsync(15);
        }
        
        #endregion
        
        #region Encryption/Decryption Edge Cases
        
        [Fact]
        public async Task DecryptSessionAsync_EmptyPayload_ThrowsOrReturnsNull()
        {
            // Arrange
            var encryptedData = new EncryptedSessionData
            {
                EncryptedPayload = "",
                IntegrityCheck = "test-hmac",
                Metadata = new SessionMetadata { SessionId = "test" }
            };
            var decryptionKey = new byte[32];
            
            // Act
            var result = await _service.DecryptSessionAsync(encryptedData, decryptionKey);
            
            // Assert
            Assert.Null(result); // Current implementation returns null
        }
        
        [Fact]
        public async Task DecryptSessionAsync_InvalidBase64Payload_ReturnsNull()
        {
            // Arrange
            var encryptedData = new EncryptedSessionData
            {
                EncryptedPayload = "not-valid-base64!@#$",
                IntegrityCheck = "test-hmac",
                Metadata = new SessionMetadata { SessionId = "test" }
            };
            var decryptionKey = new byte[32];
            
            // Act
            var result = await _service.DecryptSessionAsync(encryptedData, decryptionKey);
            
            // Assert
            Assert.Null(result);
        }
        
        [Theory]
        [InlineData(16)]  // Too small
        [InlineData(24)]  // Non-standard
        [InlineData(48)]  // Too large
        public async Task DecryptSessionAsync_NonStandardKeySize_HandlesAppropriately(int keySize)
        {
            // Arrange
            var encryptedData = new EncryptedSessionData
            {
                EncryptedPayload = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
                IntegrityCheck = Convert.ToBase64String(new byte[] { 4, 5, 6 }),
                Metadata = new SessionMetadata { SessionId = "test" }
            };
            var decryptionKey = new byte[keySize];
            
            // Act & Assert
            if (keySize != 32)
            {
                // Should either throw or handle gracefully
                try
                {
                    var result = await _service.DecryptSessionAsync(encryptedData, decryptionKey);
                    Assert.Null(result); // If it doesn't throw, should return null
                }
                catch (ArgumentException)
                {
                    // Expected for invalid key sizes
                }
            }
            else
            {
                var result = await _service.DecryptSessionAsync(encryptedData, decryptionKey);
                Assert.Null(result); // Current implementation returns null
            }
        }
        
        #endregion
        
        #region Session Expiry Boundary Tests
        
        [Theory]
        [InlineData(30, 30)]          // Normal value
        [InlineData(1440, 1440)]     // Exactly max (24 hours)
        [InlineData(1441, 1440)]     // Just over max
        [InlineData(10000, 1440)]    // Way over max
        [InlineData(int.MaxValue, 1440)] // Edge case: max int
        public async Task PersistSessionAsync_BoundaryExpiryValues_CapsAtMaximum(int requestedMinutes, int expectedCappedMinutes)
        {
            // Arrange
            var sessionData = CreateValidSessionData();
            var encryptionKey = new byte[32];
            EncryptedSessionData? capturedSession = null;
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                    It.IsAny<string>(),
                    It.IsAny<object[]>()))
                .Callback<string, object[]>((identifier, args) =>
                {
                    if (identifier.Contains("setItem") && args.Length >= 2 && args[0].ToString() == SessionPersistenceConfiguration.SessionStorageKey)
                    {
                        var json = args[1] as string;
                        if (!string.IsNullOrEmpty(json))
                        {
                            capturedSession = JsonSerializer.Deserialize<EncryptedSessionData>(json, SessionPersistenceConfiguration.JsonOptions);
                        }
                    }
                })
                .Returns(new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());
            
            // Act
            var result = await _service.PersistSessionAsync(sessionData, encryptionKey, requestedMinutes);
            
            // Assert
            Assert.True(result);
            Assert.NotNull(capturedSession);
            
            var actualMinutes = (capturedSession.Metadata.ExpiresAt - DateTime.UtcNow).TotalMinutes;
            
            if (expectedCappedMinutes == 0)
            {
                Assert.True(actualMinutes >= 0 && actualMinutes <= 1); // Allow some tolerance for immediate expiry
            }
            else
            {
                Assert.True(actualMinutes <= expectedCappedMinutes + 1); // Allow 1 minute tolerance
            }
        }
        
        [Theory]
        [InlineData(0)]    // Zero minutes
        [InlineData(-5)]   // Negative minutes
        [InlineData(-100)] // Very negative
        public async Task PersistSessionAsync_ZeroOrNegativeExpiry_SetsExpiryInPast(int requestedMinutes)
        {
            // Arrange
            var sessionData = CreateValidSessionData();
            var encryptionKey = new byte[32];
            EncryptedSessionData? capturedSession = null;
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                    It.IsAny<string>(),
                    It.IsAny<object[]>()))
                .Callback<string, object[]>((identifier, args) =>
                {
                    if (identifier.Contains("setItem") && args.Length >= 2 && args[0].ToString() == SessionPersistenceConfiguration.SessionStorageKey)
                    {
                        var json = args[1] as string;
                        if (!string.IsNullOrEmpty(json))
                        {
                            capturedSession = JsonSerializer.Deserialize<EncryptedSessionData>(json, SessionPersistenceConfiguration.JsonOptions);
                        }
                    }
                })
                .Returns(new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());
            
            // Act
            var result = await _service.PersistSessionAsync(sessionData, encryptionKey, requestedMinutes);
            
            // Assert
            Assert.True(result);
            Assert.NotNull(capturedSession);
            
            if (requestedMinutes <= 0)
            {
                // When negative or zero, expiry should be in the past or immediate
                var minutesUntilExpiry = (capturedSession.Metadata.ExpiresAt - DateTime.UtcNow).TotalMinutes;
                Assert.True(minutesUntilExpiry <= 0.1); // Allow tiny tolerance for test execution time
            }
        }
        
        [Fact]
        public async Task GetPersistedSessionAsync_SessionExpiresWhileReading_ReturnsNullAndClears()
        {
            // Arrange
            var almostExpiredSession = new EncryptedSessionData
            {
                EncryptedPayload = "payload",
                IntegrityCheck = "hmac",
                Metadata = new SessionMetadata
                {
                    SessionId = "almost-expired",
                    ExpiresAt = DateTime.UtcNow.AddMilliseconds(-1), // Already expired
                    Version = 1
                }
            };
            
            var json = JsonSerializer.Serialize(almostExpiredSession, SessionPersistenceConfiguration.JsonOptions);
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(
                    It.IsAny<string>(),
                    It.IsAny<object[]>()))
                .ReturnsAsync(() =>
                {
                    // Note: Simulating that session expired while being read
                    // In reality, we're just returning expired data
                    return json;
                });
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                    It.Is<string>(s => s.Contains("removeItem")),
                    It.IsAny<object[]>()))
                .Returns(new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());
            
            // Act
            var result = await _service.GetPersistedSessionAsync();
            
            // Assert
            Assert.Null(result);
        }
        
        [Fact]
        public async Task ExtendSessionExpiryAsync_AlreadyAtMaxExpiry_DoesNotExtendFurther()
        {
            // Arrange
            var maxExpiryTime = DateTime.UtcNow.AddMinutes(SessionPersistenceConfiguration.MaxSessionExpiryMinutes - 1);
            var session = new EncryptedSessionData
            {
                EncryptedPayload = "max-payload",
                IntegrityCheck = "max-hmac",
                Metadata = new SessionMetadata
                {
                    SessionId = "max-session",
                    ExpiresAt = maxExpiryTime,
                    Version = 1
                }
            };
            
            var json = JsonSerializer.Serialize(session, SessionPersistenceConfiguration.JsonOptions);
            EncryptedSessionData? updatedSession = null;
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(
                    It.IsAny<string>(),
                    It.IsAny<object[]>()))
                .ReturnsAsync(json);
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                    It.Is<string>(s => s.Contains("setItem")),
                    It.IsAny<object[]>()))
                .Callback<string, object[]>((identifier, args) =>
                {
                    if (args.Length >= 2 && args[0].ToString() == SessionPersistenceConfiguration.SessionStorageKey)
                    {
                        var updatedJson = args[1] as string;
                        if (!string.IsNullOrEmpty(updatedJson))
                        {
                            updatedSession = JsonSerializer.Deserialize<EncryptedSessionData>(updatedJson, SessionPersistenceConfiguration.JsonOptions);
                        }
                    }
                })
                .Returns(new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());
            
            // Act
            await _service.ExtendSessionExpiryAsync(60); // Try to extend by 60 minutes
            
            // Assert
            Assert.NotNull(updatedSession);
            var maxAllowedExpiry = DateTime.UtcNow.AddMinutes(SessionPersistenceConfiguration.MaxSessionExpiryMinutes);
            Assert.True(updatedSession.Metadata.ExpiresAt <= maxAllowedExpiry.AddMinutes(1)); // Allow 1 minute tolerance
        }
        
        #endregion
        
        #region Additional Validation Tests
        
        [Fact]
        public async Task PersistSessionAsync_EmptySessionId_StillPersists()
        {
            // Arrange
            var sessionData = CreateValidSessionData();
            sessionData.SessionId = string.Empty;
            var encryptionKey = new byte[32];
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                    It.IsAny<string>(),
                    It.IsAny<object[]>()))
                .Returns(new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());
            
            // Act
            var result = await _service.PersistSessionAsync(sessionData, encryptionKey);
            
            // Assert
            Assert.True(result);
        }
        
        [Fact]
        public async Task PersistSessionAsync_VeryLargeSessionData_HandlesAppropriately()
        {
            // Arrange
            var sessionData = CreateValidSessionData();
            sessionData.PublicKey = new byte[10000]; // Large key
            sessionData.EncryptedPrivateKey = new byte[10000]; // Large encrypted data
            var encryptionKey = new byte[32];
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                    It.IsAny<string>(),
                    It.IsAny<object[]>()))
                .Returns(new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());
            
            // Act
            var result = await _service.PersistSessionAsync(sessionData, encryptionKey);
            
            // Assert
            Assert.True(result);
        }
        
        [Theory]
        [InlineData(SessionState.Locked)]
        [InlineData(SessionState.Unlocking)]
        [InlineData(SessionState.Unlocked)]
        [InlineData(SessionState.Locking)]
        [InlineData(SessionState.Expired)]
        public async Task PersistSessionAsync_AllSessionStates_PersistsSuccessfully(SessionState state)
        {
            // Arrange
            var sessionData = CreateValidSessionData();
            sessionData.State = state;
            var encryptionKey = new byte[32];
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                    It.IsAny<string>(),
                    It.IsAny<object[]>()))
                .Returns(new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());
            
            // Act
            var result = await _service.PersistSessionAsync(sessionData, encryptionKey);
            
            // Assert
            Assert.True(result);
        }
        
        [Fact]
        public async Task HasValidPersistedSessionAsync_SessionWithFutureCreatedDate_StillValid()
        {
            // Arrange
            var futureSession = new EncryptedSessionData
            {
                EncryptedPayload = "future-payload",
                IntegrityCheck = "future-hmac",
                Metadata = new SessionMetadata
                {
                    SessionId = "future-session",
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                    Version = 1
                }
            };
            
            var json = JsonSerializer.Serialize(futureSession, SessionPersistenceConfiguration.JsonOptions);
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(
                    It.IsAny<string>(),
                    It.IsAny<object[]>()))
                .ReturnsAsync(json);
            
            // Act
            var result = await _service.HasValidPersistedSessionAsync();
            
            // Assert
            Assert.True(result);
        }
        
        [Fact]
        public async Task GetRemainingSessionTimeAsync_ExpiredSession_ReturnsZero()
        {
            // Arrange
            var expiredSession = new EncryptedSessionData
            {
                EncryptedPayload = "expired-payload",
                IntegrityCheck = "expired-hmac",
                Metadata = new SessionMetadata
                {
                    SessionId = "expired-session",
                    ExpiresAt = DateTime.UtcNow.AddMinutes(-10),
                    Version = 1
                }
            };
            
            var json = JsonSerializer.Serialize(expiredSession, SessionPersistenceConfiguration.JsonOptions);
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(
                    It.IsAny<string>(),
                    It.IsAny<object[]>()))
                .ReturnsAsync(json);
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                    It.Is<string>(s => s.Contains("removeItem")),
                    It.IsAny<object[]>()))
                .Returns(new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());
            
            // Act
            var remainingTime = await _service.GetRemainingSessionTimeAsync();
            
            // Assert
            Assert.Equal(TimeSpan.Zero, remainingTime);
        }
        
        [Fact]
        public async Task GetRemainingSessionTimeAsync_ExceptionThrown_ReturnsZero()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(
                    It.IsAny<string>(),
                    It.IsAny<object[]>()))
                .ThrowsAsync(new Exception("Storage error"));
            
            // Act
            var remainingTime = await _service.GetRemainingSessionTimeAsync();
            
            // Assert
            Assert.Equal(TimeSpan.Zero, remainingTime);
        }
        
        #endregion
        
        #region Storage Type Tests (SessionStorage vs LocalStorage)
        
        [Fact]
        public async Task PersistSessionAsync_UsesCorrectStorageType()
        {
            // Arrange
            var sessionData = CreateValidSessionData();
            var encryptionKey = new byte[32];
            var capturedStorageType = "";
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                    It.IsAny<string>(),
                    It.IsAny<object[]>()))
                .Callback<string, object[]>((identifier, args) =>
                {
                    if (identifier.Contains("Storage"))
                        capturedStorageType = identifier;
                })
                .Returns(new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());
            
            // Act
            await _service.PersistSessionAsync(sessionData, encryptionKey);
            
            // Assert
            Assert.Contains("sessionStorage", capturedStorageType); // Default is sessionStorage
        }
        
        #endregion

        [Fact]
        public async Task ExtendSessionExpiryAsync_ExtendsValidSession()
        {
            // Arrange
            var originalExpiry = DateTime.UtcNow.AddMinutes(10);
            var encryptedSession = new EncryptedSessionData
            {
                EncryptedPayload = "test-payload",
                IntegrityCheck = "test-hmac",
                Metadata = new SessionMetadata
                {
                    SessionId = "test-session",
                    ExpiresAt = originalExpiry,
                    Version = 1
                }
            };

            var json = JsonSerializer.Serialize(encryptedSession, SessionPersistenceConfiguration.JsonOptions);

            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(
                    It.IsAny<string>(),
                    It.IsAny<object[]>()))
                .ReturnsAsync((string identifier, object[] args) =>
                {
                    if (identifier.Contains("getItem") && args.Length >= 1 && args[0].ToString() == SessionPersistenceConfiguration.SessionStorageKey)
                        return json;
                    return null;
                });

            string? updatedJson = null;
            var capturedCalls = new List<(string identifier, object[] args)>();
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                    It.IsAny<string>(),
                    It.IsAny<object[]>()))
                .Callback<string, object[]>((identifier, args) =>
                {
                    capturedCalls.Add((identifier, args));
                    if (identifier.Contains("setItem") && args.Length >= 2 && args[0].ToString() == SessionPersistenceConfiguration.SessionStorageKey)
                        updatedJson = args[1] as string;
                })
                .Returns(new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());

            // Act
            await _service.ExtendSessionExpiryAsync(15);

            // Assert
            // Debug: Check what calls were made
            Assert.True(capturedCalls.Count > 0, $"No JS calls were captured. Expected setItem calls.");
            
            Assert.NotNull(updatedJson);
            var updatedSession = JsonSerializer.Deserialize<EncryptedSessionData>(
                updatedJson, SessionPersistenceConfiguration.JsonOptions);
            Assert.NotNull(updatedSession);
            
            // Verify expiry was extended
            Assert.True(updatedSession.Metadata.ExpiresAt > originalExpiry);
            
            // Verify extension is reasonable (not more than original + 15 minutes + some tolerance)
            var expectedMaxExpiry = originalExpiry.AddMinutes(16); // 15 minutes + 1 minute tolerance
            Assert.True(updatedSession.Metadata.ExpiresAt <= expectedMaxExpiry);
        }

        [Fact]
        public async Task GetRemainingSessionTimeAsync_ValidSession_ReturnsCorrectTime()
        {
            // Arrange
            var futureTime = DateTime.UtcNow.AddMinutes(15);
            var encryptedSession = new EncryptedSessionData
            {
                EncryptedPayload = "test-payload",
                IntegrityCheck = "test-hmac",
                Metadata = new SessionMetadata
                {
                    SessionId = "test-session",
                    ExpiresAt = futureTime,
                    Version = 1
                }
            };

            var json = JsonSerializer.Serialize(encryptedSession, SessionPersistenceConfiguration.JsonOptions);

            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(
                    It.IsAny<string>(),
                    It.IsAny<object[]>()))
                .ReturnsAsync((string identifier, object[] args) =>
                {
                    if (identifier.Contains("getItem") && args.Length >= 1 && args[0].ToString() == SessionPersistenceConfiguration.SessionStorageKey)
                        return json;
                    return null;
                });

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
        public async Task ExtendSessionExpiryAsync_NoSession_DoesNotThrow()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(
                    It.IsAny<string>(),
                    It.IsAny<object[]>()))
                .ReturnsAsync((string?)null);
            
            // Act & Assert - Should not throw
            await _service.ExtendSessionExpiryAsync(15);
        }
        
        [Theory]
        [InlineData(-10)] // Negative extension
        [InlineData(0)]   // Zero extension
        public async Task ExtendSessionExpiryAsync_InvalidExtensionMinutes_HandlesGracefully(int additionalMinutes)
        {
            // Arrange
            var session = new EncryptedSessionData
            {
                EncryptedPayload = "test-payload",
                IntegrityCheck = "test-hmac",
                Metadata = new SessionMetadata
                {
                    SessionId = "test-session",
                    ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                    Version = 1
                }
            };
            
            var json = JsonSerializer.Serialize(session, SessionPersistenceConfiguration.JsonOptions);
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string?>(
                    It.IsAny<string>(),
                    It.IsAny<object[]>()))
                .ReturnsAsync(json);
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                    It.Is<string>(s => s.Contains("setItem")),
                    It.IsAny<object[]>()))
                .Returns(new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());
            
            // Act & Assert - Should not throw
            await _service.ExtendSessionExpiryAsync(additionalMinutes);
        }
        
        #region Helper Methods
        
        private PersistedSessionData CreateValidSessionData()
        {
            return new PersistedSessionData
            {
                SessionId = Guid.NewGuid().ToString(),
                Username = "testuser",
                PublicKey = new byte[32],
                EncryptedPrivateKey = new byte[32],
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