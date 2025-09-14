using System;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace NoLock.Social.Core.Storage.Ipfs
{
    /// <summary>
    /// Implementation of IIpfsJsInterop that wraps JavaScript interop calls.
    /// </summary>
    public sealed class IpfsJsInterop : IIpfsJsInterop, IAsyncDisposable
    {
        private readonly IJSObjectReference _jsModule;
        private bool _disposed;

        public IpfsJsInterop(IJSObjectReference jsModule)
        {
            _jsModule = jsModule ?? throw new ArgumentNullException(nameof(jsModule));
        }

        /// <summary>
        /// Appends data to a file in IPFS MFS.
        /// </summary>
        public async ValueTask AppendDataAsync(string path, byte[] data)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(IpfsJsInterop));
                
            await _jsModule.InvokeVoidAsync("appendBytes", path, data);
        }

        /// <summary>
        /// Reads a chunk of data from a file in IPFS MFS.
        /// </summary>
        public async ValueTask<byte[]> ReadChunkAsync(string path, long offset, int length)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(IpfsJsInterop));
                
            return await _jsModule.InvokeAsync<byte[]>("readChunk", path, offset, length);
        }

        /// <summary>
        /// Gets the size of a file in IPFS MFS.
        /// </summary>
        public async ValueTask<long> GetFileSizeAsync(string path)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(IpfsJsInterop));
                
            return await _jsModule.InvokeAsync<long>("getFileSize", path);
        }

        /// <summary>
        /// Writes initial data to a file in IPFS MFS.
        /// </summary>
        public async ValueTask WriteDataAsync(string path, byte[] data)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(IpfsJsInterop));
                
            await _jsModule.InvokeVoidAsync("writeBytes", path, data);
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                if (_jsModule != null)
                {
                    await _jsModule.DisposeAsync();
                }
                _disposed = true;
            }
        }
    }
}