using System;
using System.Reactive.Disposables;

namespace NoLock.Social.Core.Cryptography.Extensions
{
    /// <summary>
    /// Extension methods for IDisposable and CompositeDisposable
    /// </summary>
    public static class DisposableExtensions
    {
        /// <summary>
        /// Adds a disposable to a CompositeDisposable and returns the disposable for chaining
        /// </summary>
        public static T AddTo<T>(this T disposable, CompositeDisposable composite) where T : IDisposable
        {
            if (disposable == null) throw new ArgumentNullException(nameof(disposable));
            if (composite == null) throw new ArgumentNullException(nameof(composite));
            
            composite.Add(disposable);
            return disposable;
        }
        
        /// <summary>
        /// Disposes an object safely, ignoring null references
        /// </summary>
        public static void SafeDispose(this IDisposable? disposable)
        {
            disposable?.Dispose();
        }
        
        /// <summary>
        /// Creates a new CompositeDisposable with the provided disposables
        /// </summary>
        public static CompositeDisposable CreateComposite(params IDisposable[] disposables)
        {
            var composite = new CompositeDisposable();
            foreach (var disposable in disposables)
            {
                if (disposable != null)
                    composite.Add(disposable);
            }
            return composite;
        }
    }
}