using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NoLock.Social.Core.Storage
{
    public interface IContentAddressableStorage
    {
        ValueTask<string> StoreAsync(byte[] content);
        
        ValueTask<byte[]?> GetAsync(string hash);
        
        ValueTask<bool> ExistsAsync(string hash);
        
        ValueTask<bool> DeleteAsync(string hash);
        
        IAsyncEnumerable<string> GetAllHashesAsync();
        
        ValueTask<long> GetSizeAsync(string hash);
        
        ValueTask<long> GetTotalSizeAsync();
        
        ValueTask ClearAsync();
    }
}