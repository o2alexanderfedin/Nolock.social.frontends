using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using NoLock.Social.Core.Identity.Services;
using NoLock.Social.Core.Identity.Storage;

namespace NoLock.Social.Core.Tests.Identity
{
    public class RememberMeServiceTests
    {
        private readonly Mock<IJSRuntime> _mockJsRuntime;
        private readonly Mock<ILogger<RememberMeService>> _mockLogger;
        private readonly RememberMeService _service;
        private const string STORAGE_KEY = "nolock_remembered_user";

        public RememberMeServiceTests()
        {
            _mockJsRuntime = new Mock<IJSRuntime>();
            _mockLogger = new Mock<ILogger<RememberMeService>>();
            _service = new RememberMeService(_mockJsRuntime.Object, _mockLogger.Object);
        }

        #region RememberUsernameAsync Tests

        [Fact]
        public async Task RememberUsernameAsync_ValidUsername_StoresSuccessfully()
        {
            // Arrange
            var username = "testuser";
            string? capturedKey = null;
            string? capturedJson = null;

            _mockJsRuntime.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "localStorage.setItem",
                It.IsAny<object[]>()))
                .Callback<string, object[]>((methodName, args) =>
                {
                    capturedKey = args[0] as string;
                    capturedJson = args[1] as string;
                })
                .ReturnsAsync(Mock.Of<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());

            // Act
            await _service.RememberUsernameAsync(username);

            // Assert
            Assert.Equal(STORAGE_KEY, capturedKey);
            Assert.NotNull(capturedJson);
            
            var storedData = JsonSerializer.Deserialize<RememberedUserData>(capturedJson, 
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            
            Assert.NotNull(storedData);
            Assert.Equal(username, storedData.Username);
            Assert.True((DateTime.UtcNow - storedData.LastUsed).TotalSeconds < 5);
            Assert.True(_service.IsUsernameRemembered);
        }

