using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NoLock.Social.Core.Storage.Ipfs
{
    /// <summary>
    /// Stream implementation for reading content from IPFS with seek support.
    /// Reads data in chunks from JavaScript interop.
    /// </summary>
    public sealed class IpfsReadStream : Stream
    {
        private readonly IIpfsJsInterop _jsInterop;
        private readonly string _path;
        private readonly ILogger _logger;
        private long _position;
        private long _length = -1;
        private bool _disposed;

        public IpfsReadStream(IIpfsJsInterop jsInterop, string path, ILogger logger)
        {
            _jsInterop = jsInterop ?? throw new ArgumentNullException(nameof(jsInterop));
            _path = path ?? throw new ArgumentNullException(nameof(path));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public override bool CanRead => !_disposed;
        public override bool CanSeek => !_disposed;
        public override bool CanWrite => false;
        
        public override long Length
        {
            get
            {
                if (_length < 0)
                {
                    _length = _jsInterop.GetFileSizeAsync(_path).GetAwaiter().GetResult();
                }
                return _length;
            }
        }

        public override long Position
        {
            get => _position;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), "Position cannot be negative");
                _position = value;
            }
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            ValidateBufferArguments(buffer, offset, count);
            
            if (_disposed)
                throw new ObjectDisposedException(nameof(IpfsReadStream));

            // Initialize length if not already done
            if (_length < 0)
            {
                _length = await _jsInterop.GetFileSizeAsync(_path);
            }

            // Check if we're at or past the end of file
            if (_position >= _length)
            {
                return 0;
            }

            // Calculate how many bytes we can actually read
            var bytesToRead = (int)Math.Min(count, _length - _position);
            
            // Read chunk from JavaScript
            var data = await _jsInterop.ReadChunkAsync(_path, _position, bytesToRead);
            
            // Copy data to the provided buffer
            var actualBytesRead = Math.Min(data.Length, bytesToRead);
            if (actualBytesRead > 0)
            {
                Array.Copy(data, 0, buffer, offset, actualBytesRead);
                _position += actualBytesRead;
            }
            
            return actualBytesRead;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer, offset, count).GetAwaiter().GetResult();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(IpfsReadStream));

            // Initialize length if seeking from end
            if (origin == SeekOrigin.End && _length < 0)
            {
                _length = _jsInterop.GetFileSizeAsync(_path).GetAwaiter().GetResult();
            }

            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;
                case SeekOrigin.Current:
                    Position = _position + offset;
                    break;
                case SeekOrigin.End:
                    Position = _length + offset;
                    break;
                default:
                    throw new ArgumentException("Invalid seek origin", nameof(origin));
            }

            return _position;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("Cannot set length of read-only stream");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("Cannot write to read-only stream");
        }

        public override void Flush()
        {
            // No-op for read-only stream
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
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