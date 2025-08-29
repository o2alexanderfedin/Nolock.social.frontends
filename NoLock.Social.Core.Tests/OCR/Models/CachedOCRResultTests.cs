using NoLock.Social.Core.OCR.Models;
using Xunit;

namespace NoLock.Social.Core.Tests.OCR.Models
{
    /// <summary>
    /// Unit tests for the CachedOCRResult model focusing on cache management.
    /// </summary>
    public class CachedOCRResultTests
    {
        [Fact]
        public void CachedOCRResult_DefaultValues()
        {
            // Arrange & Act
            var cachedResult = new CachedOCRResult();

            // Assert
            Assert.Equal(string.Empty, cachedResult.CacheKey);
            Assert.NotNull(cachedResult.Result);
            Assert.Null(cachedResult.DocumentType);
            Assert.Equal(default(DateTime), cachedResult.CachedAt);
            Assert.Equal(default(DateTime), cachedResult.ExpiresAt);
            Assert.Equal(0, cachedResult.AccessCount);
            Assert.Null(cachedResult.LastAccessedAt);
            Assert.Equal(0, cachedResult.SizeBytes);
            Assert.Null(cachedResult.Metadata);
        }

        [Theory]
        [InlineData("SHA256_HASH_123", "Receipt", 30)]
        [InlineData("CACHE_KEY_ABC", "Check", 60)]
        [InlineData("DOC_HASH_XYZ", null, 15)]
        public void Create_StaticMethod_InitializesCorrectly(string cacheKey, string documentType, int expirationMinutes)
        {
            // Arrange
            var ocrResponse = new OCRStatusResponse
            {
                TrackingId = "TRACK-123",
                Status = OCRProcessingStatus.Complete,
                ResultData = new OCRResultData
                {
                    ConfidenceScore = 85.5,
                    Metrics = new ProcessingMetrics { ProcessingTimeMs = 1500 }
                }
            };

            var before = DateTime.UtcNow;

            // Act
            var cachedResult = CachedOCRResult.Create(cacheKey, ocrResponse, expirationMinutes, documentType);
            var after = DateTime.UtcNow;

            // Assert
            Assert.Equal(cacheKey, cachedResult.CacheKey);
            Assert.Same(ocrResponse, cachedResult.Result);
            Assert.InRange(cachedResult.CachedAt, before.AddSeconds(-1), after.AddSeconds(1));
            Assert.InRange(cachedResult.ExpiresAt, 
                before.AddMinutes(expirationMinutes).AddSeconds(-1), 
                after.AddMinutes(expirationMinutes).AddSeconds(1));
            Assert.Equal(0, cachedResult.AccessCount);
            Assert.Null(cachedResult.LastAccessedAt);
            Assert.NotNull(cachedResult.Metadata);
            Assert.Equal("TRACK-123", cachedResult.Metadata.SourceTrackingId);
            Assert.Equal("Complete", cachedResult.Metadata.ProcessingStatus);
            Assert.Equal(85.5, cachedResult.Metadata.ConfidenceScore);
            Assert.Equal(1500, cachedResult.Metadata.ProcessingTimeMs);
        }

        [Fact]
        public void Create_DeterminesDocumentTypeFromExtractedFields()
        {
            // Arrange
            var ocrResponse = new OCRStatusResponse
            {
                ResultData = new OCRResultData
                {
                    ExtractedFields = new List<ExtractedField>
                    {
                        new ExtractedField { FieldName = "Amount", Value = "100.00" },
                        new ExtractedField { FieldName = "DocumentType", Value = "Invoice" },
                        new ExtractedField { FieldName = "Date", Value = "2024-01-15" }
                    }
                }
            };

            // Act
            var cachedResult = CachedOCRResult.Create("key", ocrResponse, 30, null);

            // Assert
            Assert.Equal("Invoice", cachedResult.DocumentType);
        }

