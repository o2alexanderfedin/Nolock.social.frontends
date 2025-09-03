// ReSharper disable UnusedAutoPropertyAccessor.Global
using System.Text.Json.Serialization;

namespace Nolock.Social.Storage.IndexedDb.Models;

public sealed class CasEntry<T>
{
    [JsonPropertyName("hash")]
    public required string Hash { get; init; }
    
    [JsonPropertyName("data")]
    public required T? Data { get; init; }
    
    [JsonPropertyName("typeName")]
    public required string TypeName { get; init; }
    
    [JsonPropertyName("storedAt")]
    public DateTime StoredAt { get; init; } = DateTime.UtcNow;
}