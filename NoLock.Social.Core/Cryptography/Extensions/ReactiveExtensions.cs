using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using NoLock.Social.Core.Cryptography.Interfaces;

namespace NoLock.Social.Core.Cryptography.Extensions
{
    /// <summary>
    /// Extension methods for Reactive programming patterns
    /// </summary>
    public static class ReactiveExtensions
    {
        /// <summary>
        /// Creates an observable that emits only when the session is unlocked
        /// </summary>
        public static IObservable<T> WhenUnlocked<T>(this IObservable<T> source, IReactiveSessionStateService sessionService)
        {
            return source.WithLatestFrom(
                sessionService.StateStream,
                (value, state) => new { Value = value, State = state })
                .Where(x => x.State == SessionState.Unlocked)
                .Select(x => x.Value);
        }

        /// <summary>
        /// Creates an observable that automatically retries operations with exponential backoff
        /// </summary>
        public static IObservable<T> RetryWithBackoff<T>(this IObservable<T> source, 
            int retryCount = 3, 
            TimeSpan? initialDelay = null)
        {
            var delay = initialDelay ?? TimeSpan.FromSeconds(1);
            
            return source.RetryWhen(failures => failures
                .Zip(System.Linq.Enumerable.Range(1, retryCount), (error, attempt) => new { error, attempt })
                .SelectMany(x =>
                {
                    var backoff = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * Math.Pow(2, x.attempt - 1));
                    if (x.attempt == retryCount)
                        return Observable.Throw<long>(x.error);
                    else
                        return Observable.Timer(backoff);
                }));
        }

        /// <summary>
        /// Throttles progress updates to avoid overwhelming the UI
        /// </summary>
        public static IObservable<KeyDerivationProgressEventArgs> ThrottleProgress(
            this IObservable<KeyDerivationProgressEventArgs> source,
            TimeSpan throttleDuration)
        {
            return source
                .Sample(throttleDuration)
                .Merge(source.Where(p => p.PercentComplete == 100)); // Always emit completion
        }

        /// <summary>
        /// Creates a progress reporter that can be used with async operations
        /// </summary>
        public static (IProgress<T> Progress, IObservable<T> Observable) CreateProgressObservable<T>()
        {
            var subject = new Subject<T>();
            var progress = new Progress<T>(value => subject.OnNext(value));
            return (progress, subject.AsObservable());
        }

        /// <summary>
        /// Buffers events until the session is unlocked, then replays them
        /// </summary>
        public static IObservable<T> BufferUntilUnlocked<T>(
            this IObservable<T> source, 
            IReactiveSessionStateService sessionService)
        {
            return source.Buffer(
                sessionService.StateStream
                    .DistinctUntilChanged()
                    .Where(state => state == SessionState.Unlocked))
                .SelectMany(buffered => buffered.ToObservable());
        }

        /// <summary>
        /// Combines multiple progress streams with weights
        /// </summary>
        public static IObservable<double> CombineProgress(params (IObservable<double> Progress, double Weight)[] sources)
        {
            if (sources == null || sources.Length == 0)
                return Observable.Return(0.0);

            var totalWeight = sources.Sum(s => s.Weight);
            
            return Observable.CombineLatest(
                sources.Select(s => s.Progress.Select(p => p * s.Weight / totalWeight)))
                .Select(progresses => progresses.Sum());
        }

        /// <summary>
        /// Creates an observable that completes when a timeout is reached
        /// </summary>
        public static IObservable<T> CompleteOnTimeout<T>(
            this IObservable<T> source,
            IReactiveSessionStateService sessionService,
            TimeSpan warningThreshold)
        {
            return source.TakeUntil(
                sessionService.RemainingTimeStream
                    .Where(remaining => remaining <= warningThreshold)
                    .Take(1));
        }

        /// <summary>
        /// Ensures operations are performed on the UI thread for Blazor components
        /// </summary>
        public static IObservable<T> ObserveOnUI<T>(this IObservable<T> source)
        {
            // In Blazor, we typically use InvokeAsync for UI updates
            // This is a placeholder that would need proper SynchronizationContext handling
            return source.ObserveOn(System.Reactive.Concurrency.Scheduler.Default);
        }

        /// <summary>
        /// Converts traditional event pattern to observable
        /// </summary>
        public static IObservable<TEventArgs> FromEventPattern<TEventArgs>(
            Action<EventHandler<TEventArgs>> addHandler,
            Action<EventHandler<TEventArgs>> removeHandler)
            where TEventArgs : EventArgs
        {
            return Observable.FromEventPattern<TEventArgs>(addHandler, removeHandler)
                .Select(e => e.EventArgs);
        }

        /// <summary>
        /// Creates a sliding window of values
        /// </summary>
        public static IObservable<IList<T>> SlidingWindow<T>(
            this IObservable<T> source,
            int windowSize)
        {
            return source.Buffer(windowSize, 1)
                .Where(buffer => buffer.Count == windowSize);
        }
    }
}