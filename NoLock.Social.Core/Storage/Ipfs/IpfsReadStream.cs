using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace NoLock.Social.Core.Storage.Ipfs
{
    /// <summary>
    /// Stream implementation for reading IPFS content progressively.
    /// Wraps JavaScript UnixFS cat operation with chunk-based streaming.
    /// </summary>
    internal sealed class IpfsReadStream : Stream
    {
        private readonly IJSObjectReference _jsStream;
        private readonly byte[] _internalBuffer;
        private int _bufferOffset;
        private int _bufferLength;
        private long _position;
        private long _length;
        private bool _disposed;

        // Chunk size aligned with UnixFS defaults (256KB)
        private const int DefaultChunkSize = 262144;

        public IpfsReadStream(IJSObjectReference jsStream, long length)
        {
            _jsStream = jsStream ?? throw new ArgumentNullException(nameof(jsStream));
            _internalBuffer = new byte[DefaultChunkSize];
            _bufferOffset = 0;
            _bufferLength = 0;
            _length = length;
            _position = 0;
        }

        public override bool CanRead => !_disposed;
        public override bool CanSeek => false; // IPFS streams are forward-only
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException("IPFS streams do not support seeking");
        }

        /// <summary>
        /// Reads bytes from the IPFS stream with internal buffering for efficiency.
        /// </summary>
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ValidateBufferArguments(buffer, offset, count);
            
            if (_disposed)
                throw new ObjectDisposedException(nameof(IpfsReadStream));

            int totalBytesRead = 0;

            // Read from internal buffer first if available
            if (_bufferLength > _bufferOffset)
            {
                int bytesFromBuffer = Math.Min(count, _bufferLength - _bufferOffset);
                Array.Copy(_internalBuffer, _bufferOffset, buffer, offset, bytesFromBuffer);
                _bufferOffset += bytesFromBuffer;
                _position += bytesFromBuffer;
                totalBytesRead = bytesFromBuffer;
                
                // If we satisfied the request from buffer, return early
                if (totalBytesRead >= count)
                    return totalBytesRead;
                
                // Adjust for remaining bytes to read
                offset += bytesFromBuffer;
                count -= bytesFromBuffer;
            }

            // Need more data - fetch next chunk from JavaScript stream
            var chunk = await _jsStream.InvokeAsync<byte[]?>(
                "readChunk", 
                cancellationToken, 
                DefaultChunkSize);

            if (chunk == null || chunk.Length == 0)
                return totalBytesRead; // End of stream

            // If chunk is smaller than requested, copy directly to output
            if (chunk.Length <= count)
            {
                Array.Copy(chunk, 0, buffer, offset, chunk.Length);
                _position += chunk.Length;
                return totalBytesRead + chunk.Length;
            }

            // Chunk is larger than requested - buffer the excess
            Array.Copy(chunk, 0, buffer, offset, count);
            _position += count;
            
            // Store remaining data in internal buffer
            int remainingBytes = chunk.Length - count;
            Array.Copy(chunk, count, _internalBuffer, 0, remainingBytes);
            _bufferOffset = 0;
            _bufferLength = remainingBytes;
            
            return totalBytesRead + count;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            // Synchronous read not supported for IPFS
            return ReadAsync(buffer, offset, count).GetAwaiter().GetResult();
        }

        public override void Flush()
        {
            // No-op for read-only stream
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException("IPFS streams do not support seeking");
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("Cannot set length of read-only stream");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("Cannot write to read-only stream");
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose JavaScript stream reference
                    _jsStream?.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
                _disposed = true;
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                await _jsStream.DisposeAsync();
                _disposed = true;
            }
            await base.DisposeAsync();
        }

        private new static void ValidateBufferArguments(byte[] buffer, int offset, int count)
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