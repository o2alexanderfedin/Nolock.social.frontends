using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NoLock.Social.Core.Storage
{
    public interface IContentAddressableStorage
    {
        ValueTask<string> StoreAsync(byte[] content);
        
        ValueTask<string> StoreAsync(string content);
        
        ValueTask<byte[]?> GetAsync(string hash);
        
        ValueTask<string?> GetStringAsync(string hash);
        
        ValueTask<bool> ExistsAsync(string hash);
        
        ValueTask<bool> DeleteAsync(string hash);
        
        ValueTask<IEnumerable<string>> GetAllHashesAsync();
        
        ValueTask<long> GetSizeAsync(string hash);
        
        ValueTask<long> GetTotalSizeAsync();
        
        ValueTask ClearAsync();
    }
}