using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NoLock.Social.Core.Storage
{
    public interface IContentAddressableStorage
    {
        Task<string> StoreAsync(byte[] content);
        
        Task<string> StoreAsync(string content);
        
        Task<byte[]?> GetAsync(string hash);
        
        Task<string?> GetStringAsync(string hash);
        
        Task<bool> ExistsAsync(string hash);
        
        Task<bool> DeleteAsync(string hash);
        
        Task<IEnumerable<string>> GetAllHashesAsync();
        
        Task<long> GetSizeAsync(string hash);
        
        Task<long> GetTotalSizeAsync();
        
        Task ClearAsync();
        
        Task<ContentMetadata?> GetMetadataAsync(string hash);
        
        Task<IEnumerable<ContentMetadata>> GetAllMetadataAsync();
    }
}