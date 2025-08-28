using System;
using FluentAssertions;
using NoLock.Social.Core.Performance;
using Xunit;

namespace NoLock.Social.Core.Tests.Performance
{
    public class PerformanceThresholdsTests
    {
        [Theory]
        [InlineData(100, 200, true, "Duration exceeds threshold")]
        [InlineData(100, 50, false, "Duration within threshold")]
        [InlineData(100, 100, false, "Duration equals threshold")]
        [InlineData(500, 600, true, "Large duration exceeds")]
        [InlineData(1000, 999, false, "Just under threshold")]
        public void IsViolated_WithMetrics_DurationThreshold(int thresholdMs, int actualMs, bool expectedViolation, string scenario)
        {
            // Arrange
            var threshold = new PerformanceThresholds
            {
                MaxDuration = TimeSpan.FromMilliseconds(thresholdMs)
            };

            var metrics = new PerformanceMetrics
            {
                Duration = TimeSpan.FromMilliseconds(actualMs),
                MemoryUsedBytes = 1024
            };

            // Act
            var isViolated = threshold.IsViolated(metrics);

            // Assert
            isViolated.Should().Be(expectedViolation, scenario);
        }

        [Theory]
        [InlineData(1048576, 2097152, true, "Memory exceeds threshold")]
        [InlineData(1048576, 524288, false, "Memory within threshold")]
        [InlineData(1048576, 1048576, false, "Memory equals threshold")]
        [InlineData(10485760, 20971520, true, "Large memory exceeds")]
        [InlineData(5242880, 5242879, false, "Just under memory threshold")]
        public void IsViolated_WithMetrics_MemoryThreshold(long thresholdBytes, long actualBytes, bool expectedViolation, string scenario)
        {
            // Arrange
            var threshold = new PerformanceThresholds
            {
                MaxMemoryBytes = thresholdBytes
            };

            var metrics = new PerformanceMetrics
            {
                Duration = TimeSpan.FromMilliseconds(100),
                MemoryUsedBytes = actualBytes
            };

            // Act
            var isViolated = threshold.IsViolated(metrics);

            // Assert
            isViolated.Should().Be(expectedViolation, scenario);
        }

        [Theory]
        [InlineData(100, 200, 1048576, 2097152, true, "Both thresholds exceeded")]
        [InlineData(100, 50, 1048576, 524288, false, "Both thresholds met")]
        [InlineData(100, 200, 1048576, 524288, true, "Only duration exceeded")]
        [InlineData(100, 50, 1048576, 2097152, true, "Only memory exceeded")]
        public void IsViolated_WithMetrics_CombinedThresholds(
            int durationThresholdMs, int actualDurationMs,
            long memoryThresholdBytes, long actualMemoryBytes,
            bool expectedViolation, string scenario)
        {
            // Arrange
            var threshold = new PerformanceThresholds
            {
                MaxDuration = TimeSpan.FromMilliseconds(durationThresholdMs),
                MaxMemoryBytes = memoryThresholdBytes
            };

            var metrics = new PerformanceMetrics
            {
                Duration = TimeSpan.FromMilliseconds(actualDurationMs),
                MemoryUsedBytes = actualMemoryBytes
            };

            // Act
            var isViolated = threshold.IsViolated(metrics);

            // Assert
            isViolated.Should().Be(expectedViolation, scenario);
        }

        [Theory]
        [InlineData(95.0, 96.0, false, "Success rate above threshold")]
        [InlineData(95.0, 94.0, true, "Success rate below threshold")]
        [InlineData(95.0, 95.0, false, "Success rate equals threshold")]
        [InlineData(99.9, 99.8, true, "High precision threshold violated")]
        [InlineData(50.0, 75.0, false, "Success rate well above threshold")]
        public void IsViolated_WithStatistics_SuccessRateThreshold(
            double minSuccessRate, double actualSuccessRate, bool expectedViolation, string scenario)
        {
            // Arrange
            var threshold = new PerformanceThresholds
            {
                MinSuccessRate = minSuccessRate
            };

            var stats = new PerformanceStatistics
            {
                TotalExecutions = 100,
                SuccessfulExecutions = (int)(actualSuccessRate),
                AverageDuration = TimeSpan.FromMilliseconds(100),
                AverageMemoryUsedBytes = 1024
            };

            // Act
            var isViolated = threshold.IsViolated(stats);

            // Assert
            isViolated.Should().Be(expectedViolation, scenario);
        }

