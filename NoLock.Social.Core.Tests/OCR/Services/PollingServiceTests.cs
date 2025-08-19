using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Services;
using Xunit;

namespace NoLock.Social.Core.Tests.OCR.Services
{
    /// <summary>
    /// Unit tests for PollingService with focus on exponential backoff logic.
    /// Uses data-driven testing approach to validate multiple scenarios.
    /// </summary>
    public class PollingServiceTests
    {
        private readonly Mock<ILogger<PollingService<TestResult>>> _loggerMock;
        private readonly PollingService<TestResult> _pollingService;

        public PollingServiceTests()
        {
            _loggerMock = new Mock<ILogger<PollingService<TestResult>>>();
            _pollingService = new PollingService<TestResult>(_loggerMock.Object);
        }

        /// <summary>
        /// Test result class for polling tests.
        /// </summary>
        public class TestResult
        {
            public bool IsComplete { get; set; }
            public int Value { get; set; }
            public string Message { get; set; }
        }

        [Theory]
        [InlineData(5, 1.5, 30, new[] { 5.0, 7.5, 11.25, 16.875, 25.3125, 30.0, 30.0 }, "standard exponential backoff")]
        [InlineData(1, 2.0, 8, new[] { 1.0, 2.0, 4.0, 8.0, 8.0, 8.0 }, "fast doubling backoff")]
        [InlineData(10, 1.0, 10, new[] { 10.0, 10.0, 10.0, 10.0 }, "fixed interval (no backoff)")]
        [InlineData(2, 3.0, 50, new[] { 2.0, 6.0, 18.0, 50.0, 50.0 }, "aggressive tripling backoff")]
        public async Task PollAsync_UsesCorrectExponentialBackoffIntervals(
            int initialInterval,
            double multiplier,
            int maxInterval,
            double[] expectedIntervals,
            string scenario)
        {
            // Arrange
            var configuration = new PollingConfiguration
            {
                InitialIntervalSeconds = initialInterval,
                BackoffMultiplier = multiplier,
                MaxIntervalSeconds = maxInterval,
                MaxPollingDurationSeconds = 600, // 10 minutes to avoid timeout
                UseExponentialBackoff = multiplier > 1.0
            };

            var attemptCount = 0;
            var recordedIntervals = new System.Collections.Generic.List<double>();
            var lastCallTime = DateTime.UtcNow;

            // Operation that records intervals and completes after expected attempts
            async Task<TestResult> operation(CancellationToken ct)
            {
                var now = DateTime.UtcNow;
                if (attemptCount > 0)
                {
                    var interval = (now - lastCallTime).TotalSeconds;
                    recordedIntervals.Add(interval);
                }
                lastCallTime = now;
                attemptCount++;

                await Task.Delay(10, ct); // Small delay to simulate work
                
                return new TestResult
                {
                    IsComplete = attemptCount > expectedIntervals.Length,
                    Value = attemptCount,
                    Message = $"Attempt {attemptCount}"
                };
            }

            // Act
            var result = await _pollingService.PollAsync(
                operation,
                r => r.IsComplete,
                configuration,
                CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsComplete, $"Operation should complete for scenario: {scenario}");
            Assert.Equal(expectedIntervals.Length + 1, attemptCount);

            // Verify intervals (with tolerance for timing variations)
            for (int i = 0; i < Math.Min(expectedIntervals.Length, recordedIntervals.Count); i++)
            {
                var expected = expectedIntervals[i];
                var actual = recordedIntervals[i];
                var tolerance = 0.5; // 500ms tolerance for timing variations
                
                Assert.True(
                    Math.Abs(actual - expected) <= tolerance,
                    $"Interval {i} for '{scenario}': expected ~{expected}s, got {actual:F2}s");
            }
        }

        [Theory]
        [InlineData(1, 100, true, "immediate completion")]
        [InlineData(3, 100, true, "completion after 3 attempts")]
        [InlineData(5, 100, true, "completion after 5 attempts")]
        public async Task PollAsync_CompletesSuccessfullyWithinTimeout(
            int attemptsToComplete,
            int maxDurationSeconds,
            bool shouldSucceed,
            string scenario)
        {
            // Arrange
            var configuration = new PollingConfiguration
            {
                InitialIntervalSeconds = 1,
                MaxIntervalSeconds = 2,
                BackoffMultiplier = 2.0,
                MaxPollingDurationSeconds = maxDurationSeconds,
                UseExponentialBackoff = true
            };

            var attemptCount = 0;
            
            async Task<TestResult> operation(CancellationToken ct)
            {
                attemptCount++;
                await Task.Delay(10, ct);
                return new TestResult
                {
                    IsComplete = attemptCount >= attemptsToComplete,
                    Value = attemptCount,
                    Message = $"Attempt {attemptCount} of {attemptsToComplete}"
                };
            }

            // Act & Assert
            if (shouldSucceed)
            {
                var result = await _pollingService.PollAsync(
                    operation,
                    r => r.IsComplete,
                    configuration,
                    CancellationToken.None);

                Assert.NotNull(result);
                Assert.True(result.IsComplete);
                Assert.Equal(attemptsToComplete, result.Value);
            }
        }

