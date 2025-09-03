using CloudNimble.BlazorEssentials.IndexedDb;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Nolock.Social.Storage.IndexedDb;

public sealed class IndexedDbCasDatabase : IndexedDbDatabase
{
    private readonly ILogger _logger;
    
    [ObjectStore(Name = "CasEntries", AutoIncrementKeys = false, KeyPath = "hash")]
    public IndexedDbObjectStore CasEntries { get; init; } = null!;

    public IndexedDbCasDatabase(IJSRuntime jsRuntime, ILogger logger)
        : base(jsRuntime)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Version = 1;
        
        _logger.LogInformation("IndexedDbCasDatabase initialized with Version={Version}, KeyPath='hash', AutoIncrementKeys=false", Version);
    }
    
    // Add logging wrapper method for EnsureIsOpenAsync
    public new async Task EnsureIsOpenAsync()
    {
        _logger.LogDebug("Ensuring IndexedDB database is open");
        try
        {
            await base.EnsureIsOpenAsync();
            _logger.LogDebug("IndexedDB database is open and ready");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure IndexedDB database is open");
            throw;
        }
    }
}