        [Theory]
        [InlineData(100, 200, 1048576, 2097152, 95.0, 94.0, true, "All thresholds violated")]
        [InlineData(100, 50, 1048576, 524288, 95.0, 96.0, false, "All thresholds met")]
        [InlineData(100, 200, 1048576, 524288, 95.0, 96.0, true, "Duration only violated")]
        [InlineData(100, 50, 1048576, 2097152, 95.0, 96.0, true, "Memory only violated")]
        [InlineData(100, 50, 1048576, 524288, 95.0, 94.0, true, "Success rate only violated")]
        public void IsViolated_WithStatistics_AllThresholds(
            int durationThresholdMs, int avgDurationMs,
            long memoryThresholdBytes, long avgMemoryBytes,
            double minSuccessRate, double actualSuccessRate,
            bool expectedViolation, string scenario)
        {
            // Arrange
            var threshold = new PerformanceThresholds
            {
                MaxDuration = TimeSpan.FromMilliseconds(durationThresholdMs),
                MaxMemoryBytes = memoryThresholdBytes,
                MinSuccessRate = minSuccessRate
            };

            var stats = new PerformanceStatistics
            {
                TotalExecutions = 100,
                SuccessfulExecutions = (int)actualSuccessRate,
                AverageDuration = TimeSpan.FromMilliseconds(avgDurationMs),
                AverageMemoryUsedBytes = avgMemoryBytes
            };

            // Act
            var isViolated = threshold.IsViolated(stats);

            // Assert
            isViolated.Should().Be(expectedViolation, scenario);
        }

        [Fact]
        public void IsViolated_WithMetrics_NoThresholds_ShouldReturnFalse()
        {
            // Arrange
            var threshold = new PerformanceThresholds();
            var metrics = new PerformanceMetrics
            {
                Duration = TimeSpan.FromMilliseconds(10000),
                MemoryUsedBytes = long.MaxValue
            };

            // Act
            var isViolated = threshold.IsViolated(metrics);

            // Assert
            isViolated.Should().BeFalse();
        }

        [Fact]
        public void IsViolated_WithStatistics_NoThresholds_ShouldReturnFalse()
        {
            // Arrange
            var threshold = new PerformanceThresholds();
            var stats = new PerformanceStatistics
            {
                TotalExecutions = 100,
                SuccessfulExecutions = 0,
                AverageDuration = TimeSpan.FromMilliseconds(10000),
                AverageMemoryUsedBytes = long.MaxValue
            };

            // Act
            var isViolated = threshold.IsViolated(stats);

            // Assert
            isViolated.Should().BeFalse();
        }

        [Theory]
        [InlineData(null, 1048576L, null)]
        [InlineData(100, null, null)]
        [InlineData(null, null, 95.0)]
        [InlineData(100, 1048576L, null)]
        [InlineData(null, 1048576L, 95.0)]
        public void PerformanceThresholds_PartialConfiguration_ShouldWork(
            int? durationMs, long? memoryBytes, double? minSuccessRate)
        {
            // Arrange
            var threshold = new PerformanceThresholds
            {
                MaxDuration = durationMs.HasValue ? TimeSpan.FromMilliseconds(durationMs.Value) : null,
                MaxMemoryBytes = memoryBytes,
                MinSuccessRate = minSuccessRate
            };

            // Act & Assert
            threshold.MaxDuration.HasValue.Should().Be(durationMs.HasValue);
            threshold.MaxMemoryBytes.HasValue.Should().Be(memoryBytes.HasValue);
            threshold.MinSuccessRate.HasValue.Should().Be(minSuccessRate.HasValue);
        }
    }
}