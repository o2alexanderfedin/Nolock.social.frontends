// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace NoLock.Social.Core.Storage;

public sealed record ContentReference(string hash, string? mimeType)
{
    public string Hash { get; set; } = hash;
    public string? MimeType { get; set; } = mimeType;

    public ContentReference()
        : this(string.Empty, null)
    {
    }
}