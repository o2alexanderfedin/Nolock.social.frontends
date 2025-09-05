using System.Diagnostics;
using System.Linq.Expressions;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CloudNimble.BlazorEssentials.IndexedDb;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using NoLock.Social.Core.Hashing;
using NoLock.Social.Core.Storage;
using Nolock.Social.Storage.IndexedDb.Models;

namespace Nolock.Social.Storage.IndexedDb;

public sealed class IndexedDbContentAddressableStorage<T>
    : IContentAddressableStorage<T>, IDisposable
{
    //[ObjectStoreAttribute(KeyPath = "hash")]
    private readonly IndexedDbCasDatabase _database;
    private readonly IHashService _hashService;
    private readonly ILogger<IndexedDbContentAddressableStorage<T>> _logger;
    private readonly Subject<string> _hashNotifications = new();

    public IndexedDbContentAddressableStorage(
        IJSRuntime jsRuntime,
        IHashService hashService,
        ILogger<IndexedDbContentAddressableStorage<T>> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        // Cast the generic logger to non-generic for the database
        _database = new IndexedDbCasDatabase(jsRuntime, logger);
        _hashService = hashService ?? throw new ArgumentNullException(nameof(hashService));

        _logger.LogInformation("IndexedDbContentAddressableStorage<{TypeName}> initialized", typeof(T).Name);
    }

    public async ValueTask<string> StoreAsync(T entity, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        
        _logger.LogDebug("StoreAsync called for type {TypeName}", typeof(T).Name);

        await EnsureInitializedAsync();
        
        // Generate SHA256 hash
        var hash = await ComputeHash(entity);
        _logger.LogDebug("Computed hash: {Hash}", hash);
        
        // Check cancellation after computing hash but before database operations
        if (cancellation.IsCancellationRequested)
        {
            _logger.LogDebug("StoreAsync cancelled for hash {Hash}", hash);
            throw new TaskCanceledException();
        }
        
        // Check if already exists
        if (await ExistsAsync(hash, cancellation))
        {
            _logger.LogDebug("Entity with hash {Hash} already exists, returning existing hash", hash);
            return hash;
        }
        
        // Create entry
        var entry = new CasEntry<T>
        {
            Hash = hash,
            Data = entity,
            TypeName = typeof(T).FullName!,
            StoredAt = DateTime.UtcNow
        };
        
        // Log the entry structure before storing
        _logger.LogDebug("Created CasEntry with Hash='{Hash}', TypeName='{TypeName}', Data={Data}, StoredAt={StoredAt}",
            entry.Hash, entry.TypeName, entry.Data, entry.StoredAt);
        
        try
        {
            // Store in IndexedDB (using inline key from entry.Hash property)
            _logger.LogDebug("Attempting to store entry in IndexedDB with hash {Hash}", hash);
            await _database.CasEntries.AddAsync<CasEntry<T>, string>(entry);
            // Note: When using inline keys (KeyPath = "hash"), AddAsync returns null/empty
            // The actual key is taken from entry.Hash property automatically
            
            _logger.LogInformation("Successfully stored entity with hash {Hash} in IndexedDB", hash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store entity with hash {Hash} in IndexedDB. Entry: {Entry}", hash, entry);
            throw;
        }
        
        // Notify observers about the new content hash
        _hashNotifications.OnNext(hash);
        _logger.LogDebug("Notified observers about new content hash {Hash}", hash);
        
        return hash;
    }

    public async ValueTask<T?> GetAsync(string contentHash, CancellationToken cancellation = default)
    {
        _logger.LogDebug("GetAsync called for hash {Hash}", contentHash);
        
        var entry = await GetRawAsync(contentHash, cancellation);
        if (entry is null)
        {
            _logger.LogDebug("No entry found for hash {Hash}", contentHash);
            return default;
        }
        
        _logger.LogDebug("Found entry for hash {Hash}, deserializing", contentHash);
        return entry.Data;
    }

    public async ValueTask<bool> ExistsAsync(string contentHash, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(contentHash);
        
        _logger.LogDebug("ExistsAsync called for hash {Hash}", contentHash);
        
        await EnsureInitializedAsync();

        var exists = await GetRawAsync(contentHash, cancellation) is not null;
        _logger.LogDebug("Hash {Hash} exists: {Exists}", contentHash, exists);
        
        return exists;
    }

    public async ValueTask<bool> DeleteAsync(string contentHash, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(contentHash);
        
        _logger.LogDebug("DeleteAsync called for hash {Hash}", contentHash);

        await EnsureInitializedAsync();
        
        try
        {
            // Always attempt delete operation regardless of existence (idempotent behavior)
            await _database.CasEntries.DeleteAsync(contentHash);
            _hashNotifications.OnNext(contentHash);
            _logger.LogInformation("Successfully deleted entry with hash {Hash}", contentHash);
            // IndexedDB delete operations are idempotent - return true even if item doesn't exist
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete entry with hash {Hash}", contentHash);
            return false;
        }
    }

    public IAsyncEnumerable<T> All
        => EnumerateRawAsync()
            .Select(x => x.Data);

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

    private async IAsyncEnumerable<CasEntry<T>> EnumerateRawAsync()
    {
        var keys = await _database.CasEntries.GetAllKeysAsync<string>() ?? new List<string>();
        var items = keys
            .ToAsyncEnumerable()
            .SelectAwait(async key => await GetRawAsync(key, CancellationToken.None))
            .Where(x => x is not null)
            .Select(x => x!)
            .Where(x => x.TypeName == typeof(T).FullName);
        await foreach (var item in items)
        {
            yield return item;
        }
    }

    private async ValueTask<CasEntry<T>?> GetRawAsync(string contentHash, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(contentHash);
        
        _logger.LogDebug("GetRawAsync called for hash {Hash}", contentHash);
        
        if (cancellation.IsCancellationRequested)
        {
            _logger.LogDebug("GetRawAsync cancelled for hash {Hash}", contentHash);
            throw new TaskCanceledException();
        }
        
        try
        {
            var entry = await _database.CasEntries.GetAsync<string, CasEntry<T>>(contentHash);
            if (entry != null)
            {
                _logger.LogDebug("Retrieved entry for hash {Hash}, TypeName: {TypeName}, Data: {Data}",
                    contentHash, entry.TypeName, entry.Data);
            }
            else
            {
                _logger.LogDebug("No entry found for hash {Hash}", contentHash);
            }
            return entry;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving entry for hash {Hash}", contentHash);
            throw;
        }
    }

    private async ValueTask EnsureInitializedAsync()
    {
        _logger.LogDebug("Ensuring database is initialized");
        await _database.EnsureIsOpenAsync();
        _logger.LogDebug("Database initialization confirmed");
    }

    
    private async Task<string> ComputeHash(T? data)
    {
        _logger.LogDebug("Computing hash for type {TypeName}", typeof(T).Name);
        
        // Use the injected hash service (now always returns Base64Url encoding)
        var hash = await _hashService.HashAsync<T>(data);
        _logger.LogDebug("Computed hash: {Hash}", hash);
        
        return hash;
    }

    /// <summary>
    /// Subscribes an observer to receive notifications when new content is stored.
    /// </summary>
    /// <param name="observer">The observer to subscribe for hash notifications</param>
    /// <returns>A disposable that can be used to unsubscribe the observer</returns>
    public IDisposable Subscribe(IObserver<string> observer)
    {
        return _hashNotifications.Subscribe(observer);
    }

    /// <summary>
    /// Disposes resources and completes the notification stream.
    /// </summary>
    public void Dispose()
    {
        _hashNotifications?.OnCompleted();
        _hashNotifications?.Dispose();
    }
}