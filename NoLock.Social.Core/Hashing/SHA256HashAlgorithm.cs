using System.Security.Cryptography;

namespace NoLock.Social.Core.Hashing
{
    public class SHA256HashAlgorithm : IHashAlgorithm
    {
        public string AlgorithmName => "SHA256";

        public byte[] ComputeHash(byte[] content)
        {
            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(content);
        }

        public ValueTask<byte[]> ComputeHashAsync(byte[] content)
        {
            return new ValueTask<byte[]>(ComputeHash(content));
        }
    }
}