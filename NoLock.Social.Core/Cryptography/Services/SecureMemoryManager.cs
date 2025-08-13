using NoLock.Social.Core.Cryptography.Interfaces;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace NoLock.Social.Core.Cryptography.Services
{
    /// <summary>
    /// Manages secure memory allocation and clearing for sensitive cryptographic data
    /// </summary>
    public class SecureMemoryManager : ISecureMemoryManager, IDisposable
    {
        private readonly List<WeakReference> _buffers = new();
        private readonly ReaderWriterLockSlim _lock = new();
        private bool _disposed;

        public ISecureBuffer CreateSecureBuffer(int size)
        {
            if (size <= 0)
                throw new ArgumentOutOfRangeException(nameof(size), "Size must be greater than 0");

            var buffer = new SecureBuffer(size);
            TrackBuffer(buffer);
            return buffer;
        }

        public ISecureBuffer CreateSecureBuffer(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var buffer = new SecureBuffer(data);
            
            // Clear the original data
            Array.Clear(data, 0, data.Length);
            
            TrackBuffer(buffer);
            return buffer;
        }

        public void ClearAllBuffers()
        {
            _lock.EnterReadLock();
            try
            {
                foreach (var weakRef in _buffers)
                {
                    if (weakRef.IsAlive && weakRef.Target is SecureBuffer buffer)
                    {
                        buffer.Clear();
                    }
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        private void TrackBuffer(SecureBuffer buffer)
        {
            _lock.EnterWriteLock();
            try
            {
                // Clean up dead references while we're here
                _buffers.RemoveAll(wr => !wr.IsAlive);
                
                // Add new buffer
                _buffers.Add(new WeakReference(buffer));
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            ClearAllBuffers();
            _lock?.Dispose();
            _disposed = true;
        }

        /// <summary>
        /// Internal implementation of secure buffer
        /// </summary>
        private class SecureBuffer : ISecureBuffer
        {
            private byte[] _data;
            private bool _cleared;
            private bool _disposed;
            private readonly object _lock = new();

            public SecureBuffer(int size)
            {
                _data = new byte[size];
                _cleared = false;
            }

            public SecureBuffer(byte[] data)
            {
                _data = new byte[data.Length];
                Array.Copy(data, _data, data.Length);
                _cleared = false;
            }

            public byte[] Data
            {
                get
                {
                    ThrowIfDisposed();
                    return _data;
                }
            }

            public int Size => _data?.Length ?? 0;

            public bool IsCleared => _cleared;

            public void Clear()
            {
                lock (_lock)
                {
                    if (_data != null && !_cleared)
                    {
                        // Multiple passes for better security
                        for (var pass = 0; pass < 3; pass++)
                        {
                            for (var i = 0; i < _data.Length; i++)
                            {
                                _data[i] = (byte)(pass % 2 == 0 ? 0x00 : 0xFF);
                            }
                        }
                        
                        // Final clear to zeros
                        Array.Clear(_data, 0, _data.Length);
                        _cleared = true;
                    }
                }
            }

            public void CopyTo(ISecureBuffer destination)
            {
                ThrowIfDisposed();
                
                if (destination == null)
                    throw new ArgumentNullException(nameof(destination));
                
                if (destination.Size < Size)
                    throw new ArgumentException("Destination buffer is too small", nameof(destination));
                
                Array.Copy(_data, destination.Data, _data.Length);
            }

            public ReadOnlySpan<byte> AsSpan()
            {
                ThrowIfDisposed();
                return new ReadOnlySpan<byte>(_data);
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                Clear();
                _disposed = true;
                
                // Help GC
                _data = null!;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ThrowIfDisposed()
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(SecureBuffer));
            }
        }
    }
}