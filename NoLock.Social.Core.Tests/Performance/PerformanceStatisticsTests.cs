using System;
using FluentAssertions;
using NoLock.Social.Core.Performance;
using Xunit;

namespace NoLock.Social.Core.Tests.Performance
{
    public class PerformanceStatisticsTests
    {
        [Theory]
        [InlineData("Op1", 100, 90, 10)]
        [InlineData("Op2", 1000, 950, 50)]
        [InlineData("Op3", 50, 25, 25)]
        [InlineData("Op4", 1, 1, 0)]
        [InlineData("Op5", 0, 0, 0)]
        public void PerformanceStatistics_BasicProperties_ShouldWorkCorrectly(
            string operationName, int total, int successful, int failed)
        {
            // Arrange & Act
            var stats = new PerformanceStatistics
            {
                OperationName = operationName,
                TotalExecutions = total,
                SuccessfulExecutions = successful,
                FailedExecutions = failed
            };

            // Assert
            stats.OperationName.Should().Be(operationName);
            stats.TotalExecutions.Should().Be(total);
            stats.SuccessfulExecutions.Should().Be(successful);
            stats.FailedExecutions.Should().Be(failed);
        }

        [Theory]
        [InlineData(100, 100, 100.0)]
        [InlineData(100, 75, 75.0)]
        [InlineData(100, 50, 50.0)]
        [InlineData(100, 25, 25.0)]
        [InlineData(100, 0, 0.0)]
        [InlineData(0, 0, 0.0)]
        [InlineData(1000, 999, 99.9)]
        [InlineData(3, 2, 66.66666666666667)]
        public void PerformanceStatistics_SuccessRate_ShouldCalculateCorrectly(
            int total, int successful, double expectedRate)
        {
            // Arrange
            var stats = new PerformanceStatistics
            {
                TotalExecutions = total,
                SuccessfulExecutions = successful,
                FailedExecutions = total - successful
            };

            // Act
            var successRate = stats.SuccessRate;

            // Assert
            successRate.Should().BeApproximately(expectedRate, 0.0001);
        }

        [Theory]
        [InlineData(10, 100, 50, 55, 90, 95)]
        [InlineData(1, 1000, 500, 500, 999, 999)]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(100, 500, 300, 300, 450, 490)]
        public void PerformanceStatistics_DurationProperties_ShouldStoreCorrectly(
            int minMs, int maxMs, int avgMs, int medianMs, int p95Ms, int p99Ms)
        {
            // Arrange & Act
            var stats = new PerformanceStatistics
            {
                MinDuration = TimeSpan.FromMilliseconds(minMs),
                MaxDuration = TimeSpan.FromMilliseconds(maxMs),
                AverageDuration = TimeSpan.FromMilliseconds(avgMs),
                MedianDuration = TimeSpan.FromMilliseconds(medianMs),
                P95Duration = TimeSpan.FromMilliseconds(p95Ms),
                P99Duration = TimeSpan.FromMilliseconds(p99Ms)
            };

            // Assert
            stats.MinDuration.TotalMilliseconds.Should().Be(minMs);
            stats.MaxDuration.TotalMilliseconds.Should().Be(maxMs);
            stats.AverageDuration.TotalMilliseconds.Should().Be(avgMs);
            stats.MedianDuration.TotalMilliseconds.Should().Be(medianMs);
            stats.P95Duration.TotalMilliseconds.Should().Be(p95Ms);
            stats.P99Duration.TotalMilliseconds.Should().Be(p99Ms);
        }

        [Theory]
        [InlineData(1024)]
        [InlineData(1048576)]
        [InlineData(0)]
        [InlineData(long.MaxValue)]
        [InlineData(12345678)]
        public void PerformanceStatistics_AverageMemoryUsedBytes_ShouldStore(long memoryBytes)
        {
            // Arrange & Act
            var stats = new PerformanceStatistics
            {
                AverageMemoryUsedBytes = memoryBytes
            };

            // Assert
            stats.AverageMemoryUsedBytes.Should().Be(memoryBytes);
        }

        [Fact]
        public void PerformanceStatistics_ExecutionTimestamps_ShouldStoreCorrectly()
        {
            // Arrange
            var firstExecution = DateTime.UtcNow.AddHours(-1);
            var lastExecution = DateTime.UtcNow;

            // Act
            var stats = new PerformanceStatistics
            {
                FirstExecution = firstExecution,
                LastExecution = lastExecution
            };

            // Assert
            stats.FirstExecution.Should().Be(firstExecution);
            stats.LastExecution.Should().Be(lastExecution);
            stats.LastExecution.Should().BeAfter(stats.FirstExecution);
        }

