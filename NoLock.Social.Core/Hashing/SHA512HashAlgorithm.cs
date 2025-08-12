using System;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace NoLock.Social.Core.Hashing
{
    public class SHA512HashAlgorithm : IHashAlgorithm
    {
        public string AlgorithmName => "SHA512";

        public byte[] ComputeHash(byte[] content)
        {
            using var sha512 = SHA512.Create();
            return sha512.ComputeHash(content);
        }

        public ValueTask<byte[]> ComputeHashAsync(byte[] content)
        {
            return new ValueTask<byte[]>(ComputeHash(content));
        }
    }
}