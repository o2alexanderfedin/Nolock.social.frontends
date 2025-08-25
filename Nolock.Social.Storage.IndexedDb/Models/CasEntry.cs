// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Nolock.Social.Storage.IndexedDb.Models;

public sealed class CasEntry<T>
{
    public required string Hash { get; init; }
    public required T Data { get; init; }
    public required string TypeName { get; init; }
    public DateTime StoredAt { get; init; } = DateTime.UtcNow;
}