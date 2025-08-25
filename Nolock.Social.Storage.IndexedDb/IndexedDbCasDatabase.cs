using CloudNimble.BlazorEssentials.IndexedDb;
using Microsoft.JSInterop;

namespace Nolock.Social.Storage.IndexedDb;

public sealed class IndexedDbCasDatabase : IndexedDbDatabase
{
    [ObjectStore(Name = "CasEntries", AutoIncrementKeys = false)]
    public IndexedDbObjectStore CasEntries { get; init; } = null!;

    public IndexedDbCasDatabase(IJSRuntime jsRuntime)
        : base(jsRuntime)
    {
        Version = 1;
    }
}