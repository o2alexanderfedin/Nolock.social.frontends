using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Core.Identity.Models;
using NoLock.Social.Core.Identity.Services;
using NoLock.Social.Core.Storage;
using NoLock.Social.Core.Storage.Interfaces;
using Xunit;

namespace NoLock.Social.Core.Tests.Identity
{
    public class UserTrackingServiceTests
    {
        private readonly Mock<IStorageAdapterService> _mockStorageAdapter;
        private readonly Mock<ILogger<UserTrackingService>> _mockLogger;
        private readonly UserTrackingService _service;
        private readonly string _testPublicKey = "test-public-key-base64";

        public UserTrackingServiceTests()
        {
            _mockStorageAdapter = new Mock<IStorageAdapterService>();
            _mockLogger = new Mock<ILogger<UserTrackingService>>();
            _service = new UserTrackingService(_mockStorageAdapter.Object, _mockLogger.Object);
        }

        #region CheckUserExistsAsync Tests

        [Fact]
        public async Task CheckUserExistsAsync_NewUser_ReturnsNotExists()
        {
            // Arrange
            var emptyContent = GetEmptyAsyncEnumerable<StorageMetadata>();
            _mockStorageAdapter.Setup(x => x.ListAllContentAsync())
                .Returns(emptyContent);

            // Act
            var result = await _service.CheckUserExistsAsync(_testPublicKey);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Exists);
            Assert.Equal(0, result.ContentCount);
            Assert.Null(result.FirstSeen);
            Assert.Null(result.LastSeen);
            Assert.Equal(_testPublicKey, result.PublicKeyBase64);
        }

