using NoLock.Social.Core.OCR.Models;
using Xunit;

namespace NoLock.Social.Core.Tests.OCR.Models
{
    /// <summary>
    /// Unit tests for the CacheMetadata model.
    /// </summary>
    public class CacheMetadataTests
    {
        [Fact]
        public void CacheMetadata_DefaultValues()
        {
            // Arrange & Act
            var metadata = new CacheMetadata();

            // Assert
            Assert.Null(metadata.SourceTrackingId);
            Assert.Null(metadata.ProcessingStatus);
            Assert.Null(metadata.ConfidenceScore);
            Assert.Null(metadata.ProcessingTimeMs);
            Assert.Null(metadata.Tags);
            Assert.Equal("1.0", metadata.CacheVersion);
        }

        [Theory]
        [InlineData("TRACK-123", "Complete", 95.5, 1500)]
        [InlineData("TRACK-456", "Processing", 0.0, 0)]
        [InlineData(null, null, null, null)]
        public void CacheMetadata_PropertiesSetCorrectly(
            string trackingId, 
            string status, 
            double? confidence, 
            long? processingTime)
        {
            // Arrange & Act
            var metadata = new CacheMetadata
            {
                SourceTrackingId = trackingId,
                ProcessingStatus = status,
                ConfidenceScore = confidence,
                ProcessingTimeMs = processingTime
            };

            // Assert
            Assert.Equal(trackingId, metadata.SourceTrackingId);
            Assert.Equal(status, metadata.ProcessingStatus);
            Assert.Equal(confidence, metadata.ConfidenceScore);
            Assert.Equal(processingTime, metadata.ProcessingTimeMs);
        }

        [Fact]
        public void Tags_CanBeSetAndRetrieved()
        {
            // Arrange
            var tags = new[] { "receipt", "expense", "Q1-2024", "approved" };

            // Act
            var metadata = new CacheMetadata
            {
                Tags = tags
            };

            // Assert
            Assert.NotNull(metadata.Tags);
            Assert.Equal(4, metadata.Tags.Length);
            Assert.Contains("receipt", metadata.Tags);
            Assert.Contains("expense", metadata.Tags);
            Assert.Contains("Q1-2024", metadata.Tags);
            Assert.Contains("approved", metadata.Tags);
        }

        [Theory]
        [InlineData("1.0", "Default version")]
        [InlineData("2.0", "Updated version")]
        [InlineData("1.1-beta", "Beta version")]
        [InlineData("", "Empty version")]
        public void CacheVersion_VariousValues(string version, string scenario)
        {
            // Arrange & Act
            var metadata = new CacheMetadata
            {
                CacheVersion = version
            };

            // Assert
            Assert.Equal(version, metadata.CacheVersion);
        }

        [Theory]
        [InlineData(0.0, "Zero confidence")]
        [InlineData(50.0, "Medium confidence")]
        [InlineData(100.0, "Perfect confidence")]
        [InlineData(null, "Null confidence")]
        public void ConfidenceScore_VariousValues(double? confidence, string scenario)
        {
            // Arrange & Act
            var metadata = new CacheMetadata
            {
                ConfidenceScore = confidence
            };

            // Assert
            Assert.Equal(confidence, metadata.ConfidenceScore);
        }

        [Theory]
        [InlineData(0, "Instant processing")]
        [InlineData(1500, "Fast processing")]
        [InlineData(10000, "Slow processing")]
        [InlineData(long.MaxValue, "Maximum time")]
        [InlineData(null, "Null time")]
        public void ProcessingTimeMs_VariousValues(long? processingTime, string scenario)
        {
            // Arrange & Act
            var metadata = new CacheMetadata
            {
                ProcessingTimeMs = processingTime
            };

            // Assert
            Assert.Equal(processingTime, metadata.ProcessingTimeMs);
        }

        [Theory]
        [InlineData("Queued", "Queued status")]
        [InlineData("Processing", "Processing status")]
        [InlineData("Complete", "Complete status")]
        [InlineData("Failed", "Failed status")]
        [InlineData("Cancelled", "Cancelled status")]
        [InlineData(null, "Null status")]
        public void ProcessingStatus_VariousValues(string status, string scenario)
        {
            // Arrange & Act
            var metadata = new CacheMetadata
            {
                ProcessingStatus = status
            };

            // Assert
            Assert.Equal(status, metadata.ProcessingStatus);
        }

        [Fact]
        public void EmptyTags_HandledCorrectly()
        {
            // Arrange & Act
            var metadata = new CacheMetadata
            {
                Tags = new string[] { }
            };

            // Assert
            Assert.NotNull(metadata.Tags);
            Assert.Empty(metadata.Tags);
        }

