using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NoLock.Social.Core.Storage
{
    /// <summary>
    /// Abstract base decorator class for IContentAddressableStorage<T>.
    /// Provides default forwarding behavior to an underlying storage instance.
    /// Derived classes can override specific methods to add decorating functionality.
    /// </summary>
    /// <typeparam name="T">The type of content to store</typeparam>
    public abstract class ContentAddressableStorageDecorator<T> : IContentAddressableStorage<T>
    {
        /// <summary>
        /// The underlying storage instance that this decorator wraps.
        /// </summary>
        protected readonly IContentAddressableStorage<T> _underlying;

        /// <summary>
        /// Initializes a new instance of the ContentAddressableStorageDecorator class.
        /// </summary>
        /// <param name="underlying">The underlying storage instance to wrap</param>
        /// <exception cref="ArgumentNullException">Thrown when underlying is null</exception>
        protected ContentAddressableStorageDecorator(IContentAddressableStorage<T> underlying)
        {
            _underlying = underlying ?? throw new ArgumentNullException(nameof(underlying));
        }

        /// <summary>
        /// Stores content of type T and returns its content-addressed hash.
        /// Default implementation forwards to underlying storage.
        /// </summary>
        /// <param name="content">The content to store</param>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>The hash of the stored content</returns>
        public virtual ValueTask<string> StoreAsync(T content, CancellationToken cancellation = default)
        {
            return _underlying.StoreAsync(content, cancellation);
        }

        /// <summary>
        /// Retrieves content by its hash, returning the deserialized object or null if not found.
        /// Default implementation forwards to underlying storage.
        /// </summary>
        /// <param name="hash">The hash of the content to retrieve</param>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>The deserialized content or null if not found</returns>
        public virtual ValueTask<T?> GetAsync(string hash, CancellationToken cancellation = default)
        {
            return _underlying.GetAsync(hash, cancellation);
        }

        /// <summary>
        /// Checks if content exists for the given hash.
        /// Default implementation forwards to underlying storage.
        /// </summary>
        /// <param name="hash">The hash to check</param>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>True if content exists, false otherwise</returns>
        public virtual ValueTask<bool> ExistsAsync(string hash, CancellationToken cancellation = default)
        {
            return _underlying.ExistsAsync(hash, cancellation);
        }

        /// <summary>
        /// Deletes content by its hash.
        /// Default implementation forwards to underlying storage.
        /// </summary>
        /// <param name="hash">The hash of the content to delete</param>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>True if content was deleted, false if not found</returns>
        public virtual ValueTask<bool> DeleteAsync(string hash, CancellationToken cancellation = default)
        {
            return _underlying.DeleteAsync(hash, cancellation);
        }

        /// <summary>
        /// Gets all stored content hashes.
        /// Default implementation forwards to underlying storage.
        /// </summary>
        /// <returns>An async enumerable of all stored hashes</returns>
        public virtual IAsyncEnumerable<string> AllHashes => _underlying.AllHashes;

        /// <summary>
        /// Gets all stored content entities.
        /// Default implementation forwards to underlying storage.
        /// </summary>
        /// <returns>An async enumerable of all stored content entities</returns>
        public virtual IAsyncEnumerable<T> All => _underlying.All;

        /// <summary>
        /// Clears all stored content.
        /// Default implementation forwards to underlying storage.
        /// </summary>
        /// <param name="cancellation">Cancellation token</param>
        public virtual ValueTask ClearAsync(CancellationToken cancellation = default)
        {
            return _underlying.ClearAsync(cancellation);
        }

        /// <summary>
        /// Subscribes an observer to receive notifications when content is stored.
        /// Default implementation forwards to underlying storage.
        /// </summary>
        /// <param name="observer">The observer to subscribe</param>
        /// <returns>A disposable that can be used to unsubscribe the observer</returns>
        public virtual IDisposable Subscribe(IObserver<string> observer)
        {
            return _underlying.Subscribe(observer);
        }
    }
}