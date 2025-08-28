using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.JSInterop;
using NoLock.Social.Core.Hashing;
using NoLock.Social.Core.Storage;
using Nolock.Social.Storage.IndexedDb.Models;

namespace Nolock.Social.Storage.IndexedDb;

public sealed class IndexedDbContentAddressableStorage<T>
    : IContentAddressableStorage<T>
{
    private readonly IndexedDbCasDatabase _database;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ISerializer<T> _serializer;
    private readonly IHashService _hashService;

    public IndexedDbContentAddressableStorage(
        IJSRuntime jsRuntime, 
        ISerializer<T> serializer,
        IHashService hashService)
    {
        _database = new IndexedDbCasDatabase(jsRuntime);
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _hashService = hashService ?? throw new ArgumentNullException(nameof(hashService));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async ValueTask<string> StoreAsync(T entity, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        await EnsureInitializedAsync();
        
        // Generate SHA256 hash
        var hash = await ComputeHash(entity);
        
        // Check cancellation after computing hash but before database operations
        if (cancellation.IsCancellationRequested)
            throw new TaskCanceledException();
        
        // Check if already exists
        if (await ExistsAsync(hash, cancellation))
            return hash;

        // Create entry
        var entry = new CasEntry<T>
        {
            Hash = hash,
            Data = entity,
            TypeName = typeof(T).FullName!,
            StoredAt = DateTime.UtcNow
        };

        // Store in IndexedDB
        await _database.CasEntries.AddAsync(entry, hash);
        
        return hash;
    }

    public async ValueTask<T?> GetAsync(string contentHash, CancellationToken cancellation = default)
    {
        var entry = await GetRawAsync(contentHash, cancellation);
        return entry is null || entry.Data is null
            ? default
            : entry.Data;
    }

    public async ValueTask<bool> ExistsAsync(string contentHash, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(contentHash);
        
        await EnsureInitializedAsync();

        return await GetRawAsync(contentHash, cancellation) is not null;
    }

    public async ValueTask<bool> DeleteAsync(string contentHash, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(contentHash);

        await EnsureInitializedAsync();
        
        try
        {
            await _database.CasEntries.DeleteAsync(contentHash);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public IAsyncEnumerable<T> All
        => EnumerateRawAsync().Select(x => x.Data);

    private async IAsyncEnumerable<CasEntry<T>> EnumerateRawAsync()
    {
        await EnsureInitializedAsync();

        var items = (await _database.CasEntries.GetAllAsync<CasEntry<T>>())
            .ToAsyncEnumerable()
            .Where(e => e.TypeName == typeof(T).FullName);
        await foreach (var item in items)
        {
            yield return item;
        }
    }

    public IAsyncEnumerable<string> AllHashes
        => EnumerateRawAsync()
            .Select(x => x.Hash);

    public async ValueTask ClearAsync(CancellationToken cancellation = default)
    {
        await EnsureInitializedAsync();
        
        await foreach (var entry in EnumerateRawAsync().WithCancellation(cancellation))
        {
            if (cancellation.IsCancellationRequested)
                break;
            
            // Call database directly to let exceptions propagate
            await _database.CasEntries.DeleteAsync(entry.Hash);
        }
    }

    private async ValueTask<CasEntry<T>?> GetRawAsync(string contentHash, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(contentHash);
        
        await EnsureInitializedAsync();
        
        return cancellation.IsCancellationRequested
            ? throw new TaskCanceledException()
            : await _database.CasEntries.GetAsync<string, CasEntry<T>>(contentHash);
    }

    private async ValueTask EnsureInitializedAsync()
        => await _database.EnsureIsOpenAsync();

    private async Task<string> ComputeHash(T data)
    {
        // Serialize the data to bytes for hashing
        var bytes = _serializer.Serialize(data);
        
        // Use the injected hash service (now always returns Base64Url encoding)
        return await _hashService.HashAsync(bytes);
    }
}