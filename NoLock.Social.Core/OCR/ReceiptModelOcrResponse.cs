namespace NoLock.Social.Core.OCR.Generated;

public partial class ReceiptModelOcrResponse
    : IModelOcrResponse
{
    public bool IsSuccess => ModelData is not null;
}

public interface IModelOcrResponse
{
    bool IsSuccess { get; }
    string? Error { get; set; }
}