namespace NoLock.Social.Core.Storage;

public sealed record ContentData<T>
{
    public T Data { get; init; }
    public string? MimeType { get; init; }

    public ContentData(T data, string? mimeType = null)
    {
        Data = data;
        MimeType = mimeType;
    }

    public ContentData()
        : this(default!, null)
    {
    }
}