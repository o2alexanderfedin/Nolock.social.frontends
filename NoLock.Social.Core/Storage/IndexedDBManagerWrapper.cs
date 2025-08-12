using System.Collections.Generic;
using System.Threading.Tasks;
using TG.Blazor.IndexedDB;

namespace NoLock.Social.Core.Storage
{
    public class IndexedDBManagerWrapper : IIndexedDBManagerWrapper
    {
        private readonly IndexedDBManager _dbManager;

        public IndexedDBManagerWrapper(IndexedDBManager dbManager)
        {
            _dbManager = dbManager;
        }

        public Task AddRecord<T>(StoreRecord<T> record)
        {
            return _dbManager.AddRecord(record);
        }

        public async Task<T?> GetRecordById<TKey, T>(string storeName, TKey key) where T : class
        {
            return await _dbManager.GetRecordById<TKey, T>(storeName, key);
        }

        public Task DeleteRecord(string storeName, string key)
        {
            return _dbManager.DeleteRecord(storeName, key);
        }

        public Task<List<T>?> GetRecords<T>(string storeName) where T : class
        {
            return _dbManager.GetRecords<T>(storeName);
        }

        public Task ClearStore(string storeName)
        {
            return _dbManager.ClearStore(storeName);
        }
    }
}