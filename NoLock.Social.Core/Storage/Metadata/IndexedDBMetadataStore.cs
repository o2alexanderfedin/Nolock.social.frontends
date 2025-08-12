using System;
using System.Threading.Tasks;
using TG.Blazor.IndexedDB;

namespace NoLock.Social.Core.Storage.Metadata
{
    public class IndexedDBMetadataStore<TMetadata> : IMetadataStore<TMetadata> where TMetadata : class
    {
        private readonly IIndexedDBManagerWrapper _dbManager;
        private readonly string _storeName;

        public IndexedDBMetadataStore(IIndexedDBManagerWrapper dbManager, string storeName)
        {
            _dbManager = dbManager ?? throw new ArgumentNullException(nameof(dbManager));
            
            if (string.IsNullOrWhiteSpace(storeName))
                throw new ArgumentException("Store name cannot be null or empty", nameof(storeName));
            
            _storeName = storeName;
        }

        public async ValueTask<TMetadata?> GetMetadataAsync(string contentHash)
        {
            if (string.IsNullOrWhiteSpace(contentHash))
                throw new ArgumentException("Content hash cannot be null or empty", nameof(contentHash));

            try
            {
                var entry = await _dbManager.GetRecordById<string, MetadataEntry<TMetadata>>(_storeName, contentHash);
                return entry?.Metadata;
            }
            catch
            {
                return null;
            }
        }

        public async ValueTask StoreMetadataAsync(string contentHash, TMetadata metadata)
        {
            if (string.IsNullOrWhiteSpace(contentHash))
                throw new ArgumentException("Content hash cannot be null or empty", nameof(contentHash));
            
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));

            var entry = new MetadataEntry<TMetadata>
            {
                ContentHash = contentHash,
                Metadata = metadata,
                StoredAt = DateTime.UtcNow
            };

            var record = new StoreRecord<MetadataEntry<TMetadata>>
            {
                Storename = _storeName,
                Data = entry
            };

            await _dbManager.AddRecord(record);
        }

        public async ValueTask<bool> DeleteMetadataAsync(string contentHash)
        {
            if (string.IsNullOrWhiteSpace(contentHash))
                throw new ArgumentException("Content hash cannot be null or empty", nameof(contentHash));

            try
            {
                await _dbManager.DeleteRecord(_storeName, contentHash);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async ValueTask<bool> ExistsAsync(string contentHash)
        {
            if (string.IsNullOrWhiteSpace(contentHash))
                throw new ArgumentException("Content hash cannot be null or empty", nameof(contentHash));

            try
            {
                var entry = await _dbManager.GetRecordById<string, MetadataEntry<TMetadata>>(_storeName, contentHash);
                return entry != null;
            }
            catch
            {
                return false;
            }
        }
    }

    public class MetadataEntry<TMetadata>
    {
        public string ContentHash { get; set; } = string.Empty;
        public TMetadata? Metadata { get; set; }
        public DateTime StoredAt { get; set; }
    }
}