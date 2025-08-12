using System;

namespace NoLock.Social.Core.Storage
{
    public class ContentMetadata
    {
        public string Hash { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastAccessedAt { get; set; }
        public string ContentType { get; set; } = "application/octet-stream";
        public int AccessCount { get; set; }
    }
}