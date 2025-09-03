using Microsoft.Extensions.DependencyInjection;
using NoLock.Social.Core.Storage;

namespace NoLock.Social.Core.Hashing
{
    /// <summary>
    /// Provides SHA-256 hashing functionality with multiple encoding options.
    /// </summary>
    public class SHA256HashService(IHashAlgorithm hashAlgorithm, IServiceProvider services)
        : IHashService
    {
        private readonly IHashAlgorithm _hashAlgorithm = hashAlgorithm ?? throw new ArgumentNullException(nameof(hashAlgorithm));
        private readonly IServiceProvider _services = services ?? throw new ArgumentNullException(nameof(services));

        /// <inheritdoc/>
        public async ValueTask<string> HashAsync<T>(T? data)
        {
            ArgumentNullException.ThrowIfNull(data);

            // Compute the hash using the algorithm
            var bytesToHash = data switch
            {
                byte[] bytes => bytes,
                _ => _services.GetRequiredService<ISerializer<T>>().Serialize(data)
            };
            var hashBytes = await _hashAlgorithm.ComputeHashAsync(bytesToHash);

            // Encode the hash based on the specified encoding
            return Convert.ToBase64String(hashBytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }
    }
}