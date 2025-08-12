using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TG.Blazor.IndexedDB;
using Microsoft.JSInterop;

namespace NoLock.Social.Core.Storage
{
    public class IndexedDBContentAddressableStorage : IContentAddressableStorage
    {
        private readonly IndexedDBManager _dbManager;
        private readonly string _storeName = "content_addressable_storage";

        public IndexedDBContentAddressableStorage(IndexedDBManager dbManager)
        {
            _dbManager = dbManager;
        }

        public async Task<string> StoreAsync(byte[] content)
        {
            
            var hash = ComputeHash(content);
            
            if (await ExistsAsync(hash))
            {
                await UpdateAccessTimeAsync(hash);
                return hash;
            }

            var entry = new ContentEntry
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

            var record = new StoreRecord<ContentEntry>
            {
                Storename = _storeName,
                Data = entry
            };

            await _dbManager.AddRecord(record);
            return hash;
        }

        public async Task<string> StoreAsync(string content)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            var hash = await StoreAsync(bytes);
            
            var entry = await GetEntryAsync(hash);
            if (entry != null && entry.Metadata.AccessCount == 0)
            {
                entry.Metadata.ContentType = "text/plain";
                await UpdateEntryAsync(entry);
            }
            
            return hash;
        }

        public async Task<byte[]?> GetAsync(string hash)
        {
            
            var entry = await GetEntryAsync(hash);
            if (entry != null)
            {
                await UpdateAccessTimeAsync(hash);
                return entry.Content;
            }
            
            return null;
        }

        public async Task<string?> GetStringAsync(string hash)
        {
            var bytes = await GetAsync(hash);
            return bytes != null ? Encoding.UTF8.GetString(bytes) : null;
        }

        public async Task<bool> ExistsAsync(string hash)
        {
            
            try
            {
                var entry = await _dbManager.GetRecordById<string, ContentEntry>(_storeName, hash);
                return entry != null;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeleteAsync(string hash)
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

        public async Task<IEnumerable<string>> GetAllHashesAsync()
        {
            
            var allEntries = await _dbManager.GetRecords<ContentEntry>(_storeName);
            return allEntries?.Select(e => e.Hash) ?? Enumerable.Empty<string>();
        }

        public async Task<long> GetSizeAsync(string hash)
        {
            
            var metadata = await GetMetadataAsync(hash);
            return metadata?.Size ?? 0;
        }

        public async Task<long> GetTotalSizeAsync()
        {
            
            var allEntries = await _dbManager.GetRecords<ContentEntry>(_storeName);
            return allEntries?.Sum(e => e.Metadata.Size) ?? 0;
        }

        public async Task ClearAsync()
        {
            await _dbManager.ClearStore(_storeName);
        }

        public async Task<ContentMetadata?> GetMetadataAsync(string hash)
        {
            
            var entry = await GetEntryAsync(hash);
            return entry?.Metadata;
        }

        public async Task<IEnumerable<ContentMetadata>> GetAllMetadataAsync()
        {
            
            var allEntries = await _dbManager.GetRecords<ContentEntry>(_storeName);
            return allEntries?.Select(e => e.Metadata) ?? Enumerable.Empty<ContentMetadata>();
        }

        private async Task<ContentEntry?> GetEntryAsync(string hash)
        {
            try
            {
                return await _dbManager.GetRecordById<string, ContentEntry>(_storeName, hash);
            }
            catch
            {
                return null;
            }
        }

        private async Task UpdateEntryAsync(ContentEntry entry)
        {
            var record = new StoreRecord<ContentEntry>
            {
                Storename = _storeName,
                Data = entry
            };
            
            await _dbManager.UpdateRecord(record);
        }

        private async Task UpdateAccessTimeAsync(string hash)
        {
            var entry = await GetEntryAsync(hash);
            if (entry != null)
            {
                entry.Metadata.LastAccessedAt = DateTime.UtcNow;
                entry.Metadata.AccessCount++;
                await UpdateEntryAsync(entry);
            }
        }

        private string ComputeHash(byte[] content)
        {
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(content);
            return Convert.ToBase64String(hashBytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }
    }
}