        [Theory]
        [InlineData(2, 3, false, "timeout with short duration")]
        [InlineData(1, 100, true, "no timeout with long duration")]
        public async Task PollAsync_RespectsMaxPollingDuration(
            int pollingDurationSeconds,
            int attemptsToComplete,
            bool shouldSucceed,
            string scenario)
        {
            // Arrange
            var configuration = new PollingConfiguration
            {
                InitialIntervalSeconds = 1,
                MaxIntervalSeconds = 1,
                BackoffMultiplier = 1.0,
                MaxPollingDurationSeconds = pollingDurationSeconds,
                UseExponentialBackoff = false
            };

            var attemptCount = 0;
            
            async Task<TestResult> operation(CancellationToken ct)
            {
                attemptCount++;
                await Task.Delay(100, ct); // Delay to consume time
                return new TestResult
                {
                    IsComplete = attemptCount >= attemptsToComplete,
                    Value = attemptCount
                };
            }

            // Act & Assert
            if (shouldSucceed)
            {
                var result = await _pollingService.PollAsync(
                    operation,
                    r => r.IsComplete,
                    configuration,
                    CancellationToken.None);
                
                Assert.NotNull(result);
            }
            else
            {
                await Assert.ThrowsAsync<TimeoutException>(async () =>
                    await _pollingService.PollAsync(
                        operation,
                        r => r.IsComplete,
                        configuration,
                        CancellationToken.None));
            }
        }

        [Theory]
        [InlineData(3, true, "respects max attempts limit")]
        [InlineData(null, false, "no limit when max attempts is null")]
        public async Task PollAsync_RespectsMaxAttempts(
            int? maxAttempts,
            bool shouldEnforceLimit,
            string scenario)
        {
            // Arrange
            var configuration = new PollingConfiguration
            {
                InitialIntervalSeconds = 1,
                MaxIntervalSeconds = 1,
                BackoffMultiplier = 1.0,
                MaxPollingDurationSeconds = 300,
                UseExponentialBackoff = false,
                MaxAttempts = maxAttempts
            };

            var attemptCount = 0;
            var targetAttempts = maxAttempts ?? 5;
            
            async Task<TestResult> operation(CancellationToken ct)
            {
                attemptCount++;
                await Task.Delay(10, ct);
                return new TestResult
                {
                    IsComplete = false, // Never complete naturally
                    Value = attemptCount
                };
            }

            // Act & Assert
            if (shouldEnforceLimit && maxAttempts.HasValue)
            {
                var exception = await Assert.ThrowsAsync<TimeoutException>(async () =>
                    await _pollingService.PollAsync(
                        operation,
                        r => r.IsComplete,
                        configuration,
                        CancellationToken.None));
                
                Assert.Contains("exceeded maximum attempts", exception.Message);
                Assert.Equal(maxAttempts.Value + 1, attemptCount); // One extra attempt before checking
            }
        }

        [Fact]
        public async Task PollAsync_CallsProgressCallbackOnEachAttempt()
        {
            // Arrange
            var configuration = PollingConfiguration.Fast;
            var progressUpdates = new System.Collections.Generic.List<TestResult>();
            var attemptCount = 0;
            
            async Task<TestResult> operation(CancellationToken ct)
            {
                attemptCount++;
                await Task.Delay(10, ct);
                return new TestResult
                {
                    IsComplete = attemptCount >= 3,
                    Value = attemptCount,
                    Message = $"Progress {attemptCount}"
                };
            }

            void progressCallback(TestResult result)
            {
                progressUpdates.Add(result);
            }

            // Act
            var result = await _pollingService.PollWithProgressAsync(
                operation,
                r => r.IsComplete,
                progressCallback,
                configuration,
                CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, progressUpdates.Count);
            Assert.All(progressUpdates, (update, index) =>
            {
                Assert.Equal(index + 1, update.Value);
                Assert.Equal($"Progress {index + 1}", update.Message);
            });
        }

        [Fact]
        public async Task PollAsync_ProperlyCancelsOnCancellationToken()
        {
            // Arrange
            var configuration = PollingConfiguration.Slow;
            var cts = new CancellationTokenSource();
            var attemptCount = 0;
            
            async Task<TestResult> operation(CancellationToken ct)
            {
                attemptCount++;
                if (attemptCount == 2)
                {
                    cts.Cancel(); // Cancel after second attempt
                }
                await Task.Delay(10, ct);
                return new TestResult { IsComplete = false };
            }

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await _pollingService.PollAsync(
                    operation,
                    r => r.IsComplete,
                    configuration,
                    cts.Token));
            
            Assert.True(attemptCount >= 2);
        }

        [Theory]
        [InlineData(0, typeof(ArgumentException), "initial interval must be positive")]
        [InlineData(-5, typeof(ArgumentException), "negative initial interval")]
        public void PollingConfiguration_ValidateThrowsForInvalidConfig(
            int initialInterval,
            Type expectedExceptionType,
            string scenario)
        {
            // Arrange
            var configuration = new PollingConfiguration
            {
                InitialIntervalSeconds = initialInterval,
                MaxIntervalSeconds = 30,
                BackoffMultiplier = 2.0,
                MaxPollingDurationSeconds = 300
            };

            // Act & Assert
            var exception = Assert.Throws(expectedExceptionType, () => configuration.Validate());
            Assert.NotNull(exception);
        }

        [Theory]
        [InlineData(5, 30, 2.0, 300, true, "valid OCR default configuration")]
        [InlineData(1, 8, 2.0, 60, true, "valid fast configuration")]
        [InlineData(30, 30, 1.0, 1800, false, "valid slow configuration")]
        public void PollingConfiguration_ValidateSucceedsForValidConfig(
            int initialInterval,
            int maxInterval,
            double multiplier,
            int maxDuration,
            bool useBackoff,
            string scenario)
        {
            // Arrange
            var configuration = new PollingConfiguration
            {
                InitialIntervalSeconds = initialInterval,
                MaxIntervalSeconds = maxInterval,
                BackoffMultiplier = multiplier,
                MaxPollingDurationSeconds = maxDuration,
                UseExponentialBackoff = useBackoff
            };

            // Act & Assert (should not throw)
            configuration.Validate();
            Assert.True(true, $"Configuration validation passed for: {scenario}");
        }
    }
}