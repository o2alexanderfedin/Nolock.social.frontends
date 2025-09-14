using System.Threading.Tasks;

namespace NoLock.Social.Core.Storage.Ipfs
{
    /// <summary>
    /// Wrapper interface for JavaScript interop with IPFS.
    /// Allows proper mocking in unit tests.
    /// </summary>
    public interface IIpfsJsInterop
    {
        /// <summary>
        /// Appends data to a file in IPFS MFS.
        /// </summary>
        ValueTask AppendDataAsync(string path, byte[] data);
        
        /// <summary>
        /// Reads a chunk of data from a file in IPFS MFS.
        /// </summary>
        ValueTask<byte[]> ReadChunkAsync(string path, long offset, int length);
        
        /// <summary>
        /// Gets the size of a file in IPFS MFS.
        /// </summary>
        ValueTask<long> GetFileSizeAsync(string path);
        
        /// <summary>
        /// Writes initial data to a file in IPFS MFS.
        /// </summary>
        ValueTask WriteDataAsync(string path, byte[] data);
    }
}