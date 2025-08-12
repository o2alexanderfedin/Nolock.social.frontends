using System.Collections.Generic;
using System.Threading.Tasks;
using TG.Blazor.IndexedDB;

namespace NoLock.Social.Core.Storage
{
    public interface IIndexedDBManagerWrapper
    {
        Task AddRecord<T>(StoreRecord<T> record);
        Task<T?> GetRecordById<TKey, T>(string storeName, TKey key) where T : class;
        Task DeleteRecord(string storeName, string key);
        Task<List<T>?> GetRecords<T>(string storeName) where T : class;
        Task ClearStore(string storeName);
    }
}