        [Fact]
        public async Task RememberUsernameAsync_NullUsername_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _service.RememberUsernameAsync(null!));
        }

        [Fact]
        public async Task RememberUsernameAsync_EmptyUsername_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _service.RememberUsernameAsync(string.Empty));
        }

        [Fact]
        public async Task RememberUsernameAsync_WhitespaceUsername_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _service.RememberUsernameAsync("   "));
        }

        [Fact]
        public async Task RememberUsernameAsync_JSInteropFails_ThrowsInvalidOperationException()
        {
            // Arrange
            _mockJsRuntime.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "localStorage.setItem",
                It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("localStorage error"));

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.RememberUsernameAsync("testuser"));
            
            Assert.Contains("Failed to save username", ex.Message);
        }

        #endregion

        #region GetRememberedUsernameAsync Tests

        [Fact]
        public async Task GetRememberedUsernameAsync_ValidStoredData_ReturnsUsername()
        {
            // Arrange
            var username = "testuser";
            var data = new RememberedUserData
            {
                Username = username,
                LastUsed = DateTime.UtcNow.AddHours(-1)
            };
            
            var json = JsonSerializer.Serialize(data, 
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            _mockJsRuntime.Setup(x => x.InvokeAsync<string?>(
                "localStorage.getItem",
                It.IsAny<object[]>()))
                .ReturnsAsync(json);

            // Act
            var result = await _service.GetRememberedUsernameAsync();

            // Assert
            Assert.Equal(username, result);
            Assert.True(_service.IsUsernameRemembered);
        }

        [Fact]
        public async Task GetRememberedUsernameAsync_NoStoredData_ReturnsNull()
        {
            // Arrange
            _mockJsRuntime.Setup(x => x.InvokeAsync<string?>(
                "localStorage.getItem",
                It.IsAny<object[]>()))
                .ReturnsAsync((string?)null);

            // Act
            var result = await _service.GetRememberedUsernameAsync();

            // Assert
            Assert.Null(result);
            Assert.False(_service.IsUsernameRemembered);
        }

        [Fact]
        public async Task GetRememberedUsernameAsync_ExpiredData_ClearsAndReturnsNull()
        {
            // Arrange
            var data = new RememberedUserData
            {
                Username = "olduser",
                LastUsed = DateTime.UtcNow.AddDays(-31) // Expired (>30 days)
            };
            
            var json = JsonSerializer.Serialize(data, 
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            _mockJsRuntime.Setup(x => x.InvokeAsync<string?>(
                "localStorage.getItem",
                It.IsAny<object[]>()))
                .ReturnsAsync(json);

            _mockJsRuntime.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "localStorage.removeItem",
                It.IsAny<object[]>()))
                .ReturnsAsync(Mock.Of<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());

            // Act
            var result = await _service.GetRememberedUsernameAsync();

            // Assert
            Assert.Null(result);
            Assert.False(_service.IsUsernameRemembered);
            
            // Verify that clear was called
            _mockJsRuntime.Verify(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "localStorage.removeItem",
                It.Is<object[]>(args => args[0].Equals(STORAGE_KEY))),
                Times.Once);
        }

        [Fact]
        public async Task GetRememberedUsernameAsync_CorruptedData_ClearsAndReturnsNull()
        {
            // Arrange
            _mockJsRuntime.Setup(x => x.InvokeAsync<string?>(
                "localStorage.getItem",
                It.IsAny<object[]>()))
                .ReturnsAsync("{ invalid json }");

            _mockJsRuntime.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "localStorage.removeItem",
                It.IsAny<object[]>()))
                .ReturnsAsync(Mock.Of<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());

            // Act
            var result = await _service.GetRememberedUsernameAsync();

            // Assert
            Assert.Null(result);
            Assert.False(_service.IsUsernameRemembered);
            
            // Verify that clear was called
            _mockJsRuntime.Verify(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "localStorage.removeItem",
                It.Is<object[]>(args => args[0].Equals(STORAGE_KEY))),
                Times.Once);
        }

        [Fact]
        public async Task GetRememberedUsernameAsync_CachesResult()
        {
            // Arrange
            var username = "cacheduser";
            var data = new RememberedUserData
            {
                Username = username,
                LastUsed = DateTime.UtcNow
            };
            
            var json = JsonSerializer.Serialize(data, 
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            _mockJsRuntime.Setup(x => x.InvokeAsync<string?>(
                "localStorage.getItem",
                It.IsAny<object[]>()))
                .ReturnsAsync(json);

            // Act
            var result1 = await _service.GetRememberedUsernameAsync();
            var result2 = await _service.GetRememberedUsernameAsync();

            // Assert
            Assert.Equal(username, result1);
            Assert.Equal(username, result2);
            
            // Should only call localStorage once due to caching
            _mockJsRuntime.Verify(x => x.InvokeAsync<string?>(
                "localStorage.getItem",
                It.IsAny<object[]>()),
                Times.Once);
        }

        [Fact]
        public async Task GetRememberedUsernameAsync_JSInteropFails_ReturnsNull()
        {
            // Arrange
            _mockJsRuntime.Setup(x => x.InvokeAsync<string?>(
                "localStorage.getItem",
                It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("localStorage error"));

            // Act
            var result = await _service.GetRememberedUsernameAsync();

            // Assert
            Assert.Null(result);
            
            // Verify error was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to retrieve remembered username")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region ClearRememberedDataAsync Tests

        [Fact]
        public async Task ClearRememberedDataAsync_Success_ClearsDataAndCache()
        {
            // Arrange
            _mockJsRuntime.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "localStorage.removeItem",
                It.IsAny<object[]>()))
                .ReturnsAsync(Mock.Of<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());

            // First, set up a cached username
            var data = new RememberedUserData { Username = "cacheduser", LastUsed = DateTime.UtcNow };
            var json = JsonSerializer.Serialize(data, 
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            _mockJsRuntime.Setup(x => x.InvokeAsync<string?>(
                "localStorage.getItem",
                It.IsAny<object[]>()))
                .ReturnsAsync(json);

            await _service.GetRememberedUsernameAsync(); // Populate cache

            // Act
            await _service.ClearRememberedDataAsync();

            // Assert
            Assert.False(_service.IsUsernameRemembered);
            
            _mockJsRuntime.Verify(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "localStorage.removeItem",
                It.Is<object[]>(args => args[0].Equals(STORAGE_KEY))),
                Times.Once);
        }

        [Fact]
        public async Task ClearRememberedDataAsync_JSInteropFails_DoesNotThrow()
        {
            // Arrange
            _mockJsRuntime.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "localStorage.removeItem",
                It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("localStorage error"));

            // Act (should not throw)
            await _service.ClearRememberedDataAsync();

            // Assert - verify error was logged but not thrown
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to clear remembered user data")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region UpdateLastUsedAsync Tests

        [Fact]
        public async Task UpdateLastUsedAsync_WithRememberedUsername_UpdatesTimestamp()
        {
            // Arrange
            var username = "testuser";
            
            // First, remember a username
            _mockJsRuntime.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "localStorage.setItem",
                It.IsAny<object[]>()))
                .ReturnsAsync(Mock.Of<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());

            await _service.RememberUsernameAsync(username);
            
            string? capturedJson = null;
            _mockJsRuntime.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "localStorage.setItem",
                It.IsAny<object[]>()))
                .Callback<string, object[]>((methodName, args) =>
                {
                    capturedJson = args[1] as string;
                })
                .ReturnsAsync(Mock.Of<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());

            // Act
            await Task.Delay(100); // Small delay to ensure time difference
            await _service.UpdateLastUsedAsync();

            // Assert
            Assert.NotNull(capturedJson);
            var updatedData = JsonSerializer.Deserialize<RememberedUserData>(capturedJson, 
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            
            Assert.NotNull(updatedData);
            Assert.Equal(username, updatedData.Username);
            Assert.True((DateTime.UtcNow - updatedData.LastUsed).TotalSeconds < 5);
        }

        [Fact]
        public async Task UpdateLastUsedAsync_NoRememberedUsername_DoesNothing()
        {
            // Act
            await _service.UpdateLastUsedAsync();

            // Assert - should not call localStorage
            _mockJsRuntime.Verify(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "localStorage.setItem",
                It.IsAny<object[]>()),
                Times.Never);
        }

        [Fact]
        public async Task UpdateLastUsedAsync_JSInteropFails_DoesNotThrow()
        {
            // Arrange
            var username = "testuser";
            
            // First, remember a username
            _mockJsRuntime.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "localStorage.setItem",
                It.IsAny<object[]>()))
                .ReturnsAsync(Mock.Of<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());

            await _service.RememberUsernameAsync(username);
            
            // Setup failure for update
            _mockJsRuntime.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "localStorage.setItem",
                It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("localStorage error"));

            // Act (should not throw)
            await _service.UpdateLastUsedAsync();

            // Assert - verify error was logged but not thrown
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to update last used timestamp")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region Security Tests

        [Fact]
        public async Task RememberUsernameAsync_NeverStoresPassphrase()
        {
            // Arrange
            var username = "testuser";
            var passphrase = "super-secret-passphrase"; // This should never appear in storage
            string? capturedJson = null;

            _mockJsRuntime.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "localStorage.setItem",
                It.IsAny<object[]>()))
                .Callback<string, object[]>((methodName, args) =>
                {
                    capturedJson = args[1] as string;
                })
                .ReturnsAsync(Mock.Of<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());

            // Act
            await _service.RememberUsernameAsync(username);

            // Assert
            Assert.NotNull(capturedJson);
            Assert.DoesNotContain(passphrase, capturedJson);
            Assert.DoesNotContain("passphrase", capturedJson.ToLower());
            Assert.DoesNotContain("password", capturedJson.ToLower());
            Assert.DoesNotContain("key", capturedJson.ToLower());
            Assert.DoesNotContain("secret", capturedJson.ToLower());
        }

        [Fact]
        public async Task RememberedUserData_OnlyContainsNonSensitiveData()
        {
            // Arrange
            var username = "testuser";
            string? capturedJson = null;

            _mockJsRuntime.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "localStorage.setItem",
                It.IsAny<object[]>()))
                .Callback<string, object[]>((methodName, args) =>
                {
                    capturedJson = args[1] as string;
                })
                .ReturnsAsync(Mock.Of<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());

            // Act
            await _service.RememberUsernameAsync(username);

            // Assert
            Assert.NotNull(capturedJson);
            var storedData = JsonSerializer.Deserialize<RememberedUserData>(capturedJson, 
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            
            Assert.NotNull(storedData);
            Assert.Equal(username, storedData.Username);
            Assert.NotNull(storedData.Preferences);
            Assert.Empty(storedData.Preferences); // Should not contain any preferences by default
            
            // Verify only expected fields are present
            var jsonDoc = JsonDocument.Parse(capturedJson);
            var root = jsonDoc.RootElement;
            Assert.True(root.TryGetProperty("username", out _));
            Assert.True(root.TryGetProperty("lastUsed", out _));
            Assert.True(root.TryGetProperty("preferences", out _));
            Assert.Equal(3, root.EnumerateObject().Count()); // Only 3 properties
        }

        #endregion
    }
}