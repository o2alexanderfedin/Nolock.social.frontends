using System;
using System.Collections.Generic;
using System.Linq;

namespace NoLock.Social.Core.Storage
{
    public sealed class ContentMetadata
    {
        private readonly List<ContentReference> _references;
        
        public IReadOnlyList<ContentReference> References => _references.AsReadOnly();
        public DateTime CreatedAt { get; }

        public ContentMetadata(IEnumerable<ContentReference> references, DateTime? createdAt = null)
        {
            if (references == null)
                throw new ArgumentNullException(nameof(references));

            _references = references.ToList();
            
            if (_references.Count == 0)
                throw new ArgumentException("At least one reference is required", nameof(references));

            CreatedAt = createdAt ?? DateTime.UtcNow;
        }

        public ContentMetadata(ContentReference reference, DateTime? createdAt = null)
            : this(new[] { reference ?? throw new ArgumentNullException(nameof(reference)) }, createdAt)
        {
        }
    }
}