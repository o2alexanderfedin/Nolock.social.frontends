namespace NoLock.Social.Core.Hashing
{
    public interface IHashAlgorithm
    {
        byte[] ComputeHash(byte[] content);
        ValueTask<byte[]> ComputeHashAsync(byte[] content);
        string AlgorithmName { get; }
    }
}