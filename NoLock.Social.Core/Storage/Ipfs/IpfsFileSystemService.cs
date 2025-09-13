using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;
using NoLock.Social.Core.Common.Guards;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace NoLock.Social.Core.Storage.Ipfs
{
    /// <summary>
    /// Provides IPFS file system operations through JavaScript interop.
    /// </summary>
    public class IpfsFileSystemService : IIpfsFileSystem, IAsyncDisposable
    {
        private const int DEFAULT_CHUNK_SIZE = 256 * 1024; // 256KB chunks
        
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<IpfsFileSystemService> _logger;
        private IJSObjectReference? _jsModule;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the IpfsFileSystemService class.
        /// </summary>
        /// <param name="jsRuntime">The JavaScript runtime for interop.</param>
        /// <param name="logger">The logger for diagnostic output.</param>
        public IpfsFileSystemService(IJSRuntime jsRuntime, ILogger<IpfsFileSystemService> logger)
        {
            _jsRuntime = Guard.AgainstNull(jsRuntime);
            _logger = logger;
        }

        /// <summary>
        /// Writes a file to IPFS from a stream source.
        /// </summary>
        /// <param name="path">The IPFS path (e.g., "/documents/report.pdf")</param>
        /// <param name="content">The stream containing file content</param>
        /// <param name="progress">Optional progress callback with bytes written</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The IPFS content identifier (CID) of the stored file</returns>
        public async Task<string> WriteFileAsync(
            string path, 
            Stream content, 
            IProgress<long>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            Guard.AgainstNull(path);
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be empty", nameof(path));
            Guard.AgainstNull(content);

            try
            {
                await EnsureModuleLoadedAsync();
                
                _logger?.LogInformation("Writing file to IPFS: {Path}", path);
                
                var buffer = new byte[DEFAULT_CHUNK_SIZE];
                var totalBytesWritten = 0L;
                
                // Initialize write operation in JavaScript
                var writeHandle = await _jsModule!.InvokeAsync<IJSObjectReference>(
                    "ipfs.beginWrite", 
                    cancellationToken, 
                    path);
                
                try
                {
                    // Write chunks
                    int bytesRead;
                    while ((bytesRead = await content.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        // Create a properly sized array for the actual bytes read
                        var chunk = bytesRead == buffer.Length ? buffer : buffer[..bytesRead];
                        
                        await writeHandle.InvokeAsync<object>(
                            "writeChunk", 
                            cancellationToken,
                            chunk);
                        
                        totalBytesWritten += bytesRead;
                        progress?.Report(totalBytesWritten);
                    }
                    
                    // Complete the write and get CID
                    var cid = await writeHandle.InvokeAsync<string>(
                        "complete", 
                        cancellationToken);
                    
                    _logger?.LogInformation("File written to IPFS successfully. CID: {Cid}, Size: {Size} bytes", 
                        cid, totalBytesWritten);
                    
                    return cid;
                }
                finally
                {
                    if (writeHandle != null)
                        await writeHandle.DisposeAsync();
                }
            }
            catch (JSException jsEx)
            {
                _logger?.LogError(jsEx, "JavaScript error writing file to IPFS: {Path}", path);
                throw new InvalidOperationException($"Failed to write file to IPFS: {jsEx.Message}", jsEx);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error writing file to IPFS: {Path}", path);
                throw;
            }
        }

        /// <summary>
        /// Reads a file from IPFS as a stream.
        /// </summary>
        /// <param name="path">The IPFS path or CID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A stream for reading the file content</returns>
        public async Task<Stream> ReadFileAsync(
            string path, 
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            Guard.AgainstNull(path);
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be empty", nameof(path));

            try
            {
                await EnsureModuleLoadedAsync();
                
                _logger?.LogInformation("Reading file from IPFS: {Path}", path);
                
                // Get file metadata to determine size
                var metadata = await GetMetadataAsync(path, cancellationToken);
                
                if (metadata == null)
                {
                    _logger?.LogWarning("File not found in IPFS: {Path}", path);
                    return null;
                }
                
                // Create read handle from JavaScript
                var readHandle = await _jsModule!.InvokeAsync<IJSObjectReference>(
                    "ipfs.beginRead", 
                    cancellationToken, 
                    path);
                
                // Return custom stream that reads from IPFS
                return new IpfsReadStream(readHandle, metadata.Size);
            }
            catch (JSException jsEx)
            {
                _logger?.LogError(jsEx, "JavaScript error reading file from IPFS: {Path}", path);
                throw new InvalidOperationException($"Failed to read file from IPFS: {jsEx.Message}", jsEx);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error reading file from IPFS: {Path}", path);
                throw;
            }
        }

        /// <summary>
        /// Lists directory contents in IPFS.
        /// </summary>
        /// <param name="path">The directory path</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Async enumerable of directory entries</returns>
        public async IAsyncEnumerable<IpfsFileEntry> ListDirectoryAsync(
            string path, 
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            Guard.AgainstNull(path);
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be empty", nameof(path));

            await EnsureModuleLoadedAsync();
            
            _logger?.LogInformation("Listing directory in IPFS: {Path}", path);
            
            // Get directory entries from JavaScript
            var entries = await _jsModule!.InvokeAsync<IpfsFileEntryDto[]>(
                "ipfs.listDirectory", 
                cancellationToken, 
                path);
            
            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                yield return new IpfsFileEntry(
                    entry.Name,
                    entry.Path,
                    entry.Cid,
                    entry.Size,
                    entry.Type == "directory" ? IpfsFileType.Directory : IpfsFileType.File,
                    entry.LastModified.HasValue ? DateTime.FromFileTimeUtc(entry.LastModified.Value) : null
                );
            }
        }

        /// <summary>
        /// Checks if a file or directory exists.
        /// </summary>
        /// <param name="path">The path to check</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the path exists</returns>
        public async Task<bool> ExistsAsync(
            string path, 
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            Guard.AgainstNull(path);
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be empty", nameof(path));

            try
            {
                await EnsureModuleLoadedAsync();
                
                _logger?.LogDebug("Checking existence in IPFS: {Path}", path);
                
                return await _jsModule!.InvokeAsync<bool>(
                    "ipfs.exists", 
                    cancellationToken, 
                    path);
            }
            catch (JSException jsEx)
            {
                _logger?.LogError(jsEx, "JavaScript error checking existence in IPFS: {Path}", path);
                return false;
            }
        }

        /// <summary>
        /// Gets metadata about a file or directory.
        /// </summary>
        /// <param name="path">The path to query</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>File metadata including size and type</returns>
        public async Task<IpfsFileMetadata> GetMetadataAsync(
            string path, 
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            Guard.AgainstNull(path);
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be empty", nameof(path));

            try
            {
                await EnsureModuleLoadedAsync();
                
                _logger?.LogDebug("Getting metadata from IPFS: {Path}", path);
                
                var metadata = await _jsModule!.InvokeAsync<IpfsFileMetadataDto?>(
                    "ipfs.getMetadata", 
                    cancellationToken, 
                    path);
                
                if (metadata == null)
                {
                    return null;
                }
                
                return new IpfsFileMetadata(
                    metadata.Path,
                    metadata.Cid,
                    metadata.Size,
                    metadata.Type == "directory" ? IpfsFileType.Directory : IpfsFileType.File,
                    metadata.BlockCount,
                    metadata.Created.HasValue ? DateTime.FromFileTimeUtc(metadata.Created.Value) : null,
                    metadata.LastModified.HasValue ? DateTime.FromFileTimeUtc(metadata.LastModified.Value) : null
                );
            }
            catch (JSException jsEx)
            {
                _logger?.LogError(jsEx, "JavaScript error getting metadata from IPFS: {Path}", path);
                throw new InvalidOperationException($"Failed to get metadata from IPFS: {jsEx.Message}", jsEx);
            }
        }

        /// <summary>
        /// Deletes a file from IPFS (unpins it).
        /// </summary>
        /// <param name="path">The path or CID to unpin</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if unpinned successfully</returns>
        public async Task<bool> DeleteAsync(
            string path, 
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            Guard.AgainstNull(path);
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be empty", nameof(path));

            try
            {
                await EnsureModuleLoadedAsync();
                
                _logger?.LogInformation("Unpinning from IPFS: {Path}", path);
                
                var result = await _jsModule!.InvokeAsync<bool>(
                    "ipfs.unpin", 
                    cancellationToken, 
                    path);
                
                if (result)
                {
                    _logger?.LogInformation("Successfully unpinned from IPFS: {Path}", path);
                }
                else
                {
                    _logger?.LogWarning("Failed to unpin from IPFS: {Path}", path);
                }
                
                return result;
            }
            catch (JSException jsEx)
            {
                _logger?.LogError(jsEx, "JavaScript error unpinning from IPFS: {Path}", path);
                return false;
            }
        }

        /// <summary>
        /// Ensures the JavaScript module is loaded.
        /// </summary>
        private async ValueTask EnsureModuleLoadedAsync()
        {
            if (_jsModule == null)
            {
                _jsModule = await _jsRuntime.InvokeAsync<IJSObjectReference>(
                    "import", 
                    "./js/modules/ipfs-module.js");
            }
        }

        /// <summary>
        /// Throws if the service has been disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(IpfsFileSystemService));
            }
        }

        /// <summary>
        /// Disposes the service and its resources.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                if (_jsModule != null)
                {
                    await _jsModule.DisposeAsync();
                    _jsModule = null;
                }
                
                _disposed = true;
            }
        }

        // DTOs for JavaScript interop
        public sealed class IpfsFileEntryDto
        {
            public string Name { get; set; } = string.Empty;
            public string Path { get; set; } = string.Empty;
            public string Cid { get; set; } = string.Empty;
            public long Size { get; set; }
            public string Type { get; set; } = "file";
            public long? LastModified { get; set; }
        }

        public sealed class IpfsFileMetadataDto
        {
            public string Path { get; set; } = string.Empty;
            public string Cid { get; set; } = string.Empty;
            public long Size { get; set; }
            public string Type { get; set; } = "file";
            public int? BlockCount { get; set; }
            public long? Created { get; set; }
            public long? LastModified { get; set; }
        }
    }
}