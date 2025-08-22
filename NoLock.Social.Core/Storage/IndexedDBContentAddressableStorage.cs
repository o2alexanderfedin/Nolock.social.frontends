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
    public class IndexedDBContentAddressableStorage
        : IContentAddressableStorage<byte[]>
    {
        private readonly IIndexedDBManagerWrapper _dbManager;
        private readonly IHashAlgorithm _hashAlgorithm;
        private readonly string _storeName = "content_addressable_storage";

        public IndexedDBContentAddressableStorage(
            IIndexedDBManagerWrapper dbManager, 
            IHashAlgorithm hashAlgorithm
        )
        {
            _dbManager = dbManager;
            _hashAlgorithm = hashAlgorithm;
        }

        public async ValueTask<string> StoreAsync(byte[] content)
        {
            var hash = await ComputeHashAsync(content);
            
            if (await ExistsAsync(hash))
            {
                return hash;
            }

            var contentEntry = new ContentEntry
            {
                Hash = hash,
                Content = content
            };

            var record = new StoreRecord<ContentEntry>
            {
                Storename = _storeName,
                Data = contentEntry
            };

            await _dbManager.AddRecord(record);
            return hash;
        }

        public async ValueTask<byte[]?> GetAsync(string hash)
        {
            try
            {
                var contentEntry = await _dbManager.GetRecordById<string, ContentEntry>(_storeName, hash);
                return contentEntry?.Content;
            }
            catch
            {
                return null;
            }
        }

        public async ValueTask<bool> ExistsAsync(string hash)
        {
            try
            {
                var contentEntry = await _dbManager.GetRecordById<string, ContentEntry>(_storeName, hash);
                return contentEntry != null;
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

        public async IAsyncEnumerable<string> GetAllHashesAsync()
        {
            var allContent = await _dbManager.GetRecords<ContentEntry>(_storeName);
            if (allContent != null)
            {
                foreach (var content in allContent)
                {
                    yield return content.Hash;
                }
            }
        }

        public async ValueTask<long> GetSizeAsync(string hash)
        {
            try
            {
                var contentEntry = await _dbManager.GetRecordById<string, ContentEntry>(_storeName, hash);
                return contentEntry?.Content.Length ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        public async ValueTask<long> GetTotalSizeAsync()
        {
            var allContent = await _dbManager.GetRecords<ContentEntry>(_storeName);
            return allContent?.Sum(c => c.Content.Length) ?? 0;
        }

        public async ValueTask ClearAsync()
        {
            await _dbManager.ClearStore(_storeName);
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