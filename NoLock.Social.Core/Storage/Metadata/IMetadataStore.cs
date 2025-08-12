using System.Threading.Tasks;

namespace NoLock.Social.Core.Storage.Metadata
{
    public interface IMetadataStore<TMetadata> where TMetadata : class
    {
        ValueTask<TMetadata?> GetMetadataAsync(string contentHash);
        ValueTask StoreMetadataAsync(string contentHash, TMetadata metadata);
        ValueTask<bool> DeleteMetadataAsync(string contentHash);
        ValueTask<bool> ExistsAsync(string contentHash);
    }
}