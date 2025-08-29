using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NoLock.Social.Core.Cryptography.Extensions;
using NoLock.Social.Core.Cryptography.Interfaces;
using Xunit;

namespace NoLock.Social.Core.Tests.Cryptography.Extensions
{
    public class ReactiveExtensionsTests
    {
        private readonly Mock<IReactiveSessionStateService> _sessionServiceMock;
        private readonly Subject<SessionState> _stateSubject;

        public ReactiveExtensionsTests()
        {
            _sessionServiceMock = new Mock<IReactiveSessionStateService>();
            _stateSubject = new Subject<SessionState>();
            _sessionServiceMock.Setup(x => x.StateStream).Returns(_stateSubject.AsObservable());
        }

        [Theory]
        [InlineData(SessionState.Unlocked, true, "Should emit when unlocked")]
        [InlineData(SessionState.Locked, false, "Should not emit when locked")]
        [InlineData(SessionState.Expired, false, "Should not emit when expired")]
        [InlineData(SessionState.Unlocking, false, "Should not emit when unlocking")]
        public async Task WhenUnlocked_ShouldEmitOnlyWhenUnlocked(SessionState state, bool shouldEmit, string scenario)
        {
            // Arrange
            var result = new List<int>();
            var tcs = new TaskCompletionSource<bool>();
            
            // Create a subject that we control for the source
            var sourceSubject = new Subject<int>();
            var whenUnlocked = sourceSubject.WhenUnlocked(_sessionServiceMock.Object);
            
            // Subscribe and set up completion
            whenUnlocked.Subscribe(
                value => { result.Add(value); tcs.TrySetResult(true); },
                error => tcs.TrySetException(error),
                () => tcs.TrySetResult(false));
            
            // Act - Set the state first
            _stateSubject.OnNext(state);
            await Task.Delay(10); // Small delay to ensure state propagates
            
            // Then emit value from source
            sourceSubject.OnNext(42);
            sourceSubject.OnCompleted();
            
            // Wait for completion or timeout
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(100)) == tcs.Task;

            // Assert
            if (shouldEmit)
            {
                result.Should().ContainSingle().Which.Should().Be(42, scenario);
            }
            else
            {
                result.Should().BeEmpty(scenario);
            }
        }

        [Theory]
        [InlineData(1, 100, 1, "Single retry")]
        [InlineData(3, 100, 3, "Multiple retries")]
        [InlineData(5, 50, 5, "Many retries with short delay")]
        public async Task RetryWithBackoff_ShouldRetryWithExponentialDelay(int retryCount, int initialDelayMs, int expectedAttempts, string scenario)
        {
            // Arrange
            var attemptCount = 0;
            var source = Observable.Defer(() =>
            {
                attemptCount++;
                if (attemptCount < expectedAttempts)
                    return Observable.Throw<int>(new Exception($"Attempt {attemptCount}"));
                return Observable.Return(42);
            });

            // Act
            var result = await source
                .RetryWithBackoff(retryCount, TimeSpan.FromMilliseconds(initialDelayMs))
                .FirstOrDefaultAsync();

            // Assert
            result.Should().Be(42, scenario);
            attemptCount.Should().Be(expectedAttempts, scenario);
        }

        [Fact]
        public async Task RetryWithBackoff_ShouldFailAfterMaxRetries()
        {
            // Arrange
            var source = Observable.Throw<int>(new Exception("Always fails"));

            // Act
            var act = async () => await source
                .RetryWithBackoff(3, TimeSpan.FromMilliseconds(10))
                .FirstOrDefaultAsync();

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("Always fails");
        }

        [Theory]
        [InlineData(100, 50, 3, "Fast progress updates")]
        [InlineData(500, 200, 5, "Medium progress updates")]
        [InlineData(1000, 300, 10, "Slow progress updates")]
        public async Task ThrottleProgress_ShouldSampleProgressUpdates(int totalMs, int throttleMs, int updates, string scenario)
        {
            // Arrange
            var progressSubject = new Subject<KeyDerivationProgressEventArgs>();
            var receivedProgress = new List<KeyDerivationProgressEventArgs>();
            
            progressSubject
                .ThrottleProgress(TimeSpan.FromMilliseconds(throttleMs))
                .Subscribe(receivedProgress.Add);

            // Act
            for (int i = 0; i < updates; i++)
            {
                var progress = new KeyDerivationProgressEventArgs
                {
                    PercentComplete = (i + 1) * 100 / updates,
                    Message = $"Processing {i + 1}/{updates}",
                    ElapsedTime = TimeSpan.FromMilliseconds(i * totalMs / updates)
                };
                progressSubject.OnNext(progress);
                await Task.Delay(totalMs / updates);
            }

            // Add completion
            progressSubject.OnNext(new KeyDerivationProgressEventArgs
            {
                PercentComplete = 100,
                Message = "Completed",
                ElapsedTime = TimeSpan.FromMilliseconds(totalMs)
            });

            await Task.Delay(throttleMs + 50); // Wait for throttle to complete

            // Assert
            receivedProgress.Should().NotBeEmpty(scenario);
            receivedProgress.Last().PercentComplete.Should().Be(100, "Completion should always be emitted");
        }