        [Fact]
        public void RecordAccess_UpdatesAccessTracking()
        {
            // Arrange
            var cachedResult = new CachedOCRResult
            {
                AccessCount = 5,
                LastAccessedAt = DateTime.UtcNow.AddHours(-1)
            };

            var before = DateTime.UtcNow;

            // Act
            cachedResult.RecordAccess();
            var after = DateTime.UtcNow;

            // Assert
            Assert.Equal(6, cachedResult.AccessCount);
            Assert.NotNull(cachedResult.LastAccessedAt);
            Assert.InRange(cachedResult.LastAccessedAt.Value, before.AddSeconds(-1), after.AddSeconds(1));
        }

        [Theory]
        [InlineData(-60, true, "Expired 1 hour ago")]
        [InlineData(-1, true, "Expired 1 minute ago")]
        [InlineData(0, true, "Expires now")]
        [InlineData(1, false, "Expires in 1 minute")]
        [InlineData(60, false, "Expires in 1 hour")]
        public void IsExpired_ReturnsCorrectStatus(int minutesFromNow, bool expectedExpired, string scenario)
        {
            // Arrange
            var cachedResult = new CachedOCRResult
            {
                ExpiresAt = DateTime.UtcNow.AddMinutes(minutesFromNow)
            };

            // Act
            var isExpired = cachedResult.IsExpired;

            // Assert
            Assert.Equal(expectedExpired, isExpired);
        }

        [Theory]
        [InlineData(60, false)]
        [InlineData(30, false)]
        [InlineData(1, false)]
        [InlineData(-1, true)]
        public void TimeToLive_CalculatesCorrectly(int minutesFromNow, bool shouldBeNull)
        {
            // Arrange
            var cachedResult = new CachedOCRResult
            {
                ExpiresAt = DateTime.UtcNow.AddMinutes(minutesFromNow)
            };

            // Act
            var ttl = cachedResult.TimeToLive;

            // Assert
            if (shouldBeNull)
            {
                Assert.Null(ttl);
            }
            else
            {
                Assert.NotNull(ttl);
                // Allow for small time differences during test execution
                Assert.InRange(ttl.Value.TotalMinutes, minutesFromNow - 0.1, minutesFromNow + 0.1);
            }
        }

        [Fact]
        public void MultipleAccesses_TrackCorrectly()
        {
            // Arrange
            var cachedResult = new CachedOCRResult();
            var accessTimes = new List<DateTime?>();

            // Act
            for (int i = 0; i < 5; i++)
            {
                cachedResult.RecordAccess();
                accessTimes.Add(cachedResult.LastAccessedAt);
                System.Threading.Thread.Sleep(10); // Small delay between accesses
            }

            // Assert
            Assert.Equal(5, cachedResult.AccessCount);
            Assert.NotNull(cachedResult.LastAccessedAt);
            
            // Last access time should be the most recent
            for (int i = 1; i < accessTimes.Count; i++)
            {
                Assert.True(accessTimes[i] >= accessTimes[i - 1]);
            }
        }

        [Theory]
        [InlineData(1024, "1KB file")]
        [InlineData(1048576, "1MB file")]
        [InlineData(10485760, "10MB file")]
        [InlineData(0, "Empty file")]
        public void SizeBytes_VariousValues(long sizeBytes, string scenario)
        {
            // Arrange & Act
            var cachedResult = new CachedOCRResult
            {
                SizeBytes = sizeBytes
            };

            // Assert
            Assert.Equal(sizeBytes, cachedResult.SizeBytes);
        }

        [Fact]
        public void CacheKey_VariousFormats()
        {
            // Arrange & Act
            var keys = new[]
            {
                "sha256:abcdef123456",
                "md5:098765432",
                "custom_key_format",
                "USER_123_DOC_456",
                ""
            };

            foreach (var key in keys)
            {
                var cachedResult = new CachedOCRResult { CacheKey = key };
                
                // Assert
                Assert.Equal(key, cachedResult.CacheKey);
            }
        }

        [Fact]
        public void DocumentType_Assignment()
        {
            // Arrange
            var documentTypes = new[] { "Receipt", "Check", "Invoice", "W2", "W4", "1099", null };

            foreach (var docType in documentTypes)
            {
                // Act
                var cachedResult = new CachedOCRResult { DocumentType = docType };

                // Assert
                Assert.Equal(docType, cachedResult.DocumentType);
            }
        }

