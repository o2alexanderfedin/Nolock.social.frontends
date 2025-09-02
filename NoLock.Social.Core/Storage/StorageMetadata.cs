using System;
using System.Collections.Generic;

namespace NoLock.Social.Core.Storage
{
    public class StorageMetadata
    {
        public string Id { get; set; }
        public string ContentHash { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Dictionary<string, string> Tags { get; set; }
        public long Size { get; set; }
        public string ContentType { get; set; }
        public string Algorithm { get; set; }
        public DateTime Timestamp { get; set; }
        public string ContentAddress { get; set; }
    }
}