        [Fact]
        public async Task CreateProgressObservable_ShouldCreateWorkingProgressReporter()
        {
            // Arrange & Act
            var (progress, observable) = ReactiveExtensions.CreateProgressObservable<int>();
            var results = new List<int>();
            var tcs = new TaskCompletionSource<bool>();
            var expectedCount = 4;
            
            observable.Subscribe(value =>
            {
                results.Add(value);
                if (results.Count >= expectedCount)
                {
                    tcs.TrySetResult(true);
                }
            });

            // Report progress - use Task.Run to ensure async execution
            await Task.Run(async () =>
            {
                progress.Report(25);
                await Task.Yield(); // Allow processing
                progress.Report(50);
                await Task.Yield();
                progress.Report(75);
                await Task.Yield();
                progress.Report(100);
            });

            // Wait for all reports to be processed
            await Task.WhenAny(tcs.Task, Task.Delay(500));

            // Assert
            results.Should().BeEquivalentTo(new[] { 25, 50, 75, 100 });
        }

        [Theory]
        [InlineData(SessionState.Locked, SessionState.Unlocked, 3, "Buffer until unlocked")]
        [InlineData(SessionState.Expired, SessionState.Unlocked, 5, "Buffer expired to unlocked")]
        [InlineData(SessionState.Locking, SessionState.Unlocked, 2, "Buffer locking to unlocked")]
        public async Task BufferUntilUnlocked_ShouldBufferAndReplay(SessionState initial, SessionState final, int itemCount, string scenario)
        {
            // Arrange
            var source = new Subject<int>();
            var results = new List<int>();
            var buffered = source.BufferUntilUnlocked(_sessionServiceMock.Object);
            buffered.Subscribe(results.Add);

            // Start with locked state
            _stateSubject.OnNext(initial);

            // Act - emit items while locked
            for (int i = 0; i < itemCount; i++)
            {
                source.OnNext(i);
            }

            // Should have no results yet
            results.Should().BeEmpty($"No items before unlock - {scenario}");

            // Unlock
            _stateSubject.OnNext(final);
            await Task.Delay(50);

            // Assert - all buffered items should be replayed
            results.Should().BeEquivalentTo(Enumerable.Range(0, itemCount), scenario);
        }

        [Theory]
        [InlineData(new[] { 0.5, 0.3, 0.2 }, new[] { 100.0, 50.0, 25.0 }, 70.0)]
        [InlineData(new[] { 1.0, 1.0, 1.0 }, new[] { 100.0, 50.0, 0.0 }, 50.0)]
        [InlineData(new[] { 0.25, 0.25, 0.25, 0.25 }, new[] { 100.0, 75.0, 50.0, 25.0 }, 62.5)]
        [InlineData(new[] { 0.8, 0.2 }, new[] { 100.0, 0.0 }, 80.0)]
        [InlineData(new[] { 0.5, 0.5 }, new[] { 100.0, 100.0 }, 100.0)]
        public async Task CombineProgress_ShouldWeightProgressCorrectly(double[] weights, double[] progresses, double expectedTotal)
        {
            // Arrange
            var sources = new List<(IObservable<double> Progress, double Weight)>();
            for (int i = 0; i < weights.Length; i++)
            {
                sources.Add((Observable.Return(progresses[i]), weights[i]));
            }

            // Act
            var combined = ReactiveExtensions.CombineProgress(sources.ToArray());
            var result = await combined.FirstOrDefaultAsync();

            // Assert
            result.Should().BeApproximately(expectedTotal, 0.01); // Allow for floating point rounding
        }

        [Fact]
        public void CombineProgress_WithEmptyArray_ShouldReturnZero()
        {
            // Act
            var combined = ReactiveExtensions.CombineProgress();
            var result = combined.FirstOrDefault();

            // Assert
            result.Should().Be(0.0);
        }

