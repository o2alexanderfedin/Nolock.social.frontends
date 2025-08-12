using System;

namespace NoLock.Social.Core.Storage
{
    public sealed class ContentReference
    {
        public string Hash { get; }
        public string? MimeType { get; }

        public ContentReference(string hash, string? mimeType = null)
        {
            if (string.IsNullOrWhiteSpace(hash))
                throw new ArgumentException("Hash cannot be null or empty", nameof(hash));

            Hash = hash;
            MimeType = mimeType;
        }

        public override bool Equals(object? obj)
        {
            return obj is ContentReference reference &&
                   Hash == reference.Hash &&
                   MimeType == reference.MimeType;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Hash, MimeType);
        }
    }
}