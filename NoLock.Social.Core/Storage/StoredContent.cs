using System;

namespace NoLock.Social.Core.Storage
{
    internal class StoredContent
    {
        public string Hash { get; set; } = string.Empty;
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public ContentMetadata Metadata { get; set; } = new ContentMetadata();
    }
}