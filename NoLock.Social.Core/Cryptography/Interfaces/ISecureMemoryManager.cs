using System;

namespace NoLock.Social.Core.Cryptography.Interfaces
{
    /// <summary>
    /// Manages secure memory allocation and clearing for sensitive cryptographic data
    /// </summary>
    public interface ISecureMemoryManager
    {
        /// <summary>
        /// Create a secure buffer that automatically clears on disposal
        /// </summary>
        ISecureBuffer CreateSecureBuffer(int size);

        /// <summary>
        /// Create a secure buffer from existing data (data will be copied and original cleared)
        /// </summary>
        ISecureBuffer CreateSecureBuffer(byte[] data);

        /// <summary>
        /// Clear all tracked secure buffers immediately
        /// </summary>
        void ClearAllBuffers();
    }

    /// <summary>
    /// A secure buffer that automatically clears its contents on disposal
    /// </summary>
    public interface ISecureBuffer : IDisposable
    {
        /// <summary>
        /// Get the buffer data (use with caution - data should not be copied)
        /// </summary>
        byte[] Data { get; }

        /// <summary>
        /// Get the buffer size
        /// </summary>
        int Size { get; }

        /// <summary>
        /// Check if the buffer has been cleared
        /// </summary>
        bool IsCleared { get; }

        /// <summary>
        /// Clear the buffer contents immediately
        /// </summary>
        void Clear();

        /// <summary>
        /// Copy data from this buffer to another secure buffer
        /// </summary>
        void CopyTo(ISecureBuffer destination);

        /// <summary>
        /// Get a span view of the buffer for zero-copy operations
        /// </summary>
        ReadOnlySpan<byte> AsSpan();
    }
}