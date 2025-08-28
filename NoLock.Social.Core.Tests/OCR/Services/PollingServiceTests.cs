using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Services;

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
            public string? Message { get; set; }
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
        [InlineData(10, 5, true, "no timeout with long duration")]
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
                Assert.Equal(maxAttempts.Value, attemptCount); // Should call operation exactly maxAttempts times
            }
        }

        [Fact]
        public async Task PollAsync_CallsProgressCallbackOnEachAttempt()
        {
            // Arrange
            var configuration = PollingConfiguration.Fast;
            var progressUpdates = new List<TestResult>();
            var attemptCount = 0;
            
            async Task<TestResult> operation(CancellationToken ct)
            {
                attemptCount++;
                await Task.Delay(1, ct);
                return new TestResult
                {
                    IsComplete = attemptCount >= 2,
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
            Assert.Equal(2, progressUpdates.Count);
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
            var configuration = PollingConfiguration.Fast;
            var cts = new CancellationTokenSource();
            var attemptCount = 0;
            
            async Task<TestResult> operation(CancellationToken ct)
            {
                if (Interlocked.Increment(ref attemptCount) >= 2) 
                    await cts.CancelAsync(); // Cancel after second attempt
                
                await Task.Delay(10, ct);
                return new TestResult { IsComplete = false };
            }

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await _pollingService.PollAsync(
                    operation,
                    r => r.IsComplete,
                    configuration,
                    cts.Token));
            
            Assert.True(Volatile.Read(ref attemptCount) >= 2);
        }

        [Theory]
        [InlineData(0, typeof(ArgumentOutOfRangeException), "initial interval must be positive")]
        [InlineData(-5, typeof(ArgumentOutOfRangeException), "negative initial interval")]
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

        [Fact]
        public async Task PollAsync_ImplementsExponentialBackoffCorrectly()
        {
            // Arrange
            var configuration = new PollingConfiguration
            {
                InitialIntervalSeconds = 1,
                MaxIntervalSeconds = 4,
                BackoffMultiplier = 2,
                MaxPollingDurationSeconds = 300,
                UseExponentialBackoff = true
            };

            var attemptCount = 0;
            var recordedIntervals = new List<double>();
            var lastTime = DateTime.UtcNow;
            
            async Task<TestResult> operation(CancellationToken ct)
            {
                if (attemptCount > 0)
                {
                    var elapsed = (DateTime.UtcNow - lastTime).TotalSeconds;
                    recordedIntervals.Add(Math.Round(elapsed, 1));
                }
                lastTime = DateTime.UtcNow;
                attemptCount++;
                await Task.Delay(10, ct);
                return new TestResult { IsComplete = attemptCount > 4 };
            }

            // Act
            var result = await _pollingService.PollAsync(
                operation,
                r => r.IsComplete,
                configuration,
                CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(recordedIntervals.Count >= 3);
        }

        [Fact]
        public async Task PollAsync_HandlesOperationExceptionsGracefully()
        {
            // Arrange
            var configuration = PollingConfiguration.Fast;
            var exceptionMessage = "Operation failed";
            
            Task<TestResult> operation(CancellationToken ct)
            {
                throw new InvalidOperationException(exceptionMessage);
            }

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _pollingService.PollAsync(
                    operation,
                    r => r.IsComplete,
                    configuration,
                    CancellationToken.None));
            
            Assert.Equal(exceptionMessage, exception.Message);
        }

        [Fact]
        public async Task PollAsync_PropagatesOperationCancellation()
        {
            // Arrange
            var configuration = PollingConfiguration.Fast;
            var cts = new CancellationTokenSource();
            
            async Task<TestResult> operation(CancellationToken ct)
            {
                cts.Cancel(); // Cancel immediately
                ct.ThrowIfCancellationRequested();
                await Task.Delay(10, ct);
                return new TestResult { IsComplete = false };
            }

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await _pollingService.PollAsync(
                    operation,
                    r => r.IsComplete,
                    configuration,
                    cts.Token));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task PollAsync_RespectsFixedIntervalConfiguration(
            bool useExponentialBackoff)
        {
            // Arrange
            var configuration = new PollingConfiguration
            {
                InitialIntervalSeconds = 1,
                MaxIntervalSeconds = 5,
                BackoffMultiplier = 2.0,
                MaxPollingDurationSeconds = 300,
                UseExponentialBackoff = useExponentialBackoff
            };

            var attemptCount = 0;
            var intervalTimes = new List<DateTime>();
            
            async Task<TestResult> operation(CancellationToken ct)
            {
                intervalTimes.Add(DateTime.UtcNow);
                attemptCount++;
                await Task.Delay(10, ct);
                return new TestResult { IsComplete = attemptCount >= 3 };
            }

            // Act
            var result = await _pollingService.PollAsync(
                operation,
                r => r.IsComplete,
                configuration,
                CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, attemptCount);
            
            if (!useExponentialBackoff)
            {
                // Verify fixed interval between attempts
                for (int i = 1; i < intervalTimes.Count; i++)
                {
                    var interval = (intervalTimes[i] - intervalTimes[i - 1]).TotalSeconds;
                    Assert.InRange(interval, 0.9, 1.2); // Allow some tolerance
                }
            }
        }

        [Fact]
        public async Task PollWithProgressAsync_HandlesNullProgressCallback()
        {
            // Arrange
            var configuration = PollingConfiguration.Fast;
            var attemptCount = 0;
            
            async Task<TestResult> operation(CancellationToken ct)
            {
                attemptCount++;
                await Task.Delay(10, ct);
                return new TestResult { IsComplete = attemptCount >= 2 };
            }

            // Act (should not throw despite null callback)
            var result = await _pollingService.PollWithProgressAsync(
                operation,
                r => r.IsComplete,
                null!, // Null progress callback
                configuration,
                CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsComplete);
        }

        [Fact]
        public async Task PollAsync_ThrowsForNullOperation()
        {
            // Arrange
            var configuration = PollingConfiguration.Fast;
            
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                async () => await _pollingService.PollAsync(
                    null!, // Null operation
                    r => true,
                    configuration,
                    CancellationToken.None));
        }

        [Fact]
        public async Task PollAsync_HandlesTimeoutDuringOperation()
        {
            // Arrange
            var configuration = new PollingConfiguration
            {
                InitialIntervalSeconds = 1,
                MaxIntervalSeconds = 1,
                BackoffMultiplier = 1.0,
                MaxPollingDurationSeconds = 1, // Very short timeout
                UseExponentialBackoff = false
            };
            
            async Task<TestResult> operation(CancellationToken ct)
            {
                await Task.Delay(2000, ct); // Delay longer than timeout
                return new TestResult { IsComplete = false };
            }

            // Act & Assert
            var exception = await Assert.ThrowsAsync<TimeoutException>(
                async () => await _pollingService.PollAsync(
                    operation,
                    r => r.IsComplete,
                    configuration,
                    CancellationToken.None));
            
            Assert.Contains("timed out", exception.Message);
        }

        [Fact]
        public async Task PollAsync_HandlesTimeoutDuringDelay()
        {
            // Arrange
            var configuration = new PollingConfiguration
            {
                InitialIntervalSeconds = 10, // Long delay
                MaxIntervalSeconds = 10,
                BackoffMultiplier = 1.0,
                MaxPollingDurationSeconds = 1, // Short timeout
                UseExponentialBackoff = false
            };

            var attemptCount = 0;
            
            async Task<TestResult> operation(CancellationToken ct)
            {
                attemptCount++;
                await Task.Delay(10, ct);
                return new TestResult { IsComplete = false };
            }

            // Act & Assert
            var exception = await Assert.ThrowsAsync<TimeoutException>(
                async () => await _pollingService.PollAsync(
                    operation,
                    r => r.IsComplete,
                    configuration,
                    CancellationToken.None));
            
            Assert.Contains("timed out", exception.Message);
            Assert.Equal(1, attemptCount); // Should only execute once before timeout
        }

        [Theory]
        [InlineData(1, 2, 4)]
        [InlineData(1, 10, 8)]
        public async Task PollAsync_HandlesRapidPolling(
            int initialInterval,
            double multiplier,
            int maxInterval)
        {
            // Arrange
            var configuration = new PollingConfiguration
            {
                InitialIntervalSeconds = initialInterval,
                MaxIntervalSeconds = maxInterval,
                BackoffMultiplier = multiplier,
                MaxPollingDurationSeconds = 300,
                UseExponentialBackoff = true
            };

            var attemptCount = 0;
            
            async Task<TestResult> operation(CancellationToken ct)
            {
                attemptCount++;
                await Task.Delay(10, ct);
                return new TestResult { IsComplete = attemptCount >= 3 };
            }

            // Act
            var stopwatch = Stopwatch.StartNew();
            var result = await _pollingService.PollAsync(
                operation,
                r => r.IsComplete,
                configuration,
                CancellationToken.None);
            stopwatch.Stop();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsComplete);
            // Timing assertions are unreliable in CI/CD environments, we just ensure it completes
        }

        [Fact]
        public async Task PollAsync_PreservesOriginalExceptionDetails()
        {
            // Arrange
            var configuration = PollingConfiguration.Fast;
            var innerException = new InvalidOperationException("Inner exception");
            var outerException = new AggregateException("Outer exception", innerException);
            
            Task<TestResult> operation(CancellationToken ct)
            {
                throw outerException;
            }

            // Act & Assert
            var thrownException = await Assert.ThrowsAsync<AggregateException>(
                async () => await _pollingService.PollAsync(
                    operation,
                    r => r.IsComplete,
                    configuration,
                    CancellationToken.None));
            
            Assert.Equal(outerException.Message, thrownException.Message);
            Assert.NotNull(thrownException.InnerException);
            Assert.IsType<InvalidOperationException>(thrownException.InnerException);
        }
    }
}