using FluentAssertions;
using NoLock.Social.Core.Storage;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NoLock.Social.Core.Tests.Storage;

public class ContentMetadataTests
{
    [Fact]
    public void Constructor_WithSingleReference_CreatesInstance()
    {
        // Arrange
        var reference = new ContentReference("hash123", "image/jpeg");
        var createdAt = DateTime.UtcNow;

        // Act
        var metadata = new ContentMetadata(reference, createdAt);

        // Assert
        metadata.References.Should().HaveCount(1);
        metadata.References[0].Should().Be(reference);
        metadata.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void Constructor_WithMultipleReferences_CreatesInstance()
    {
        // Arrange
        var references = new[]
        {
            new ContentReference("hash1", "image/jpeg"),
            new ContentReference("hash2", "text/plain"),
            new ContentReference("hash3", "application/json")
        };
        var createdAt = DateTime.UtcNow;

        // Act
        var metadata = new ContentMetadata(references, createdAt);

        // Assert
        metadata.References.Should().HaveCount(3);
        metadata.References.Should().BeEquivalentTo(references);
        metadata.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void Constructor_WithoutCreatedAt_UsesCurrentTime()
    {
        // Arrange
        var reference = new ContentReference("hash");
        var beforeCreation = DateTime.UtcNow;

        // Act
        var metadata = new ContentMetadata(reference);
        var afterCreation = DateTime.UtcNow;

        // Assert
        metadata.CreatedAt.Should().BeOnOrAfter(beforeCreation).And.BeOnOrBefore(afterCreation);
    }

    [Fact]
    public void Constructor_WithNullReference_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ContentMetadata(reference: null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("reference");
    }

    [Fact]
    public void Constructor_WithNullReferences_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ContentMetadata(references: null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("references");
    }

    [Fact]
    public void Constructor_WithEmptyReferences_ThrowsArgumentException()
    {
        // Arrange
        var emptyReferences = new List<ContentReference>();

        // Act
        var act = () => new ContentMetadata(emptyReferences);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("At least one reference is required*")
            .WithParameterName("references");
    }

    [Fact]
    public void References_IsReadOnly()
    {
        // Arrange
        var reference = new ContentReference("hash");
        var metadata = new ContentMetadata(reference);

        // Act & Assert
        metadata.References.Should().BeAssignableTo<IReadOnlyList<ContentReference>>();
        metadata.GetType().GetProperty("References")!.CanWrite.Should().BeFalse();
    }

    [Fact]
    public void CreatedAt_IsImmutable()
    {
        // Arrange
        var reference = new ContentReference("hash");
        var metadata = new ContentMetadata(reference);

        // Act & Assert
        metadata.GetType().GetProperty("CreatedAt")!.CanWrite.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithReferencesContainingNullMimeType_CreatesInstance()
    {
        // Arrange
        var references = new[]
        {
            new ContentReference("hash1", null),
            new ContentReference("hash2", "text/plain")
        };

        // Act
        var metadata = new ContentMetadata(references);

        // Assert
        metadata.References[0].MimeType.Should().BeNull();
        metadata.References[1].MimeType.Should().Be("text/plain");
    }

    [Fact]
    public void Constructor_WithMixedMimeTypes_StoresCorrectly()
    {
        // Arrange
        var references = new[]
        {
            new ContentReference("hash1", "image/png"),
            new ContentReference("hash2", "image/jpeg"),
            new ContentReference("hash3", "video/mp4"),
            new ContentReference("hash4", "audio/mpeg")
        };

        // Act
        var metadata = new ContentMetadata(references);

        // Assert
        metadata.References.Should().HaveCount(4);
        metadata.References.Select(r => r.MimeType).Should().BeEquivalentTo(
            "image/png", "image/jpeg", "video/mp4", "audio/mpeg");
    }

    [Fact]
    public void Constructor_WithDuplicateHashes_AllowsIt()
    {
        // Arrange - Same hash with different mime types (e.g., same content served as different formats)
        var references = new[]
        {
            new ContentReference("samehash", "image/png"),
            new ContentReference("samehash", "image/webp")
        };

        // Act
        var metadata = new ContentMetadata(references);

        // Assert
        metadata.References.Should().HaveCount(2);
        metadata.References.Should().BeEquivalentTo(references);
    }

    [Fact]
    public void Constructor_CreatesDefensiveCopy()
    {
        // Arrange
        var referencesList = new List<ContentReference>
        {
            new ContentReference("hash1", "text/plain")
        };
        var metadata = new ContentMetadata(referencesList);

        // Act - Modify original list
        referencesList.Add(new ContentReference("hash2", "image/png"));

        // Assert - Metadata should not be affected
        metadata.References.Should().HaveCount(1);
    }

    [Fact]
    public void Constructor_WithSpecificCreatedAt_UsesProvidedTime()
    {
        // Arrange
        var reference = new ContentReference("hash");
        var specificTime = new DateTime(2024, 1, 15, 10, 30, 45, DateTimeKind.Utc);

        // Act
        var metadata = new ContentMetadata(reference, specificTime);

        // Assert
        metadata.CreatedAt.Should().Be(specificTime);
    }
}