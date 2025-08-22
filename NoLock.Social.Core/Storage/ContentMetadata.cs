// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
namespace NoLock.Social.Core.Storage;

public sealed record ContentMetadata(IEnumerable<ContentReference> references, DateTime? createdAt)
{
    public List<ContentReference> References { get; set; }
        = references is IEnumerable<ContentReference> ? references.ToList() : new List<ContentReference>();
    public DateTime CreatedAt { get; } = createdAt ?? DateTime.MaxValue;

    public ContentMetadata()
        : this([], null)
    {
    }
}