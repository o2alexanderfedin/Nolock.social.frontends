using System.Threading.Tasks;

namespace NoLock.Social.Core.Storage.Signatures
{
    public interface IContentSigner
    {
        ValueTask<byte[]> SignHashAsync(string hash);
        string Algorithm { get; }
        string? PublicKeyId { get; }
    }
}