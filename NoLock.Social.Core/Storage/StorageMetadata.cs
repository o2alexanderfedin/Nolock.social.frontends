using System;
using System.Collections.Generic;

namespace NoLock.Social.Core.Storage
{
    public class StorageMetadata
    {
        public required string Id { get; init; } = null!;
        public required string ContentHash { get; init; } = null!;  
        public required DateTime CreatedAt { get; init; }
        public required DateTime UpdatedAt { get; init; }
        public Dictionary<string, string>? Tags { get; init; } = null;
        public required long Size { get; init; } = 0;
        public required string ContentType { get; init; } = null!;
        public required string Algorithm { get; init; } = null!;
        public required DateTime Timestamp { get; init; }
        public required string ContentAddress { get; init; }
    }
}