        [Fact]
        public async Task CheckUserExistsAsync_ReturningUser_ReturnsExists()
        {
            // Arrange
            var contentList = new List<StorageMetadata>
            {
                new StorageMetadata
                {
                    PublicKeyBase64 = _testPublicKey,
                    Timestamp = DateTime.UtcNow.AddDays(-5),
                    ContentAddress = "hash1",
                    Size = 1024
                },
                new StorageMetadata
                {
                    PublicKeyBase64 = _testPublicKey,
                    Timestamp = DateTime.UtcNow.AddDays(-2),
                    ContentAddress = "hash2",
                    Size = 2048
                },
                new StorageMetadata
                {
                    PublicKeyBase64 = "other-user-key",
                    Timestamp = DateTime.UtcNow.AddDays(-1),
                    ContentAddress = "hash3",
                    Size = 512
                }
            };

            _mockStorageAdapter.Setup(x => x.ListAllContentAsync())
                .Returns(contentList.ToAsyncEnumerable());

            // Act
            var result = await _service.CheckUserExistsAsync(_testPublicKey);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Exists);
            Assert.Equal(2, result.ContentCount);
            Assert.Equal(contentList[0].Timestamp, result.FirstSeen);
            Assert.Equal(contentList[1].Timestamp, result.LastSeen);
            Assert.Equal(_testPublicKey, result.PublicKeyBase64);
        }

        [Fact]
        public async Task CheckUserExistsAsync_CachesResult()
        {
            // Arrange
            var contentList = new List<StorageMetadata>
            {
                new StorageMetadata
                {
                    PublicKeyBase64 = _testPublicKey,
                    Timestamp = DateTime.UtcNow,
                    ContentAddress = "hash1",
                    Size = 1024
                }
            };

            _mockStorageAdapter.Setup(x => x.ListAllContentAsync())
                .Returns(contentList.ToAsyncEnumerable());

            // Act
            var result1 = await _service.CheckUserExistsAsync(_testPublicKey);
            var result2 = await _service.CheckUserExistsAsync(_testPublicKey);

            // Assert
            Assert.Same(result1, result2); // Should be the same cached instance
            _mockStorageAdapter.Verify(x => x.ListAllContentAsync(), Times.Once()); // Should only query once
        }

        [Fact]
        public async Task CheckUserExistsAsync_NullPublicKey_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _service.CheckUserExistsAsync(null!));
        }

        [Fact]
        public async Task CheckUserExistsAsync_EmptyPublicKey_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _service.CheckUserExistsAsync(string.Empty));
        }

        [Fact]
        public async Task CheckUserExistsAsync_StorageError_ReturnsPartialResult()
        {
            // Arrange
            _mockStorageAdapter.Setup(x => x.ListAllContentAsync())
                .Throws(new Exception("Storage error"));

            // Act
            var result = await _service.CheckUserExistsAsync(_testPublicKey);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Exists);
            Assert.Equal(0, result.ContentCount);
            Assert.Equal(_testPublicKey, result.PublicKeyBase64);
            
            // Verify error was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error querying storage")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region MarkUserAsActiveAsync Tests

        [Fact]
        public async Task MarkUserAsActiveAsync_ClearsCache()
        {
            // Arrange
            // First, populate the cache
            var contentList = new List<StorageMetadata>
            {
                new StorageMetadata
                {
                    PublicKeyBase64 = _testPublicKey,
                    Timestamp = DateTime.UtcNow,
                    ContentAddress = "hash1",
                    Size = 1024
                }
            };

            _mockStorageAdapter.Setup(x => x.ListAllContentAsync())
                .Returns(contentList.ToAsyncEnumerable());

            await _service.CheckUserExistsAsync(_testPublicKey); // Populate cache

            // Setup for second call with more content
            var updatedContentList = new List<StorageMetadata>
            {
                new StorageMetadata
                {
                    PublicKeyBase64 = _testPublicKey,
                    Timestamp = DateTime.UtcNow,
                    ContentAddress = "hash1",
                    Size = 1024
                },
                new StorageMetadata
                {
                    PublicKeyBase64 = _testPublicKey,
                    Timestamp = DateTime.UtcNow.AddMinutes(1),
                    ContentAddress = "hash2",
                    Size = 2048
                }
            };

            _mockStorageAdapter.Setup(x => x.ListAllContentAsync())
                .Returns(updatedContentList.ToAsyncEnumerable());

            // Act
            await _service.MarkUserAsActiveAsync(_testPublicKey);
            var result = await _service.CheckUserExistsAsync(_testPublicKey);

            // Assert
            Assert.Equal(2, result.ContentCount); // Should reflect updated content
            _mockStorageAdapter.Verify(x => x.ListAllContentAsync(), Times.Exactly(2)); // Should query twice
        }

        [Fact]
        public async Task MarkUserAsActiveAsync_NullPublicKey_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _service.MarkUserAsActiveAsync(null!));
        }

        #endregion

        #region GetUserActivityAsync Tests

        [Fact]
        public async Task GetUserActivityAsync_ReturnsCompleteSummary()
        {
            // Arrange
            var now = DateTime.UtcNow;
            var contentList = new List<StorageMetadata>
            {
                new StorageMetadata
                {
                    PublicKeyBase64 = _testPublicKey,
                    Timestamp = now.AddDays(-5),
                    ContentAddress = "hash1",
                    Size = 1024
                },
                new StorageMetadata
                {
                    PublicKeyBase64 = _testPublicKey,
                    Timestamp = now.AddDays(-2),
                    ContentAddress = "hash2",
                    Size = 2048
                },
                new StorageMetadata
                {
                    PublicKeyBase64 = _testPublicKey,
                    Timestamp = now,
                    ContentAddress = "hash3",
                    Size = 4096
                },
                new StorageMetadata
                {
                    PublicKeyBase64 = "other-user",
                    Timestamp = now,
                    ContentAddress = "hash4",
                    Size = 512
                }
            };

            _mockStorageAdapter.Setup(x => x.ListAllContentAsync())
                .Returns(contentList.ToAsyncEnumerable());

            // Act
            var result = await _service.GetUserActivityAsync(_testPublicKey);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.TotalContent);
            Assert.Equal(1024 + 2048 + 4096, result.TotalStorageBytes);
            Assert.Equal(now, result.LastActivity);
            Assert.Equal(3, result.RecentContentAddresses.Count);
            Assert.Equal("hash3", result.RecentContentAddresses[0]); // Most recent first
            Assert.Equal("hash2", result.RecentContentAddresses[1]);
            Assert.Equal("hash1", result.RecentContentAddresses[2]);
        }

        [Fact]
        public async Task GetUserActivityAsync_LimitsRecentContent()
        {
            // Arrange
            var contentList = new List<StorageMetadata>();
            for (int i = 0; i < 15; i++)
            {
                contentList.Add(new StorageMetadata
                {
                    PublicKeyBase64 = _testPublicKey,
                    Timestamp = DateTime.UtcNow.AddHours(-i),
                    ContentAddress = $"hash{i}",
                    Size = 1024
                });
            }

            _mockStorageAdapter.Setup(x => x.ListAllContentAsync())
                .Returns(contentList.ToAsyncEnumerable());

            // Act
            var result = await _service.GetUserActivityAsync(_testPublicKey);

            // Assert
            Assert.Equal(15, result.TotalContent);
            Assert.Equal(10, result.RecentContentAddresses.Count); // Should limit to 10
            Assert.Equal("hash0", result.RecentContentAddresses[0]); // Most recent
        }

        [Fact]
        public async Task GetUserActivityAsync_NoContent_ReturnsEmptySummary()
        {
            // Arrange
            _mockStorageAdapter.Setup(x => x.ListAllContentAsync())
                .Returns(GetEmptyAsyncEnumerable<StorageMetadata>());

            // Act
            var result = await _service.GetUserActivityAsync(_testPublicKey);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.TotalContent);
            Assert.Equal(0, result.TotalStorageBytes);
            Assert.Null(result.LastActivity);
            Assert.Empty(result.RecentContentAddresses);
        }

        [Fact]
        public async Task GetUserActivityAsync_StorageError_ReturnsPartialSummary()
        {
            // Arrange
            _mockStorageAdapter.Setup(x => x.ListAllContentAsync())
                .Throws(new Exception("Storage error"));

            // Act
            var result = await _service.GetUserActivityAsync(_testPublicKey);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.TotalContent);
            Assert.Equal(0, result.TotalStorageBytes);
            Assert.Null(result.LastActivity);
            Assert.Empty(result.RecentContentAddresses);
            
            // Verify error was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error getting user activity")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region ClearCache Tests

        [Fact]
        public async Task ClearCache_RemovesAllCachedData()
        {
            // Arrange
            var contentList = new List<StorageMetadata>
            {
                new StorageMetadata
                {
                    PublicKeyBase64 = _testPublicKey,
                    Timestamp = DateTime.UtcNow,
                    ContentAddress = "hash1",
                    Size = 1024
                }
            };

            _mockStorageAdapter.SetupSequence(x => x.ListAllContentAsync())
                .Returns(contentList.ToAsyncEnumerable())
                .Returns(contentList.ToAsyncEnumerable());

            // Populate cache
            await _service.CheckUserExistsAsync(_testPublicKey);

            // Act
            _service.ClearCache();
            await _service.CheckUserExistsAsync(_testPublicKey);

            // Assert
            _mockStorageAdapter.Verify(x => x.ListAllContentAsync(), Times.Exactly(2));
        }

        #endregion

        private static async IAsyncEnumerable<T> GetEmptyAsyncEnumerable<T>()
        {
            yield break;
        }
    }

    // Helper extension to convert IEnumerable to IAsyncEnumerable
    public static class AsyncEnumerableExtensions
    {
        public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
        {
            foreach (var item in source)
            {
                yield return item;
                await Task.CompletedTask;
            }
        }
    }
}