        [Fact]
        public void PerformanceStatistics_DefaultValues_ShouldBeCorrect()
        {
            // Arrange & Act
            var stats = new PerformanceStatistics();

            // Assert
            stats.OperationName.Should().Be("");
            stats.TotalExecutions.Should().Be(0);
            stats.SuccessfulExecutions.Should().Be(0);
            stats.FailedExecutions.Should().Be(0);
            stats.SuccessRate.Should().Be(0.0);
            stats.MinDuration.Should().Be(TimeSpan.Zero);
            stats.MaxDuration.Should().Be(TimeSpan.Zero);
            stats.AverageDuration.Should().Be(TimeSpan.Zero);
            stats.MedianDuration.Should().Be(TimeSpan.Zero);
            stats.P95Duration.Should().Be(TimeSpan.Zero);
            stats.P99Duration.Should().Be(TimeSpan.Zero);
            stats.AverageMemoryUsedBytes.Should().Be(0);
            stats.FirstExecution.Should().Be(default(DateTime));
            stats.LastExecution.Should().Be(default(DateTime));
        }

        [Theory]
        [InlineData(100, 50, 50.0, true)]
        [InlineData(100, 100, 100.0, false)]
        [InlineData(100, 0, 0.0, true)]
        [InlineData(0, 0, 0.0, false)]
        public void PerformanceStatistics_SuccessRate_EdgeCases(
            int total, int successful, double expectedRate, bool hasFailures)
        {
            // Arrange
            var stats = new PerformanceStatistics
            {
                TotalExecutions = total,
                SuccessfulExecutions = successful,
                FailedExecutions = total - successful
            };

            // Act
            var successRate = stats.SuccessRate;
            var hasFailed = stats.FailedExecutions > 0;

            // Assert
            successRate.Should().Be(expectedRate);
            hasFailed.Should().Be(hasFailures);
        }

        [Fact]
        public void PerformanceStatistics_CompleteScenario_ShouldWorkCorrectly()
        {
            // Arrange & Act
            var stats = new PerformanceStatistics
            {
                OperationName = "DatabaseQuery",
                TotalExecutions = 1000,
                SuccessfulExecutions = 980,
                FailedExecutions = 20,
                MinDuration = TimeSpan.FromMilliseconds(5),
                MaxDuration = TimeSpan.FromMilliseconds(500),
                AverageDuration = TimeSpan.FromMilliseconds(50),
                MedianDuration = TimeSpan.FromMilliseconds(45),
                P95Duration = TimeSpan.FromMilliseconds(150),
                P99Duration = TimeSpan.FromMilliseconds(300),
                AverageMemoryUsedBytes = 1024 * 100, // 100KB
                FirstExecution = DateTime.UtcNow.AddHours(-24),
                LastExecution = DateTime.UtcNow
            };

            // Assert
            stats.OperationName.Should().Be("DatabaseQuery");
            stats.SuccessRate.Should().Be(98.0);
            stats.TotalExecutions.Should().Be(1000);
            stats.MinDuration.Should().BeLessThan(stats.MedianDuration);
            stats.MedianDuration.Should().BeLessThan(stats.P95Duration);
            stats.P95Duration.Should().BeLessThan(stats.P99Duration);
            stats.P99Duration.Should().BeLessThanOrEqualTo(stats.MaxDuration);
            stats.FirstExecution.Should().BeBefore(stats.LastExecution);
        }

        [Theory]
        [InlineData(10, 50, 100, 200, 300, true)]
        [InlineData(100, 100, 100, 100, 100, true)]
        [InlineData(300, 200, 100, 50, 10, false)]
        public void PerformanceStatistics_PercentileOrdering_ShouldBeValidated(
            int minMs, int medianMs, int avgMs, int p95Ms, int p99Ms, bool isValid)
        {
            // Arrange
            var stats = new PerformanceStatistics
            {
                MinDuration = TimeSpan.FromMilliseconds(minMs),
                MedianDuration = TimeSpan.FromMilliseconds(medianMs),
                AverageDuration = TimeSpan.FromMilliseconds(avgMs),
                P95Duration = TimeSpan.FromMilliseconds(p95Ms),
                P99Duration = TimeSpan.FromMilliseconds(p99Ms),
                MaxDuration = TimeSpan.FromMilliseconds(Math.Max(p99Ms, 400))
            };

            // Act & Assert
            if (isValid)
            {
                stats.MinDuration.Should().BeLessThanOrEqualTo(stats.MedianDuration);
                stats.P95Duration.Should().BeLessThanOrEqualTo(stats.P99Duration);
                stats.P99Duration.Should().BeLessThanOrEqualTo(stats.MaxDuration);
            }
            else
            {
                // Invalid ordering scenario - just verify the values are set
                stats.MinDuration.TotalMilliseconds.Should().Be(minMs);
                stats.P99Duration.TotalMilliseconds.Should().Be(p99Ms);
            }
        }
    }
}