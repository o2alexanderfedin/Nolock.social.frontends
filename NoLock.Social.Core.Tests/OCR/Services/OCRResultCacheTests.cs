using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Core.Hashing;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Models;
using NoLock.Social.Core.OCR.Services;
using NoLock.Social.Core.Storage;
using Xunit;

namespace NoLock.Social.Core.Tests.OCR.Services
{
    /// <summary>
    /// Unit tests for OCRResultCache implementation.
    /// </summary>
    public class OCRResultCacheTests
    {
        private readonly Mock<IContentAddressableStorage> _mockStorage;
        private readonly Mock<IHashAlgorithm> _mockHashAlgorithm;
        private readonly Mock<ILogger<OCRResultCache>> _mockLogger;
        private readonly OCRResultCache _cache;
        private const int DefaultExpirationMinutes = 60;

        public OCRResultCacheTests()
        {
            _mockStorage = new Mock<IContentAddressableStorage>();
            _mockHashAlgorithm = new Mock<IHashAlgorithm>();
            _mockLogger = new Mock<ILogger<OCRResultCache>>();
            _cache = new OCRResultCache(
                _mockStorage.Object,
                _mockHashAlgorithm.Object,
                _mockLogger.Object,
                DefaultExpirationMinutes);
        }

        [Theory]
        [InlineData(new byte[] { 1, 2, 3 }, "test-hash-123", "Receipt")]
        [InlineData(new byte[] { 4, 5, 6 }, "test-hash-456", "Check")]
        [InlineData(new byte[] { 7, 8, 9 }, "test-hash-789", null)]
        public async Task StoreResultAsync_ValidInput_StoresAndReturnsHash(
            byte[] documentContent,
            string expectedHash,
            string documentType)
        {
            // Arrange
            var result = CreateTestStatusResponse(documentType);
            var hashBytes = Convert.FromBase64String("dGVzdC1oYXNoLXZhbHVl");
            _mockHashAlgorithm.Setup(h => h.ComputeHash(documentContent))
                .Returns(hashBytes);
            _mockStorage.Setup(s => s.StoreAsync(It.IsAny<byte[]>()))
                .ReturnsAsync(expectedHash);

            // Act
            var cacheKey = await _cache.StoreResultAsync(
                documentContent,
                result,
                30,
                CancellationToken.None);

            // Assert
            Assert.NotNull(cacheKey);
            Assert.NotEmpty(cacheKey);
            _mockStorage.Verify(s => s.StoreAsync(It.IsAny<byte[]>()), Times.Once);
            _mockHashAlgorithm.Verify(h => h.ComputeHash(documentContent), Times.Once);
        }

        [Fact]
        public async Task StoreResultAsync_NullDocument_ThrowsArgumentNullException()
        {
            // Arrange
            var result = CreateTestStatusResponse();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _cache.StoreResultAsync(null!, result));
        }

