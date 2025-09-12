using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NoLock.Social.Core.Storage.Ipfs
{
    /// <summary>
    /// Provides a familiar file system interface for IPFS operations using C# Stream patterns.
    /// Maps to JavaScript UnixFS operations with memory-efficient chunking.
    /// </summary>
    public interface IIpfsFileSystem : IAsyncDisposable
    {
        /// <summary>
        /// Writes a file to IPFS from a stream source.
        /// </summary>
        /// <param name="path">The IPFS path (e.g., "/documents/report.pdf")</param>
        /// <param name="content">The stream containing file content</param>
        /// <param name="progress">Optional progress callback with bytes written</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The IPFS content identifier (CID) of the stored file</returns>
        Task<string> WriteFileAsync(
            string path, 
            Stream content, 
            IProgress<long>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads a file from IPFS as a stream.
        /// </summary>
        /// <param name="path">The IPFS path or CID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A stream for reading the file content</returns>
        Task<Stream> ReadFileAsync(
            string path, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists directory contents in IPFS.
        /// </summary>
        /// <param name="path">The directory path</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Async enumerable of directory entries</returns>
        IAsyncEnumerable<IpfsFileEntry> ListDirectoryAsync(
            string path, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a file or directory exists.
        /// </summary>
        /// <param name="path">The path to check</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the path exists</returns>
        Task<bool> ExistsAsync(
            string path, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets metadata about a file or directory.
        /// </summary>
        /// <param name="path">The path to query</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>File metadata including size and type</returns>
        Task<IpfsFileMetadata> GetMetadataAsync(
            string path, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a file from IPFS (unpins it).
        /// </summary>
        /// <param name="path">The path or CID to unpin</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if unpinned successfully</returns>
        Task<bool> DeleteAsync(
            string path, 
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents an entry in an IPFS directory listing.
    /// </summary>
    public sealed record IpfsFileEntry(
        string Name,
        string Path,
        string Cid,
        long Size,
        IpfsFileType Type,
        DateTime? LastModified = null);

    /// <summary>
    /// File type enumeration for IPFS entries.
    /// </summary>
    public enum IpfsFileType
    {
        File,
        Directory
    }

    /// <summary>
    /// Metadata for an IPFS file or directory.
    /// </summary>
    public sealed record IpfsFileMetadata(
        string Path,
        string Cid,
        long Size,
        IpfsFileType Type,
        int? BlockCount = null,
        DateTime? Created = null,
        DateTime? LastModified = null);
}