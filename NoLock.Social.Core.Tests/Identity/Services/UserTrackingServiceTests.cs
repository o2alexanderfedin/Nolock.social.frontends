using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Core.Identity.Services;
using Xunit;

namespace NoLock.Social.Core.Tests.Identity.Services
{
    public class UserTrackingServiceTests
    {
        private readonly Mock<ILogger<UserTrackingService>> _loggerMock;
        private readonly UserTrackingService _service;

        public UserTrackingServiceTests()
        {
            _loggerMock = new Mock<ILogger<UserTrackingService>>();
            _service = new UserTrackingService(_loggerMock.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_ShouldThrowArgumentNullException_WhenLoggerIsNull()
        {
            // Act & Assert
            var act = () => new UserTrackingService(null!);
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("logger");
        }

        [Fact]
        public void Constructor_ShouldInitializeSuccessfully_WithValidDependencies()
        {
            // Act
            var service = new UserTrackingService(_loggerMock.Object);

            // Assert
            service.Should().NotBeNull();
        }

        #endregion

        #region CheckUserExistsAsync Tests

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public async Task CheckUserExistsAsync_ShouldThrowArgumentException_WhenPublicKeyIsInvalid(string publicKey)
        {
            // Act
            var act = async () => await _service.CheckUserExistsAsync(publicKey);

            // Assert
            await act.Should().ThrowAsync<ArgumentException>()
                .WithParameterName("publicKeyBase64");
        }

        [Fact]
        public async Task CheckUserExistsAsync_ShouldReturnNotExists_ForNewUser()
        {
            // Arrange
            var publicKey = "testPublicKey123456789";

            // Act
            var result = await _service.CheckUserExistsAsync(publicKey);

            // Assert
            result.Should().NotBeNull();
            result.Exists.Should().BeFalse();
            result.FirstSeen.Should().BeNull();
            result.LastSeen.Should().BeNull();
            result.ContentCount.Should().Be(0);
            result.PublicKeyBase64.Should().Be(publicKey);
        }

        [Fact]
        public async Task CheckUserExistsAsync_ShouldReturnCachedResult_OnSubsequentCalls()
        {
            // Arrange
            var publicKey = "testPublicKey123456789";

            // Act
            var result1 = await _service.CheckUserExistsAsync(publicKey);
            var result2 = await _service.CheckUserExistsAsync(publicKey);

            // Assert
            result1.Should().BeSameAs(result2);
            
            // Logger should indicate cache hit
            _loggerMock.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Returning cached")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once); // Only on second call
        }

        [Theory]
        [InlineData("shortKey")]
        [InlineData("veryLongPublicKeyWithManyCharactersToTestSubstringHandling")]
        public async Task CheckUserExistsAsync_ShouldHandleVariousKeyLengths(string publicKey)
        {
            // Act
            var result = await _service.CheckUserExistsAsync(publicKey);

            // Assert
            result.Should().NotBeNull();
            result.PublicKeyBase64.Should().Be(publicKey);
            
            // Verify logging handles substring correctly
            _loggerMock.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Checking if user exists")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        #endregion

        #region MarkUserAsActiveAsync Tests

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public async Task MarkUserAsActiveAsync_ShouldThrowArgumentException_WhenPublicKeyIsInvalid(string publicKey)
        {
            // Act
            var act = async () => await _service.MarkUserAsActiveAsync(publicKey);

            // Assert
            await act.Should().ThrowAsync<ArgumentException>()
                .WithParameterName("publicKeyBase64");
        }

        [Fact]
        public async Task MarkUserAsActiveAsync_ShouldClearCache_WhenUserIsCached()
        {
            // Arrange
            var publicKey = "testPublicKey123456789";
            
            // First, cache the user
            await _service.CheckUserExistsAsync(publicKey);

            // Act
            await _service.MarkUserAsActiveAsync(publicKey);

            // Assert
            _loggerMock.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cleared cache entry")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task MarkUserAsActiveAsync_ShouldHandleNonCachedUser()
        {
            // Arrange
            var publicKey = "testPublicKey123456789";

            // Act
            await _service.MarkUserAsActiveAsync(publicKey);

            // Assert - Should complete without error
            _loggerMock.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Marking user as active")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task MarkUserAsActiveAsync_ShouldForceCacheRefresh_OnNextCheck()
        {
            // Arrange
            var publicKey = "testPublicKey123456789";
            
            // Cache the user first
            var initialResult = await _service.CheckUserExistsAsync(publicKey);
            
            // Clear cache
            await _service.MarkUserAsActiveAsync(publicKey);
            
            // Act
            var subsequentResult = await _service.CheckUserExistsAsync(publicKey);

            // Assert
            initialResult.Should().NotBeSameAs(subsequentResult); // New instance created
            
            // Should not log "Returning cached" for the second CheckUserExistsAsync
            _loggerMock.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Returning cached")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        #endregion

        #region GetUserActivityAsync Tests

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public async Task GetUserActivityAsync_ShouldThrowArgumentException_WhenPublicKeyIsInvalid(string publicKey)
        {
            // Act
            var act = async () => await _service.GetUserActivityAsync(publicKey);

            // Assert
            await act.Should().ThrowAsync<ArgumentException>()
                .WithParameterName("publicKeyBase64");
        }

        [Fact]
        public async Task GetUserActivityAsync_ShouldReturnEmptySummary_ForNewUser()
        {
            // Arrange
            var publicKey = "testPublicKey123456789";

            // Act
            var result = await _service.GetUserActivityAsync(publicKey);

            // Assert
            result.Should().NotBeNull();
            result.TotalContent.Should().Be(0);
            result.LastActivity.Should().BeNull();
            result.TotalStorageBytes.Should().Be(0);
            result.RecentContentAddresses.Should().NotBeNull();
            result.RecentContentAddresses.Should().BeEmpty();
        }

        [Fact]
        public async Task GetUserActivityAsync_ShouldLogDebugInformation()
        {
            // Arrange
            var publicKey = "testPublicKey123456789";

            // Act
            await _service.GetUserActivityAsync(publicKey);

            // Assert
            _loggerMock.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Getting user activity summary")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region Thread Safety Tests

        [Fact]
        public async Task Service_ShouldBeThreadSafe_WhenAccessedConcurrently()
        {
            // Arrange
            var publicKey = "testPublicKey123456789";
            var tasks = new List<Task>();

            // Act - Perform multiple operations concurrently
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await _service.CheckUserExistsAsync(publicKey);
                    await _service.MarkUserAsActiveAsync(publicKey);
                    await _service.GetUserActivityAsync(publicKey);
                }));
            }

            await Task.WhenAll(tasks);

            // Assert - Should complete without deadlocks or exceptions
            tasks.Should().OnlyContain(t => t.IsCompletedSuccessfully);
        }

        [Fact]
        public async Task Cache_ShouldBeThreadSafe_WithConcurrentReadsAndWrites()
        {
            // Arrange
            var keys = Enumerable.Range(0, 100).Select(i => $"key{i}").ToList();
            var tasks = new List<Task>();

            // Act - Concurrent cache operations
            foreach (var key in keys)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await _service.CheckUserExistsAsync(key); // Cache write
                    await _service.CheckUserExistsAsync(key); // Cache read
                    if (key.GetHashCode() % 2 == 0)
                    {
                        await _service.MarkUserAsActiveAsync(key); // Cache clear
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            tasks.Should().OnlyContain(t => t.IsCompletedSuccessfully);
        }

        #endregion

        #region Edge Cases and Scenarios

        [Fact]
        public async Task CheckUserExistsAsync_ShouldHandleSpecialCharactersInPublicKey()
        {
            // Arrange
            var publicKey = "test+Public/Key==123456789!@#$%^&*()";

            // Act
            var result = await _service.CheckUserExistsAsync(publicKey);

            // Assert
            result.Should().NotBeNull();
            result.PublicKeyBase64.Should().Be(publicKey);
        }

        [Fact]
        public async Task Service_ShouldMaintainSeparateCacheEntriesPerKey()
        {
            // Arrange
            var key1 = "publicKey1";
            var key2 = "publicKey2";

            // Act
            var result1a = await _service.CheckUserExistsAsync(key1);
            var result2a = await _service.CheckUserExistsAsync(key2);
            
            await _service.MarkUserAsActiveAsync(key1); // Clear only key1
            
            var result1b = await _service.CheckUserExistsAsync(key1);
            var result2b = await _service.CheckUserExistsAsync(key2);

            // Assert
            result1a.Should().NotBeSameAs(result1b); // key1 was cleared
            result2a.Should().BeSameAs(result2b); // key2 remained cached
        }

        [Fact]
        public async Task Service_ShouldHandleRapidCacheClearAndAccess()
        {
            // Arrange
            var publicKey = "testKey";
            var tasks = new List<Task>();

            // Act - Rapidly alternate between caching and clearing
            for (int i = 0; i < 50; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await _service.CheckUserExistsAsync(publicKey);
                    await _service.MarkUserAsActiveAsync(publicKey);
                }));
            }

            await Task.WhenAll(tasks);

            // Assert - Should handle rapid operations without issues
            tasks.Should().OnlyContain(t => t.IsCompletedSuccessfully);
        }

        #endregion
    }
}