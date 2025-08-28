using System.Threading.Tasks;

namespace NoLock.Social.Core.Hashing
{
    /// <summary>
    /// Provides hashing functionality.
    /// </summary>
    public interface IHashService
    {
        /// <summary>
        /// Computes a hash of the input data.
        /// </summary>
        /// <param name="data">The input data to hash.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the hash string.</returns>
        Task<string> HashAsync<T>(T data);
    }
}