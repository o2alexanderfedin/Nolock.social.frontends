using System;
using System.Collections.Generic;
using System.Linq;
using NoLock.Social.Core.Storage.Signatures;

namespace NoLock.Social.Core.Storage.Metadata
{
    public sealed class EnrichedContentMetadata
    {
        private readonly List<SignedContent> _signatures;

        public ContentMetadata ContentMetadata { get; }
        public IReadOnlyList<SignedContent> Signatures => _signatures.AsReadOnly();

        public EnrichedContentMetadata(ContentMetadata contentMetadata, IEnumerable<SignedContent>? signatures = null)
        {
            ContentMetadata = contentMetadata ?? throw new ArgumentNullException(nameof(contentMetadata));
            _signatures = signatures?.ToList() ?? new List<SignedContent>();
        }

        public void AddSignature(SignedContent signature)
        {
            if (signature == null)
                throw new ArgumentNullException(nameof(signature));

            // Verify the signature is for the right content
            if (!ContentMetadata.References.Any(r => r.Hash == signature.ContentHash))
                throw new ArgumentException($"Signature is for hash {signature.ContentHash} which is not in content metadata references");

            _signatures.Add(signature);
        }
    }
}