        [Fact]
        public async Task StoreResultAsync_NullResult_ThrowsArgumentNullException()
        {
            // Arrange
            var documentContent = new byte[] { 1, 2, 3 };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _cache.StoreResultAsync(documentContent, null!));
        }

        [Theory]
        [InlineData(true, false)] // Not expired, should return result
        [InlineData(false, true)] // Expired, should return null
        public async Task GetResultAsync_ChecksExpiration_ReturnsAccordingly(
            bool shouldReturnResult,
            bool isExpired)
        {
            // Arrange
            var documentContent = new byte[] { 1, 2, 3 };
            var cacheKey = "test-cache-key";
            var hashBytes = Convert.FromBase64String("dGVzdC1oYXNoLXZhbHVl");
            
            _mockHashAlgorithm.Setup(h => h.ComputeHash(documentContent))
                .Returns(hashBytes);

            if (shouldReturnResult)
            {
                var cachedResult = CreateCachedResult(cacheKey, isExpired);
                var json = System.Text.Json.JsonSerializer.Serialize(cachedResult);
                var bytes = Encoding.UTF8.GetBytes(json);
                
                _mockStorage.Setup(s => s.GetAsync(It.IsAny<string>()))
                    .ReturnsAsync(bytes);
            }
            else
            {
                _mockStorage.Setup(s => s.GetAsync(It.IsAny<string>()))
                    .ReturnsAsync((byte[]?)null);
            }

            // Act
            var result = await _cache.GetResultAsync(documentContent);

            // Assert
            if (shouldReturnResult && !isExpired)
            {
                Assert.NotNull(result);
            }
            else
            {
                Assert.Null(result);
            }
        }

        [Fact]
        public async Task GetResultAsync_CacheMiss_ReturnsNull()
        {
            // Arrange
            var documentContent = new byte[] { 1, 2, 3 };
            var hashBytes = Convert.FromBase64String("dGVzdC1oYXNoLXZhbHVl");
            
            _mockHashAlgorithm.Setup(h => h.ComputeHash(documentContent))
                .Returns(hashBytes);
            _mockStorage.Setup(s => s.GetAsync(It.IsAny<string>()))
                .ReturnsAsync((byte[]?)null);

            // Act
            var result = await _cache.GetResultAsync(documentContent);

            // Assert
            Assert.Null(result);
        }

        [Theory]
        [InlineData("Receipt", 2)]
        [InlineData("Check", 1)]
        [InlineData("W4", 0)]
        public async Task InvalidateByTypeAsync_RemovesCorrectEntries(
            string documentType,
            int expectedCount)
        {
            // Arrange
            var keys = new[] { "key1", "key2", "key3" };
            var documents = new[]
            {
                CreateCachedResult("key1", false, "Receipt"),
                CreateCachedResult("key2", false, "Receipt"),
                CreateCachedResult("key3", false, "Check")
            };

            _mockStorage.Setup(s => s.GetAllHashesAsync())
                .Returns(ToAsyncEnumerable(keys));

            for (int i = 0; i < keys.Length; i++)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(documents[i]);
                var bytes = Encoding.UTF8.GetBytes(json);
                _mockStorage.Setup(s => s.GetAsync(keys[i]))
                    .ReturnsAsync(bytes);
            }

            _mockStorage.Setup(s => s.DeleteAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var count = await _cache.InvalidateByTypeAsync(documentType);

            // Assert
            Assert.Equal(expectedCount, count);
        }

        [Fact]
        public async Task CleanupExpiredAsync_RemovesOnlyExpiredEntries()
        {
            // Arrange
            var keys = new[] { "key1", "key2", "key3" };
            var documents = new[]
            {
                CreateCachedResult("key1", true),  // Expired
                CreateCachedResult("key2", false), // Not expired
                CreateCachedResult("key3", true)   // Expired
            };

            _mockStorage.Setup(s => s.GetAllHashesAsync())
                .Returns(ToAsyncEnumerable(keys));

            for (int i = 0; i < keys.Length; i++)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(documents[i]);
                var bytes = Encoding.UTF8.GetBytes(json);
                _mockStorage.Setup(s => s.GetAsync(keys[i]))
                    .ReturnsAsync(bytes);
            }

            _mockStorage.Setup(s => s.DeleteAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var removedCount = await _cache.CleanupExpiredAsync();

            // Assert
            Assert.Equal(2, removedCount); // Two expired entries
            _mockStorage.Verify(s => s.DeleteAsync(It.IsAny<string>()), Times.Exactly(2));
        }

        [Fact]
        public async Task ClearAllAsync_RemovesAllEntries()
        {
            // Arrange
            var keys = new[] { "key1", "key2", "key3" };
            _mockStorage.Setup(s => s.GetAllHashesAsync())
                .Returns(ToAsyncEnumerable(keys));
            _mockStorage.Setup(s => s.ClearAsync())
                .Returns(ValueTask.CompletedTask);

            // Act
            var count = await _cache.ClearAllAsync();

            // Assert
            Assert.Equal(3, count);
            _mockStorage.Verify(s => s.ClearAsync(), Times.Once);
        }

        [Fact]
        public async Task GetStatisticsAsync_ReturnsCorrectStatistics()
        {
            // Arrange
            var keys = new[] { "key1", "key2" };
            _mockStorage.Setup(s => s.GetAllHashesAsync())
                .Returns(ToAsyncEnumerable(keys));
            _mockStorage.Setup(s => s.GetSizeAsync(It.IsAny<string>()))
                .ReturnsAsync(1024);

            // Act
            var stats = await _cache.GetStatisticsAsync();

            // Assert
            Assert.NotNull(stats);
            Assert.Equal(2, stats.EntryCount);
            Assert.Equal(2048, stats.TotalSizeBytes);
            Assert.Equal(1024, stats.AverageEntrySizeBytes);
        }

        [Theory]
        [InlineData(100, 50, 66.67)]
        [InlineData(75, 25, 75.0)]
        [InlineData(0, 0, 0.0)]
        public void CacheStatistics_HitRate_CalculatesCorrectly(
            long hitCount,
            long missCount,
            double expectedHitRate)
        {
            // Arrange
            var stats = new CacheStatistics
            {
                HitCount = hitCount,
                MissCount = missCount
            };

            // Act
            var hitRate = stats.HitRate;

            // Assert
            Assert.Equal(expectedHitRate, hitRate, 2);
        }

        #region Helper Methods

        private OCRStatusResponse CreateTestStatusResponse(string? documentType = null)
        {
            var response = new OCRStatusResponse
            {
                TrackingId = "TEST-123",
                Status = OCRProcessingStatus.Complete,
                SubmittedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow.AddMinutes(2),
                ResultData = new OCRResultData
                {
                    ExtractedText = "Test OCR text",
                    ConfidenceScore = 95.5,
                    DetectedLanguage = "en",
                    PageCount = 1,
                    StructuredData = "{}"
                }
            };

            if (documentType != null)
            {
                response.ResultData.ExtractedFields = new[]
                {
                    new ExtractedField
                    {
                        FieldName = "DocumentType",
                        Value = documentType,
                        Confidence = 98.0
                    }
                };
            }

            return response;
        }

        private CachedOCRResult CreateCachedResult(
            string cacheKey,
            bool isExpired,
            string? documentType = null)
        {
            var now = DateTime.UtcNow;
            return new CachedOCRResult
            {
                CacheKey = cacheKey,
                Result = CreateTestStatusResponse(documentType),
                DocumentType = documentType,
                CachedAt = now.AddMinutes(-30),
                ExpiresAt = isExpired ? now.AddMinutes(-1) : now.AddMinutes(30),
                AccessCount = 0,
                SizeBytes = 1024
            };
        }

        private async IAsyncEnumerable<T> ToAsyncEnumerable<T>(params T[] items)
        {
            foreach (var item in items)
            {
                yield return item;
                await Task.CompletedTask;
            }
        }

        #endregion
    }
}