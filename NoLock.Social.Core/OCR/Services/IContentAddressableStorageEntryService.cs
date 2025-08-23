namespace NoLock.Social.Core.OCR.Services;

public interface IContentAddressableStorageEntryService<T>
{
    ValueTask<string> SubmitAsync(T value, string documentType);
}