        [Theory]
        [InlineData(5000, 1000, false, "Should not timeout when remaining time is above threshold")]
        [InlineData(500, 1000, true, "Should timeout when remaining time drops below threshold")]
        [InlineData(1000, 1000, true, "Should timeout when remaining time equals threshold")]
        public async Task CompleteOnTimeout_ShouldCompleteAtWarningThreshold(int remainingMs, int warningMs, bool shouldTimeout, string scenario)
        {
            // Arrange
            var remainingTimeSubject = new Subject<TimeSpan>();
            _sessionServiceMock.Setup(x => x.RemainingTimeStream).Returns(remainingTimeSubject.AsObservable());

            var source = new Subject<int>();
            var results = new List<int>();
            var completed = false;
            var tcs = new TaskCompletionSource<bool>();

            source
                .CompleteOnTimeout(_sessionServiceMock.Object, TimeSpan.FromMilliseconds(warningMs))
                .Subscribe(
                    value => results.Add(value),
                    error => tcs.TrySetException(error),
                    () => { completed = true; tcs.TrySetResult(true); });

            // Act - emit initial values
            source.OnNext(1);
            source.OnNext(2);
            await Task.Delay(10);
            
            // Trigger the remaining time update
            remainingTimeSubject.OnNext(TimeSpan.FromMilliseconds(remainingMs));
            await Task.Delay(50);
            
            // Try to emit more values after potential timeout
            if (!shouldTimeout)
            {
                source.OnNext(3);
                source.OnNext(4);
            }
            
            // Complete the source if not timed out
            if (!shouldTimeout)
            {
                source.OnCompleted();
            }

            // Wait for completion or timeout
            await Task.WhenAny(tcs.Task, Task.Delay(200));

            // Assert
            if (shouldTimeout)
            {
                results.Should().BeEquivalentTo(new[] { 1, 2 }, $"Should only have values before timeout - {scenario}");
                completed.Should().BeTrue($"Should complete on timeout - {scenario}");
            }
            else
            {
                results.Should().BeEquivalentTo(new[] { 1, 2, 3, 4 }, $"Should have all values - {scenario}");
                completed.Should().BeTrue($"Should complete normally - {scenario}");
            }
        }

        [Fact]
        public void ObserveOnUI_ShouldReturnObservable()
        {
            // Arrange
            var source = Observable.Return(42);

            // Act
            var uiObservable = source.ObserveOnUI();
            var result = uiObservable.FirstOrDefault();

            // Assert
            result.Should().Be(42);
        }

        [Fact]
        public void FromEventPattern_ShouldConvertEventsToObservable()
        {
            // Arrange
            var eventSource = new TestEventSource();
            var results = new List<TestEventArgs>();
            
            var observable = ReactiveExtensions.FromEventPattern<TestEventArgs>(
                h => eventSource.TestEvent += h,
                h => eventSource.TestEvent -= h);
            
            observable.Subscribe(results.Add);

            // Act
            var args1 = new TestEventArgs { Data = "First" };
            var args2 = new TestEventArgs { Data = "Second" };
            eventSource.RaiseEvent(args1);
            eventSource.RaiseEvent(args2);

            // Assert
            results.Should().HaveCount(2);
            results[0].Data.Should().Be("First");
            results[1].Data.Should().Be("Second");
        }

        [Theory]
        [InlineData(3, new[] { 1, 2, 3, 4, 5 }, 3, "Window size 3")]
        [InlineData(2, new[] { 1, 2, 3, 4 }, 3, "Window size 2")]
        [InlineData(5, new[] { 1, 2, 3, 4, 5, 6, 7 }, 3, "Window size 5")]
        public void SlidingWindow_ShouldCreateCorrectWindows(int windowSize, int[] input, int expectedWindows, string scenario)
        {
            // Arrange
            var source = input.ToObservable();
            var windows = new List<IList<int>>();

            // Act
            source.SlidingWindow(windowSize).Subscribe(windows.Add);

            // Assert
            windows.Should().HaveCount(expectedWindows, scenario);
            windows.All(w => w.Count == windowSize).Should().BeTrue($"All windows should have size {windowSize}");
            
            if (windows.Any())
            {
                windows.First().Should().BeEquivalentTo(input.Take(windowSize));
            }
        }

        private class TestEventSource
        {
            public event EventHandler<TestEventArgs>? TestEvent;

            public void RaiseEvent(TestEventArgs args)
            {
                TestEvent?.Invoke(this, args);
            }
        }

        private class TestEventArgs : EventArgs
        {
            public string Data { get; set; } = "";
        }
    }
}