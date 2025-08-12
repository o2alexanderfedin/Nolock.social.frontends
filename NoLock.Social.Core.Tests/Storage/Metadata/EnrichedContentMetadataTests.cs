using FluentAssertions;
using NoLock.Social.Core.Storage;
using NoLock.Social.Core.Storage.Metadata;
using NoLock.Social.Core.Storage.Signatures;
using System;
using System.Linq;

namespace NoLock.Social.Core.Tests.Storage.Metadata;

public class EnrichedContentMetadataTests
{
    [Fact]
    public void Constructor_WithContentMetadata_CreatesInstance()
    {
        // Arrange
        var contentMetadata = new ContentMetadata(new ContentReference("hash1", "image/png"));

        // Act
        var enriched = new EnrichedContentMetadata(contentMetadata);

        // Assert
        enriched.ContentMetadata.Should().Be(contentMetadata);
        enriched.Signatures.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithSignatures_CreatesInstance()
    {
        // Arrange
        var contentMetadata = new ContentMetadata(new ContentReference("hash1", "image/png"));
        var signatures = new[]
        {
            new SignedContent("hash1", new byte[] { 1, 2 }, "RSA"),
            new SignedContent("hash1", new byte[] { 3, 4 }, "ECDSA")
        };

        // Act
        var enriched = new EnrichedContentMetadata(contentMetadata, signatures);

        // Assert
        enriched.ContentMetadata.Should().Be(contentMetadata);
        enriched.Signatures.Should().HaveCount(2);
        enriched.Signatures.Should().BeEquivalentTo(signatures);
    }

    [Fact]
    public void Constructor_WithNullContentMetadata_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new EnrichedContentMetadata(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("contentMetadata");
    }

    [Fact]
    public void AddSignature_WithValidSignature_AddsToCollection()
    {
        // Arrange
        var contentMetadata = new ContentMetadata(new ContentReference("hash1", "text/plain"));
        var enriched = new EnrichedContentMetadata(contentMetadata);
        var signature = new SignedContent("hash1", new byte[] { 1, 2, 3 }, "RSA");

        // Act
        enriched.AddSignature(signature);

        // Assert
        enriched.Signatures.Should().HaveCount(1);
        enriched.Signatures[0].Should().Be(signature);
    }

    [Fact]
    public void AddSignature_WithNullSignature_ThrowsArgumentNullException()
    {
        // Arrange
        var contentMetadata = new ContentMetadata(new ContentReference("hash1"));
        var enriched = new EnrichedContentMetadata(contentMetadata);

        // Act
        var act = () => enriched.AddSignature(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("signature");
    }

    [Fact]
    public void AddSignature_WithSignatureForDifferentHash_ThrowsArgumentException()
    {
        // Arrange
        var contentMetadata = new ContentMetadata(new ContentReference("hash1", "image/jpeg"));
        var enriched = new EnrichedContentMetadata(contentMetadata);
        var signature = new SignedContent("different-hash", new byte[] { 1, 2 }, "RSA");

        // Act
        var act = () => enriched.AddSignature(signature);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Signature is for hash different-hash which is not in content metadata references");
    }

    [Fact]
    public void AddSignature_WithMultipleReferences_AcceptsMatchingSignatures()
    {
        // Arrange
        var references = new[]
        {
            new ContentReference("hash1", "image/png"),
            new ContentReference("hash2", "image/jpeg"),
            new ContentReference("hash3", "text/plain")
        };
        var contentMetadata = new ContentMetadata(references);
        var enriched = new EnrichedContentMetadata(contentMetadata);

        var signature1 = new SignedContent("hash1", new byte[] { 1 }, "RSA");
        var signature2 = new SignedContent("hash2", new byte[] { 2 }, "RSA");
        var signature3 = new SignedContent("hash3", new byte[] { 3 }, "RSA");

        // Act
        enriched.AddSignature(signature1);
        enriched.AddSignature(signature2);
        enriched.AddSignature(signature3);

        // Assert
        enriched.Signatures.Should().HaveCount(3);
        enriched.Signatures.Should().Contain(signature1);
        enriched.Signatures.Should().Contain(signature2);
        enriched.Signatures.Should().Contain(signature3);
    }

    [Fact]
    public void Signatures_IsReadOnly()
    {
        // Arrange
        var contentMetadata = new ContentMetadata(new ContentReference("hash"));
        var enriched = new EnrichedContentMetadata(contentMetadata);

        // Act & Assert
        enriched.Signatures.Should().BeAssignableTo<System.Collections.Generic.IReadOnlyList<SignedContent>>();
    }

    [Fact]
    public void Constructor_CreatesDefensiveCopyOfSignatures()
    {
        // Arrange
        var contentMetadata = new ContentMetadata(new ContentReference("hash1"));
        var signaturesList = new System.Collections.Generic.List<SignedContent>
        {
            new SignedContent("hash1", new byte[] { 1 }, "RSA")
        };
        var enriched = new EnrichedContentMetadata(contentMetadata, signaturesList);

        // Act - Modify original list
        signaturesList.Add(new SignedContent("hash1", new byte[] { 2 }, "ECDSA"));

        // Assert - EnrichedContentMetadata should not be affected
        enriched.Signatures.Should().HaveCount(1);
    }
}