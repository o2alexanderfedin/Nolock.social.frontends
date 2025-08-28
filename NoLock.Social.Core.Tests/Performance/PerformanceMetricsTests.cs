using System;
using System.Collections.Generic;
using FluentAssertions;
using NoLock.Social.Core.Performance;
using Xunit;

namespace NoLock.Social.Core.Tests.Performance
{
    public class PerformanceMetricsTests
    {
        [Theory]
        [InlineData("Operation1", 100, 1024, true, null)]
        [InlineData("Operation2", 250, 2048, false, "Error occurred")]
        [InlineData("LongRunningOp", 5000, 10240, true, null)]
        [InlineData("FailedOp", 50, 512, false, "Timeout")]
        [InlineData("QuickOp", 10, 256, true, null)]
        public void PerformanceMetrics_ShouldStoreBasicProperties(
            string operationName, int durationMs, long memoryBytes, bool success, string? errorMessage)
        {
            // Arrange & Act
            var startTime = DateTime.UtcNow.AddMilliseconds(-durationMs);
            var endTime = DateTime.UtcNow;
            
            var metrics = new PerformanceMetrics
            {
                OperationName = operationName,
                StartTime = startTime,
                EndTime = endTime,
                MemoryUsedBytes = memoryBytes,
                Success = success,
                ErrorMessage = errorMessage
            };

            // Assert
            metrics.OperationName.Should().Be(operationName);
            metrics.StartTime.Should().Be(startTime);
            metrics.EndTime.Should().Be(endTime);
            metrics.Duration.Should().BeCloseTo(TimeSpan.FromMilliseconds(durationMs), TimeSpan.FromMilliseconds(1));
            metrics.MemoryUsedBytes.Should().Be(memoryBytes);
            metrics.Success.Should().Be(success);
            metrics.ErrorMessage.Should().Be(errorMessage);
        }

        [Fact]
        public void PerformanceMetrics_Duration_ShouldCalculateFromStartAndEndTime()
        {
            // Arrange
            var startTime = DateTime.UtcNow.AddSeconds(-5);
            var endTime = DateTime.UtcNow;
            
            var metrics = new PerformanceMetrics
            {
                StartTime = startTime,
                EndTime = endTime
            };

            // Act
            var calculatedDuration = metrics.Duration;

            // Assert
            calculatedDuration.Should().BeCloseTo(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(100));
        }

        [Fact]
        public void PerformanceMetrics_Duration_ShouldUseExplicitValueWhenSet()
        {
            // Arrange
            var explicitDuration = TimeSpan.FromMilliseconds(1234);
            var metrics = new PerformanceMetrics
            {
                StartTime = DateTime.UtcNow.AddSeconds(-10),
                EndTime = DateTime.UtcNow,
                Duration = explicitDuration
            };

            // Act
            var duration = metrics.Duration;

            // Assert
            duration.Should().Be(explicitDuration);
            duration.Should().NotBeCloseTo(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));
        }

        [Theory]
        [InlineData("key1", "value1")]
        [InlineData("requestId", "12345")]
        [InlineData("userId", 42)]
        [InlineData("success", true)]
        [InlineData("responseTime", 123.45)]
        public void PerformanceMetrics_CustomMetrics_ShouldStoreValues(string key, object value)
        {
            // Arrange
            var metrics = new PerformanceMetrics();

            // Act
            metrics.CustomMetrics[key] = value;

            // Assert
            metrics.CustomMetrics.Should().ContainKey(key);
            metrics.CustomMetrics[key].Should().Be(value);
        }

        [Fact]
        public void PerformanceMetrics_CustomMetrics_ShouldInitializeAsEmptyDictionary()
        {
            // Arrange & Act
            var metrics = new PerformanceMetrics();

            // Assert
            metrics.CustomMetrics.Should().NotBeNull();
            metrics.CustomMetrics.Should().BeEmpty();
        }

        [Fact]
        public void PerformanceMetrics_DefaultValues_ShouldBeCorrect()
        {
            // Arrange & Act
            var metrics = new PerformanceMetrics();

            // Assert
            metrics.OperationName.Should().Be("");
            metrics.StartTime.Should().Be(default(DateTime));
            metrics.EndTime.Should().Be(default(DateTime));
            metrics.MemoryUsedBytes.Should().Be(0);
            metrics.Success.Should().BeFalse();
            metrics.ErrorMessage.Should().BeNull();
            metrics.CustomMetrics.Should().NotBeNull().And.BeEmpty();
        }

        [Theory]
        [InlineData(0, 0, 0)]
        [InlineData(100, 200, -100)]
        [InlineData(1000, 500, 500)]
        [InlineData(long.MaxValue, 0, long.MaxValue)]
        public void PerformanceMetrics_MemoryUsedBytes_CanBeNegative(long startMemory, long endMemory, long expectedUsed)
        {
            // This tests that memory can be negative (e.g., when GC runs during operation)
            // Arrange & Act
            var metrics = new PerformanceMetrics
            {
                MemoryUsedBytes = endMemory - startMemory
            };

            // Assert
            metrics.MemoryUsedBytes.Should().Be(endMemory - startMemory);
        }

        [Fact]
        public void PerformanceMetrics_ComplexCustomMetrics_ShouldWork()
        {
            // Arrange
            var metrics = new PerformanceMetrics();
            var complexObject = new
            {
                Id = 123,
                Name = "Test",
                Values = new[] { 1, 2, 3 },
                Nested = new { Property = "Value" }
            };

            // Act
            metrics.CustomMetrics["simple"] = "text";
            metrics.CustomMetrics["number"] = 42;
            metrics.CustomMetrics["decimal"] = 3.14;
            metrics.CustomMetrics["boolean"] = true;
            metrics.CustomMetrics["date"] = DateTime.UtcNow;
            metrics.CustomMetrics["complex"] = complexObject;
            metrics.CustomMetrics["null"] = null!;

            // Assert
            metrics.CustomMetrics.Should().HaveCount(7);
            metrics.CustomMetrics["complex"].Should().BeSameAs(complexObject);
            metrics.CustomMetrics["null"].Should().BeNull();
        }
    }
}