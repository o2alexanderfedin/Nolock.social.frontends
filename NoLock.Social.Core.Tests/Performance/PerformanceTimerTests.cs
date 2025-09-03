using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NoLock.Social.Core.Performance;
using Xunit;

namespace NoLock.Social.Core.Tests.Performance
{
    public class PerformanceTimerTests
    {
        [Fact]
        public void PerformanceTimer_ShouldRecordMetricsOnDispose()
        {
            // Arrange
            PerformanceMetrics? capturedMetrics = null;
            var operationName = "TestOperation";

            // Act
            using (var timer = new PerformanceTimer(operationName, metrics => capturedMetrics = metrics))
            {
                Thread.Sleep(50); // Simulate work
            }

            // Assert
            capturedMetrics.Should().NotBeNull();
            capturedMetrics!.OperationName.Should().Be(operationName);
            capturedMetrics.Duration.TotalMilliseconds.Should().BeGreaterThan(40);
            capturedMetrics.Success.Should().BeTrue();
            capturedMetrics.ErrorMessage.Should().BeNull();
        }

        [Theory]
        [InlineData("metric1", 123, "Custom metric integer")]
        [InlineData("metric2", "test value", "Custom metric string")]
        [InlineData("metric3", 45.67, "Custom metric double")]
        [InlineData("metric4", true, "Custom metric boolean")]
        public void AddMetric_ShouldStoreCustomMetrics(string key, object value, string description)
        {
            // Arrange
            PerformanceMetrics? capturedMetrics = null;
            var timer = new PerformanceTimer($"Test_{description}", metrics => capturedMetrics = metrics);

            // Act
            timer.AddMetric(key, value);
            timer.Dispose();

            // Assert
            capturedMetrics.Should().NotBeNull();
            capturedMetrics!.CustomMetrics.Should().ContainKey(key);
            capturedMetrics.CustomMetrics[key].Should().Be(value);
        }

        [Theory]
        [InlineData("Database connection failed")]
        [InlineData("Timeout occurred")]
        [InlineData("Invalid input data")]
        [InlineData("Network error")]
        [InlineData("Authorization failed")]
        public void MarkAsFailure_ShouldSetFailureState(string errorMessage)
        {
            // Arrange
            PerformanceMetrics? capturedMetrics = null;
            var timer = new PerformanceTimer("FailureTest", metrics => capturedMetrics = metrics);

            // Act
            timer.MarkAsFailure(errorMessage);
            timer.Dispose();

            // Assert
            capturedMetrics.Should().NotBeNull();
            capturedMetrics!.Success.Should().BeFalse();
            capturedMetrics.ErrorMessage.Should().Be(errorMessage);
        }

        [Fact]
        public void MultipleDispose_ShouldOnlyRecordOnce()
        {
            // Arrange
            var recordCount = 0;
            var timer = new PerformanceTimer("MultiDispose", _ => recordCount++);

            // Act
            timer.Dispose();
            timer.Dispose();
            timer.Dispose();

            // Assert
            recordCount.Should().Be(1);
        }

        [Fact]
        public void PerformanceTimer_ShouldCaptureMemoryUsage()
        {
            // Arrange
            PerformanceMetrics? capturedMetrics = null;
            
            // Act
            using (var timer = new PerformanceTimer("MemoryTest", metrics => capturedMetrics = metrics))
            {
                // Allocate some memory
                var data = new byte[1024 * 1024]; // 1MB
                GC.Collect(0, GCCollectionMode.Forced);
            }

            // Assert
            capturedMetrics.Should().NotBeNull();
            capturedMetrics!.MemoryUsedBytes.Should().BeGreaterThanOrEqualTo(0);
        }

        [Fact]
        public void PerformanceTimer_ShouldRecordAccurateTimestamps()
        {
            // Arrange
            PerformanceMetrics? capturedMetrics = null;
            var startTime = DateTime.UtcNow;

            // Act
            using (var timer = new PerformanceTimer("TimestampTest", metrics => capturedMetrics = metrics))
            {
                Thread.Sleep(100);
            }
            var endTime = DateTime.UtcNow;

            // Assert
            capturedMetrics.Should().NotBeNull();
            capturedMetrics!.StartTime.Should().BeOnOrAfter(startTime.AddSeconds(-1));
            capturedMetrics.StartTime.Should().BeOnOrBefore(endTime);
            capturedMetrics.EndTime.Should().BeOnOrAfter(startTime);
            capturedMetrics.EndTime.Should().BeOnOrBefore(endTime.AddSeconds(1));
            capturedMetrics.EndTime.Should().BeAfter(capturedMetrics.StartTime);
        }

        [Fact]
        public async Task PerformanceTimer_ShouldWorkWithAsyncOperations()
        {
            // Arrange
            PerformanceMetrics? capturedMetrics = null;
            
            // Act
            using (var timer = new PerformanceTimer("AsyncTest", metrics => capturedMetrics = metrics))
            {
                await Task.Delay(50);
                timer.AddMetric("asyncCompleted", true);
            }

            // Assert
            capturedMetrics.Should().NotBeNull();
            capturedMetrics!.Duration.TotalMilliseconds.Should().BeGreaterThan(40);
            capturedMetrics.CustomMetrics.Should().ContainKey("asyncCompleted");
            capturedMetrics.CustomMetrics["asyncCompleted"].Should().Be(true);
        }

        [Fact]
        public void PerformanceTimer_WithMultipleCustomMetrics_ShouldStoreAll()
        {
            // Arrange
            PerformanceMetrics? capturedMetrics = null;
            var timer = new PerformanceTimer("MultiMetricTest", metrics => capturedMetrics = metrics);
            var metricsToAdd = new Dictionary<string, object>
            {
                ["itemsProcessed"] = 100,
                ["processingRate"] = 50.5,
                ["status"] = "completed",
                ["hasErrors"] = false,
                ["timestamp"] = DateTime.UtcNow
            };

            // Act
            foreach (var kvp in metricsToAdd)
            {
                timer.AddMetric(kvp.Key, kvp.Value);
            }
            timer.Dispose();

            // Assert
            capturedMetrics.Should().NotBeNull();
            capturedMetrics!.CustomMetrics.Should().HaveCount(metricsToAdd.Count);
            foreach (var kvp in metricsToAdd)
            {
                capturedMetrics.CustomMetrics.Should().ContainKey(kvp.Key);
                capturedMetrics.CustomMetrics[kvp.Key].Should().Be(kvp.Value);
            }
        }

        [Fact]
        public void PerformanceTimer_FailureThenSuccess_ShouldRecordFailure()
        {
            // Arrange
            PerformanceMetrics? capturedMetrics = null;
            var timer = new PerformanceTimer("FailureOverrideTest", metrics => capturedMetrics = metrics);

            // Act
            timer.MarkAsFailure("Initial failure");
            // Try to override (this shouldn't change the failure state in a real scenario)
            timer.AddMetric("attemptedRecovery", true);
            timer.Dispose();

            // Assert
            capturedMetrics.Should().NotBeNull();
            capturedMetrics!.Success.Should().BeFalse();
            capturedMetrics.ErrorMessage.Should().Be("Initial failure");
            capturedMetrics.CustomMetrics.Should().ContainKey("attemptedRecovery");
        }
    }
}