        [Fact]
        public void Create_WithNullResultData_HandlesGracefully()
        {
            // Arrange
            var ocrResponse = new OCRStatusResponse
            {
                TrackingId = "TRACK-456",
                Status = OCRProcessingStatus.Processing,
                ResultData = null
            };

            // Act
            var cachedResult = CachedOCRResult.Create("key", ocrResponse, 30);

            // Assert
            Assert.NotNull(cachedResult);
            Assert.NotNull(cachedResult.Metadata);
            Assert.Null(cachedResult.Metadata.ConfidenceScore);
            Assert.Null(cachedResult.Metadata.ProcessingTimeMs);
        }

        [Fact]
        public void Create_WithNullMetrics_HandlesGracefully()
        {
            // Arrange
            var ocrResponse = new OCRStatusResponse
            {
                TrackingId = "TRACK-789",
                ResultData = new OCRResultData
                {
                    ConfidenceScore = 90.0,
                    Metrics = null
                }
            };

            // Act
            var cachedResult = CachedOCRResult.Create("key", ocrResponse, 30);

            // Assert
            Assert.NotNull(cachedResult);
            Assert.NotNull(cachedResult.Metadata);
            Assert.Equal(90.0, cachedResult.Metadata.ConfidenceScore);
            Assert.Null(cachedResult.Metadata.ProcessingTimeMs);
        }

        [Theory]
        [InlineData(OCRProcessingStatus.Queued, "Queued")]
        [InlineData(OCRProcessingStatus.Processing, "Processing")]
        [InlineData(OCRProcessingStatus.Complete, "Complete")]
        [InlineData(OCRProcessingStatus.Failed, "Failed")]
        // [InlineData(OCRProcessingStatus.Cancelled, "Cancelled")] // Status doesn't exist in enum
        public void Create_CapturesProcessingStatus(OCRProcessingStatus status, string expectedStatusString)
        {
            // Arrange
            var ocrResponse = new OCRStatusResponse
            {
                Status = status
            };

            // Act
            var cachedResult = CachedOCRResult.Create("key", ocrResponse, 30);

            // Assert
            Assert.Equal(expectedStatusString, cachedResult.Metadata.ProcessingStatus);
        }

        [Fact]
        public void ExpirationBoundaries_WorkCorrectly()
        {
            // Arrange & Act
            var shortExpiry = CachedOCRResult.Create("key1", new OCRStatusResponse(), 1);
            var mediumExpiry = CachedOCRResult.Create("key2", new OCRStatusResponse(), 60);
            var longExpiry = CachedOCRResult.Create("key3", new OCRStatusResponse(), 1440); // 24 hours

            // Assert
            Assert.True(shortExpiry.ExpiresAt < mediumExpiry.ExpiresAt);
            Assert.True(mediumExpiry.ExpiresAt < longExpiry.ExpiresAt);
            
            // Verify approximate expiration times
            var now = DateTime.UtcNow;
            Assert.InRange(shortExpiry.ExpiresAt, now, now.AddMinutes(2));
            Assert.InRange(mediumExpiry.ExpiresAt, now.AddMinutes(59), now.AddMinutes(61));
            Assert.InRange(longExpiry.ExpiresAt, now.AddMinutes(1439), now.AddMinutes(1441));
        }

        [Fact]
        public void CachedResult_IndependentInstances()
        {
            // Arrange & Act
            var result1 = CachedOCRResult.Create("key1", new OCRStatusResponse(), 30);
            var result2 = CachedOCRResult.Create("key2", new OCRStatusResponse(), 60);

            // Assert
            Assert.NotEqual(result1.CacheKey, result2.CacheKey);
            Assert.NotEqual(result1.ExpiresAt, result2.ExpiresAt);
            Assert.NotSame(result1.Result, result2.Result);
            Assert.NotSame(result1.Metadata, result2.Metadata);
        }
    }
}