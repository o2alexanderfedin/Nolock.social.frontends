using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NoLock.Social.Core.Storage.Ipfs
{
    /// <summary>
    /// Stream implementation for writing content to IPFS progressively.
    /// Buffers data in memory and flushes to JavaScript when threshold is reached.
    /// </summary>
    public sealed class IpfsWriteStream : Stream
    {
        private readonly IIpfsJsInterop _jsInterop;
        private readonly string _path;
        private readonly ILogger _logger;
        private readonly MemoryStream _buffer;
        private bool _disposed;
        private long _totalBytesWritten;

        // Buffer threshold (256KB) - auto-flush when exceeded
        private const int BufferThreshold = 256 * 1024;

        public IpfsWriteStream(IIpfsJsInterop jsInterop, string path, ILogger logger)
        {
            _jsInterop = jsInterop ?? throw new ArgumentNullException(nameof(jsInterop));
            _path = path ?? throw new ArgumentNullException(nameof(path));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _buffer = new MemoryStream();
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
        /// Writes bytes to the IPFS stream buffer, auto-flushing when threshold is exceeded.
        /// </summary>
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ValidateBufferArguments(buffer, offset, count);
            
            if (_disposed)
                throw new ObjectDisposedException(nameof(IpfsWriteStream));

            // Write to internal buffer
            await _buffer.WriteAsync(buffer, offset, count, cancellationToken);
            _totalBytesWritten += count;

            // Auto-flush if buffer exceeds threshold
            if (_buffer.Length > BufferThreshold)
            {
                await FlushBufferAsync(cancellationToken, autoFlush: true);
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            WriteAsync(buffer, offset, count).GetAwaiter().GetResult();
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            if (_buffer.Length > 0)
            {
                await FlushBufferAsync(cancellationToken, autoFlush: false);
            }
        }

        public override void Flush()
        {
            FlushAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        private async Task FlushBufferAsync(CancellationToken cancellationToken = default, bool autoFlush = false)
        {
            if (_buffer.Length == 0)
                return;

            // Get current position before we start manipulating the buffer
            var currentPosition = _buffer.Position;
            
            // Get data from buffer
            var data = _buffer.ToArray();
            
            // Determine how many bytes to flush
            int bytesToFlush;
            if (autoFlush)
            {
                // For auto-flush, only flush exactly BufferThreshold bytes
                bytesToFlush = Math.Min(data.Length, BufferThreshold);
            }
            else
            {
                // For manual flush, flush everything
                bytesToFlush = data.Length;
            }
            
            if (bytesToFlush > 0)
            {
                // Create array with exact size to flush
                var chunkToFlush = new byte[bytesToFlush];
                Array.Copy(data, 0, chunkToFlush, 0, bytesToFlush);
                
                // Call JavaScript to append data  
                await _jsInterop.AppendDataAsync(_path, chunkToFlush);
                
                // Reset buffer with remaining data (if any)
                _buffer.SetLength(0);
                _buffer.Position = 0;
                
                if (data.Length > bytesToFlush)
                {
                    // Write remaining data back to buffer
                    await _buffer.WriteAsync(data, bytesToFlush, data.Length - bytesToFlush, cancellationToken);
                    // Restore position to account for all written data
                    _buffer.Position = currentPosition - bytesToFlush;
                }
            }
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
            if (disposing && !_disposed)
            {
                DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                try
                {
                    // Flush any remaining buffered data
                    if (_buffer.Length > 0)
                    {
                        var remainingData = _buffer.ToArray();
                        await _jsInterop.AppendDataAsync(_path, remainingData);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error flushing data during disposal");
                }
                finally
                {
                    _buffer?.Dispose();
                    _disposed = true;
                }
            }
            await base.DisposeAsync();
        }

        private static new void ValidateBufferArguments(byte[] buffer, int offset, int count)
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