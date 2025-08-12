using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TG.Blazor.IndexedDB;
using Microsoft.JSInterop;
using NoLock.Social.Core.Hashing;

namespace NoLock.Social.Core.Storage
{
    public class IndexedDBContentAddressableStorage : IContentAddressableStorage
    {
        private readonly IndexedDBManager _dbManager;
        private readonly IHashAlgorithm _hashAlgorithm;
        private readonly string _storeName = "content_addressable_storage";

        public IndexedDBContentAddressableStorage(IndexedDBManager dbManager, IHashAlgorithm hashAlgorithm)
        {
            _dbManager = dbManager;
            _hashAlgorithm = hashAlgorithm;
        }

        public async ValueTask<string> StoreAsync(byte[] content)
        {
            
            var hash = await ComputeHashAsync(content);
            
            if (await ExistsAsync(hash))
            {
                await UpdateAccessTimeAsync(hash);
                return hash;
            }

            var storedContent = new StoredContent
            {
                Hash = hash,
                Content = content,
                Metadata = new ContentMetadata
                {
                    Hash = hash,
                    Size = content.Length,
                    CreatedAt = DateTime.UtcNow,
                    LastAccessedAt = DateTime.UtcNow,
                    ContentType = "application/octet-stream",
                    AccessCount = 0
                }
            };

            var record = new StoreRecord<StoredContent>
            {
                Storename = _storeName,
                Data = storedContent
            };

            await _dbManager.AddRecord(record);
            return hash;
        }

        public async ValueTask<string> StoreAsync(string content)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            var hash = await StoreAsync(bytes);
            
            var storedContent = await GetStoredContentAsync(hash);
            if (storedContent != null && storedContent.Metadata.AccessCount == 0)
            {
                storedContent.Metadata.ContentType = "text/plain";
                await UpdateStoredContentAsync(storedContent);
            }
            
            return hash;
        }

        public async ValueTask<byte[]?> GetAsync(string hash)
        {
            
            var storedContent = await GetStoredContentAsync(hash);
            if (storedContent != null)
            {
                await UpdateAccessTimeAsync(hash);
                return storedContent.Content;
            }
            
            return null;
        }

        public async ValueTask<string?> GetStringAsync(string hash)
        {
            var bytes = await GetAsync(hash);
            return bytes != null ? Encoding.UTF8.GetString(bytes) : null;
        }

        public async ValueTask<bool> ExistsAsync(string hash)
        {
            
            try
            {
                var storedContent = await _dbManager.GetRecordById<string, StoredContent>(_storeName, hash);
                return storedContent != null;
            }
            catch
            {
                return false;
            }
        }

        public async ValueTask<bool> DeleteAsync(string hash)
        {
            
            try
            {
                await _dbManager.DeleteRecord(_storeName, hash);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async ValueTask<IEnumerable<string>> GetAllHashesAsync()
        {
            
            var allStoredContent = await _dbManager.GetRecords<StoredContent>(_storeName);
            return allStoredContent?.Select(s => s.Hash) ?? Enumerable.Empty<string>();
        }

        public async ValueTask<long> GetSizeAsync(string hash)
        {
            
            var metadata = await GetMetadataAsync(hash);
            return metadata?.Size ?? 0;
        }

        public async ValueTask<long> GetTotalSizeAsync()
        {
            
            var allStoredContent = await _dbManager.GetRecords<StoredContent>(_storeName);
            return allStoredContent?.Sum(s => s.Metadata.Size) ?? 0;
        }

        public async ValueTask ClearAsync()
        {
            await _dbManager.ClearStore(_storeName);
        }

        public async ValueTask<ContentMetadata?> GetMetadataAsync(string hash)
        {
            
            var storedContent = await GetStoredContentAsync(hash);
            return storedContent?.Metadata;
        }

        public async ValueTask<IEnumerable<ContentMetadata>> GetAllMetadataAsync()
        {
            
            var allStoredContent = await _dbManager.GetRecords<StoredContent>(_storeName);
            return allStoredContent?.Select(s => s.Metadata) ?? Enumerable.Empty<ContentMetadata>();
        }

        private async ValueTask<StoredContent?> GetStoredContentAsync(string hash)
        {
            try
            {
                return await _dbManager.GetRecordById<string, StoredContent>(_storeName, hash);
            }
            catch
            {
                return null;
            }
        }

        private async ValueTask UpdateStoredContentAsync(StoredContent storedContent)
        {
            var record = new StoreRecord<StoredContent>
            {
                Storename = _storeName,
                Data = storedContent
            };
            
            await _dbManager.UpdateRecord(record);
        }

        private async ValueTask UpdateAccessTimeAsync(string hash)
        {
            var storedContent = await GetStoredContentAsync(hash);
            if (storedContent != null)
            {
                storedContent.Metadata.LastAccessedAt = DateTime.UtcNow;
                storedContent.Metadata.AccessCount++;
                await UpdateStoredContentAsync(storedContent);
            }
        }

        private async ValueTask<string> ComputeHashAsync(byte[] content)
        {
            var hashBytes = await _hashAlgorithm.ComputeHashAsync(content);
            return ConvertToUrlSafeBase64(hashBytes);
        }

        private static string ConvertToUrlSafeBase64(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }
    }
}