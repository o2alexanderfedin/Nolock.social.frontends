using System;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace NoLock.Social.Core.Hashing
{
    public class Blake3HashAlgorithm : IHashAlgorithm
    {
        public string AlgorithmName => "BLAKE3";

        public byte[] ComputeHash(byte[] content)
        {
            // For now, fallback to SHA256 until BLAKE3 package is added
            // TODO: Add Blake3.NET package and implement proper BLAKE3
            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(content);
        }

        public ValueTask<byte[]> ComputeHashAsync(byte[] content)
        {
            return new ValueTask<byte[]>(ComputeHash(content));
        }
    }
}