        [Fact]
        public void NullTags_HandledCorrectly()
        {
            // Arrange & Act
            var metadata = new CacheMetadata
            {
                Tags = null
            };

            // Assert
            Assert.Null(metadata.Tags);
        }

        [Fact]
        public void SingleTag_WorksCorrectly()
        {
            // Arrange & Act
            var metadata = new CacheMetadata
            {
                Tags = new[] { "important" }
            };

            // Assert
            Assert.Single(metadata.Tags);
            Assert.Equal("important", metadata.Tags[0]);
        }

        [Fact]
        public void DuplicateTags_Allowed()
        {
            // Arrange & Act
            var metadata = new CacheMetadata
            {
                Tags = new[] { "tag1", "tag2", "tag1", "tag3", "tag2" }
            };

            // Assert
            Assert.Equal(5, metadata.Tags.Length);
            Assert.Equal(2, metadata.Tags.Count(t => t == "tag1"));
            Assert.Equal(2, metadata.Tags.Count(t => t == "tag2"));
            Assert.Equal(1, metadata.Tags.Count(t => t == "tag3"));
        }

        [Theory]
        [InlineData("TRACK-ABC-123-XYZ", "Complex tracking ID")]
        [InlineData("123456789", "Numeric tracking ID")]
        [InlineData("", "Empty tracking ID")]
        [InlineData(null, "Null tracking ID")]
        public void SourceTrackingId_VariousFormats(string trackingId, string scenario)
        {
            // Arrange & Act
            var metadata = new CacheMetadata
            {
                SourceTrackingId = trackingId
            };

            // Assert
            Assert.Equal(trackingId, metadata.SourceTrackingId);
        }

        [Fact]
        public void AllPropertiesSet_WorksTogether()
        {
            // Arrange & Act
            var metadata = new CacheMetadata
            {
                SourceTrackingId = "TRACK-FULL-TEST",
                ProcessingStatus = "Complete",
                ConfidenceScore = 92.5,
                ProcessingTimeMs = 2500,
                Tags = new[] { "test", "full", "metadata" },
                CacheVersion = "2.1"
            };

            // Assert
            Assert.Equal("TRACK-FULL-TEST", metadata.SourceTrackingId);
            Assert.Equal("Complete", metadata.ProcessingStatus);
            Assert.Equal(92.5, metadata.ConfidenceScore);
            Assert.Equal(2500, metadata.ProcessingTimeMs);
            Assert.Equal(3, metadata.Tags.Length);
            Assert.Equal("2.1", metadata.CacheVersion);
        }

        [Fact]
        public void IndependentInstances_DoNotInterfere()
        {
            // Arrange & Act
            var metadata1 = new CacheMetadata
            {
                SourceTrackingId = "TRACK-1",
                ConfidenceScore = 80.0,
                Tags = new[] { "tag1" }
            };

            var metadata2 = new CacheMetadata
            {
                SourceTrackingId = "TRACK-2",
                ConfidenceScore = 90.0,
                Tags = new[] { "tag2", "tag3" }
            };

            // Assert
            Assert.NotEqual(metadata1.SourceTrackingId, metadata2.SourceTrackingId);
            Assert.NotEqual(metadata1.ConfidenceScore, metadata2.ConfidenceScore);
            Assert.NotEqual(metadata1.Tags.Length, metadata2.Tags.Length);
        }

        [Fact]
        public void TagsModification_Works()
        {
            // Arrange
            var metadata = new CacheMetadata
            {
                Tags = new[] { "original" }
            };

            // Act
            metadata.Tags = new[] { "modified", "tags", "list" };

            // Assert
            Assert.Equal(3, metadata.Tags.Length);
            Assert.DoesNotContain("original", metadata.Tags);
            Assert.Contains("modified", metadata.Tags);
            Assert.Contains("tags", metadata.Tags);
            Assert.Contains("list", metadata.Tags);
        }

        [Theory]
        [InlineData(85.5, 1500, true, "High confidence, fast processing")]
        [InlineData(45.0, 5000, false, "Low confidence, slow processing")]
        [InlineData(null, null, false, "No metrics available")]
        public void PerformanceMetrics_Evaluation(
            double? confidence, 
            long? processingTime, 
            bool isGoodPerformance,
            string scenario)
        {
            // Arrange
            var metadata = new CacheMetadata
            {
                ConfidenceScore = confidence,
                ProcessingTimeMs = processingTime
            };

            // Act
            bool meetsPerformanceCriteria = 
                metadata.ConfidenceScore.HasValue && metadata.ConfidenceScore.Value > 70.0 &&
                metadata.ProcessingTimeMs.HasValue && metadata.ProcessingTimeMs.Value < 3000;

            // Assert
            Assert.Equal(isGoodPerformance, meetsPerformanceCriteria);
        }
    }
}