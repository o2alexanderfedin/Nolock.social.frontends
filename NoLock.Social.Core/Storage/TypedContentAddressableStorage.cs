using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NoLock.Social.Core.Storage
{
    /// <summary>
    /// Implementation of typed content-addressable storage that delegates to byte-array storage.
    /// Provides type-safe storage operations with automatic serialization/deserialization.
    /// </summary>
    /// <typeparam name="T">The type of content to store</typeparam>
    public class TypedContentAddressableStorage<T> : IContentAddressableStorage<T>
    {
        private readonly IContentAddressableStorage<byte[]> _byteStorage;
        private readonly ISerializer<T> _serializer;

        /// <summary>
        /// Initializes a new instance of the TypedContentAddressableStorage class.
        /// </summary>
        /// <param name="byteStorage">The underlying byte-array storage implementation</param>
        /// <param name="serializer">The serializer for converting T to/from byte arrays</param>
        public TypedContentAddressableStorage(
            IContentAddressableStorage<byte[]> byteStorage,
            ISerializer<T> serializer)
        {
            _byteStorage = byteStorage ?? throw new ArgumentNullException(nameof(byteStorage));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        /// <inheritdoc/>
        public async ValueTask<string> StoreAsync(T content)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            try
            {
                var bytes = _serializer.Serialize(content);
                return await _byteStorage.StoreAsync(bytes);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to store content of type {typeof(T).Name}", ex);
            }
        }

        /// <inheritdoc/>
        public async ValueTask<T?> GetAsync(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
            {
                throw new ArgumentException("Hash cannot be null or empty", nameof(hash));
            }

            try
            {
                var bytes = await _byteStorage.GetAsync(hash);
                if (bytes == null)
                {
                    return default(T);
                }

                return _serializer.Deserialize(bytes);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to retrieve content of type {typeof(T).Name} for hash {hash}", ex);
            }
        }

        /// <inheritdoc/>
        public ValueTask<bool> ExistsAsync(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
            {
                throw new ArgumentException("Hash cannot be null or empty", nameof(hash));
            }

            return _byteStorage.ExistsAsync(hash);
        }

        /// <inheritdoc/>
        public ValueTask<bool> DeleteAsync(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
            {
                throw new ArgumentException("Hash cannot be null or empty", nameof(hash));
            }

            return _byteStorage.DeleteAsync(hash);
        }

        /// <inheritdoc/>
        public IAsyncEnumerable<string> GetAllHashesAsync()
        {
            return _byteStorage.GetAllHashesAsync();
        }

        /// <inheritdoc/>
        public ValueTask<long> GetSizeAsync(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
            {
                throw new ArgumentException("Hash cannot be null or empty", nameof(hash));
            }

            return _byteStorage.GetSizeAsync(hash);
        }

        /// <inheritdoc/>
        public ValueTask<long> GetTotalSizeAsync()
        {
            return _byteStorage.GetTotalSizeAsync();
        }

        /// <inheritdoc/>
        public ValueTask ClearAsync()
        {
            return _byteStorage.ClearAsync();
        }
    }
}