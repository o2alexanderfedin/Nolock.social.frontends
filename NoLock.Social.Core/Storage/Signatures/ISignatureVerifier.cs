using System.Threading.Tasks;

namespace NoLock.Social.Core.Storage.Signatures
{
    public interface ISignatureVerifier
    {
        ValueTask<bool> VerifyHashSignatureAsync(string hash, byte[] signature, string? publicKeyId = null);
        string Algorithm { get; }
    }
}