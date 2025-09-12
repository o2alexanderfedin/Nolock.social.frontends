using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace NoLock.Social.Core.Storage.Ipfs
{
    /// <summary>
    /// Stream implementation for writing content to IPFS progressively.
    /// Wraps JavaScript UnixFS add operation with chunk-based uploading.
    /// </summary>
    internal sealed class IpfsWriteStream : Stream
    {
        private readonly IJSObjectReference _jsStream;
        private readonly IProgress<long>? _progress;
        private readonly byte[] _buffer;
        private int _bufferPosition;
        private long _totalBytesWritten;
        private bool _disposed;
        private string? _resultCid;

        // Chunk size aligned with UnixFS defaults (256KB)
        private const int DefaultChunkSize = 262144;

        public IpfsWriteStream(IJSObjectReference jsStream, IProgress<long>? progress = null)
        {
            _jsStream = jsStream ?? throw new ArgumentNullException(nameof(jsStream));
            _progress = progress;
            _buffer = new byte[DefaultChunkSize];
            _bufferPosition = 0;
            _totalBytesWritten = 0;
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => !_disposed;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => _totalBytesWritten;
            set => throw new NotSupportedException("IPFS streams do not support seeking");
        }

        /// <summary>
        /// Gets the CID after the stream is closed.
        /// </summary>
        public string? ResultCid => _resultCid;

        /// <summary>
        /// Writes bytes to the IPFS stream.
        /// </summary>
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ValidateBufferArguments(buffer, offset, count);
            
            if (_disposed)
                throw new ObjectDisposedException(nameof(IpfsWriteStream));

            var bytesRemaining = count;
            var sourceOffset = offset;

            while (bytesRemaining > 0)
            {
                // Fill internal buffer
                var bytesToCopy = Math.Min(bytesRemaining, DefaultChunkSize - _bufferPosition);
                Array.Copy(buffer, sourceOffset, _buffer, _bufferPosition, bytesToCopy);
                
                _bufferPosition += bytesToCopy;
                sourceOffset += bytesToCopy;
                bytesRemaining -= bytesToCopy;

                // Flush buffer if full
                if (_bufferPosition >= DefaultChunkSize)
                {
                    await FlushBufferAsync(cancellationToken);
                }
            }

            _totalBytesWritten += count;
            _progress?.Report(_totalBytesWritten);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            WriteAsync(buffer, offset, count).GetAwaiter().GetResult();
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            if (_bufferPosition > 0)
            {
                await FlushBufferAsync(cancellationToken);
            }
        }

        public override void Flush()
        {
            FlushAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Completes the write operation and gets the resulting CID.
        /// </summary>
        public async Task<string> CompleteAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(IpfsWriteStream));

            // Flush any remaining data
            await FlushAsync(cancellationToken);

            // Complete the JavaScript stream and get CID
            _resultCid = await _jsStream.InvokeAsync<string>("complete", cancellationToken);
            
            if (string.IsNullOrEmpty(_resultCid))
                throw new InvalidOperationException("Failed to get CID from IPFS");

            return _resultCid;
        }

        private async Task FlushBufferAsync(CancellationToken cancellationToken)
        {
            if (_bufferPosition == 0)
                return;

            // Create a properly sized array for the chunk
            var chunk = new byte[_bufferPosition];
            Array.Copy(_buffer, 0, chunk, 0, _bufferPosition);

            // Send chunk to JavaScript
            await _jsStream.InvokeVoidAsync("writeChunk", cancellationToken, chunk);

            // Reset buffer
            _bufferPosition = 0;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException("IPFS streams do not support seeking");
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("Cannot set length of write-only stream");
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("Cannot read from write-only stream");
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Complete and dispose JavaScript stream
                    try
                    {
                        var task = CompleteAsync();
                        task.GetAwaiter().GetResult();
                    }
                    catch
                    {
                        // Ignore errors during disposal
                    }
                    finally
                    {
                        _jsStream?.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    }
                }
                _disposed = true;
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                try
                {
                    await CompleteAsync();
                }
                catch
                {
                    // Ignore errors during disposal
                }
                finally
                {
                    await _jsStream.DisposeAsync();
                    _disposed = true;
                }
            }
            await base.DisposeAsync();
        }

        private static void ValidateBufferArguments(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (offset + count > buffer.Length)
                throw new ArgumentException("Offset and count exceed buffer length");
        }
    }
}