using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NoLock.Social.Core.Cryptography.Interfaces;
using NoLock.Social.Core.Hashing;
using NoLock.Social.Core.Storage;
using NoLock.Social.Core.Storage.Interfaces;
using Xunit;

namespace NoLock.Social.Core.Tests.Storage
{
    public class StorageAdapterServiceTests
    {
        private readonly Mock<IContentAddressableStorage<byte[]>> _mockCAS;
        private readonly Mock<IHashAlgorithm> _mockHashAlgorithm;
        private readonly Mock<IVerificationService> _mockVerificationService;
        private readonly StorageAdapterService _storageAdapter;

        public StorageAdapterServiceTests()
        {
            _mockCAS = new Mock<IContentAddressableStorage<byte[]>>();
            _mockHashAlgorithm = new Mock<IHashAlgorithm>();
            _mockVerificationService = new Mock<IVerificationService>();
            _storageAdapter = new StorageAdapterService(
                _mockCAS.Object,
                _mockHashAlgorithm.Object,
                _mockVerificationService.Object
            );
        }

        #region StoreSignedContentAsync Tests

        [Fact]
        public async Task StoreSignedContentAsync_ShouldSerializeAndStoreContent()
        {
            // Arrange
            var signedContent = CreateTestSignedContent();
            var serializedData = Encoding.UTF8.GetBytes("serialized_content");
            var contentAddress = "sha256_hash_of_content";

            _mockHashAlgorithm
                .Setup(h => h.ComputeHashAsync(It.IsAny<byte[]>()))
                .ReturnsAsync(Encoding.UTF8.GetBytes(contentAddress));

            _mockCAS
                .Setup(c => c.StoreAsync(It.IsAny<byte[]>()))
                .ReturnsAsync(contentAddress);

            // Act
            var result = await _storageAdapter.StoreSignedContentAsync(signedContent);

            // Assert
            result.Should().NotBeNull();
            result.ContentAddress.Should().Be(contentAddress);
            result.Algorithm.Should().Be(signedContent.Algorithm);
            result.Version.Should().Be(signedContent.Version);
            result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            result.PublicKeyBase64.Should().Be(Convert.ToBase64String(signedContent.PublicKey));

            _mockCAS.Verify(c => c.StoreAsync(It.IsAny<byte[]>()), Times.Once);
        }

        [Fact]
        public async Task StoreSignedContentAsync_ShouldGenerateMetadata()
        {
            // Arrange
            var signedContent = CreateTestSignedContent();
            var contentAddress = "test_content_address";

            _mockHashAlgorithm
                .Setup(h => h.ComputeHashAsync(It.IsAny<byte[]>()))
                .ReturnsAsync(Encoding.UTF8.GetBytes(contentAddress));

            _mockCAS
                .Setup(c => c.StoreAsync(It.IsAny<byte[]>()))
                .ReturnsAsync(contentAddress);

            // Act
            var result = await _storageAdapter.StoreSignedContentAsync(signedContent);

            // Assert
            result.Should().NotBeNull();
            result.ContentAddress.Should().NotBeNullOrEmpty();
            result.Algorithm.Should().Be("Ed25519");
            result.Version.Should().Be("1.0");
            result.PublicKeyBase64.Should().NotBeNullOrEmpty();
            result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task StoreSignedContentAsync_ShouldThrowOnNullContent()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _storageAdapter.StoreSignedContentAsync(null!)
            );
        }

        #endregion

        #region RetrieveSignedContentAsync Tests

        [Fact]
        public async Task RetrieveSignedContentAsync_ShouldDeserializeAndVerifyContent()
        {
            // Arrange
            var contentAddress = "test_content_address";
            var signedContent = CreateTestSignedContent();
            var serializedContent = SerializeSignedContent(signedContent);

            _mockCAS
                .Setup(c => c.GetAsync(contentAddress))
                .ReturnsAsync(serializedContent);

            _mockVerificationService
                .Setup(v => v.VerifySignedContentAsync(It.IsAny<SignedContent>()))
                .ReturnsAsync(true);

            // Act
            var result = await _storageAdapter.RetrieveSignedContentAsync(contentAddress);

            // Assert
            result.Should().NotBeNull();
            result!.Content.Should().Be(signedContent.Content);
            result.Algorithm.Should().Be(signedContent.Algorithm);
            result.Version.Should().Be(signedContent.Version);
            result.PublicKey.Should().BeEquivalentTo(signedContent.PublicKey);
            result.Signature.Should().BeEquivalentTo(signedContent.Signature);

            _mockVerificationService.Verify(v => v.VerifySignedContentAsync(It.IsAny<SignedContent>()), Times.Once);
        }

        [Fact]
        public async Task RetrieveSignedContentAsync_ShouldReturnNullWhenContentNotFound()
        {
            // Arrange
            var contentAddress = "non_existent_address";

            _mockCAS
                .Setup(c => c.GetAsync(contentAddress))
                .ReturnsAsync((byte[]?)null);

            // Act
            var result = await _storageAdapter.RetrieveSignedContentAsync(contentAddress);

            // Assert
            result.Should().BeNull();
            _mockVerificationService.Verify(v => v.VerifySignedContentAsync(It.IsAny<SignedContent>()), Times.Never);
        }

        [Fact]
        public async Task RetrieveSignedContentAsync_ShouldThrowWhenVerificationFails()
        {
            // Arrange
            var contentAddress = "test_content_address";
            var signedContent = CreateTestSignedContent();
            var serializedContent = SerializeSignedContent(signedContent);

            _mockCAS
                .Setup(c => c.GetAsync(contentAddress))
                .ReturnsAsync(serializedContent);

            _mockVerificationService
                .Setup(v => v.VerifySignedContentAsync(It.IsAny<SignedContent>()))
                .ReturnsAsync(false);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<StorageVerificationException>(
                () => _storageAdapter.RetrieveSignedContentAsync(contentAddress)
            );

            exception.Message.Should().Contain("Signature verification failed");
            exception.ContentAddress.Should().Be(contentAddress);
        }

        [Fact]
        public async Task RetrieveSignedContentAsync_ShouldThrowOnInvalidContentAddress()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _storageAdapter.RetrieveSignedContentAsync("")
            );

            await Assert.ThrowsAsync<ArgumentException>(
                () => _storageAdapter.RetrieveSignedContentAsync(null!)
            );
        }

        #endregion

        #region GetStorageMetadataAsync Tests

        [Fact]
        public async Task GetStorageMetadataAsync_ShouldReturnMetadataWhenExists()
        {
            // Arrange
            var contentAddress = "test_content_address";
            var signedContent = CreateTestSignedContent();
            var serializedContent = SerializeSignedContent(signedContent);
            var contentSize = serializedContent.Length;

            _mockCAS
                .Setup(c => c.ExistsAsync(contentAddress))
                .ReturnsAsync(true);

            _mockCAS
                .Setup(c => c.GetSizeAsync(contentAddress))
                .ReturnsAsync(contentSize);

            _mockCAS
                .Setup(c => c.GetAsync(contentAddress))
                .ReturnsAsync(serializedContent);

            // Act
            var result = await _storageAdapter.GetStorageMetadataAsync(contentAddress);

            // Assert
            result.Should().NotBeNull();
            result!.ContentAddress.Should().Be(contentAddress);
            result.Size.Should().Be(contentSize);
            result.Algorithm.Should().Be(signedContent.Algorithm);
            result.Version.Should().Be(signedContent.Version);
            result.PublicKeyBase64.Should().Be(Convert.ToBase64String(signedContent.PublicKey));
        }

        [Fact]
        public async Task GetStorageMetadataAsync_ShouldReturnNullWhenNotExists()
        {
            // Arrange
            var contentAddress = "non_existent_address";

            _mockCAS
                .Setup(c => c.ExistsAsync(contentAddress))
                .ReturnsAsync(false);

            // Act
            var result = await _storageAdapter.GetStorageMetadataAsync(contentAddress);

            // Assert
            result.Should().BeNull();
            _mockCAS.Verify(c => c.GetSizeAsync(It.IsAny<string>()), Times.Never);
            _mockCAS.Verify(c => c.GetAsync(It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region DeleteContentAsync Tests

        [Fact]
        public async Task DeleteContentAsync_ShouldDeleteContent()
        {
            // Arrange
            var contentAddress = "test_content_address";

            _mockCAS
                .Setup(c => c.DeleteAsync(contentAddress))
                .ReturnsAsync(true);

            // Act
            var result = await _storageAdapter.DeleteContentAsync(contentAddress);

            // Assert
            result.Should().BeTrue();
            _mockCAS.Verify(c => c.DeleteAsync(contentAddress), Times.Once);
        }

        [Fact]
        public async Task DeleteContentAsync_ShouldReturnFalseWhenContentNotFound()
        {
            // Arrange
            var contentAddress = "non_existent_address";

            _mockCAS
                .Setup(c => c.DeleteAsync(contentAddress))
                .ReturnsAsync(false);

            // Act
            var result = await _storageAdapter.DeleteContentAsync(contentAddress);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region ListAllContentAsync Tests

        [Fact]
        public async Task ListAllContentAsync_ShouldReturnAllStoredContent()
        {
            // Arrange
            var hashes = new List<string> { "hash1", "hash2", "hash3" };
            var metadata1 = new StorageMetadata 
            { 
                ContentAddress = "hash1", 
                Size = 100,
                Timestamp = DateTime.UtcNow.AddMinutes(-10) 
            };
            var metadata2 = new StorageMetadata 
            { 
                ContentAddress = "hash2", 
                Size = 200,
                Timestamp = DateTime.UtcNow.AddMinutes(-5) 
            };
            var metadata3 = new StorageMetadata 
            { 
                ContentAddress = "hash3", 
                Size = 150,
                Timestamp = DateTime.UtcNow 
            };

            _mockCAS
                .Setup(c => c.GetAllHashesAsync())
                .Returns(ToAsyncEnumerable(hashes));

            // Setup metadata retrieval for each hash
            SetupMetadataRetrieval("hash1", metadata1);
            SetupMetadataRetrieval("hash2", metadata2);
            SetupMetadataRetrieval("hash3", metadata3);

            // Act
            var result = new List<StorageMetadata>();
            await foreach (var metadata in _storageAdapter.ListAllContentAsync())
            {
                result.Add(metadata);
            }

            // Assert
            result.Should().HaveCount(3);
            result.Should().BeInDescendingOrder(m => m.Timestamp);
            result[0].ContentAddress.Should().Be("hash3");
            result[1].ContentAddress.Should().Be("hash2");
            result[2].ContentAddress.Should().Be("hash1");
        }

        #endregion

        #region Helper Methods

        private SignedContent CreateTestSignedContent()
        {
            return new SignedContent
            {
                Content = "Test content",
                ContentHash = Encoding.UTF8.GetBytes("content_hash"),
                Signature = new byte[] { 1, 2, 3, 4 },
                PublicKey = new byte[] { 5, 6, 7, 8 },
                Algorithm = "Ed25519",
                Version = "1.0",
                Timestamp = DateTime.UtcNow
            };
        }

        private byte[] SerializeSignedContent(SignedContent content)
        {
            // Must match the format used in StorageAdapterService
            var serializableContent = new
            {
                content.Content,
                ContentHash = Convert.ToBase64String(content.ContentHash),
                Signature = Convert.ToBase64String(content.Signature),
                PublicKey = Convert.ToBase64String(content.PublicKey),
                content.Algorithm,
                content.Version,
                content.Timestamp
            };

            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            var json = System.Text.Json.JsonSerializer.Serialize(serializableContent, options);
            return Encoding.UTF8.GetBytes(json);
        }

        private void SetupMetadataRetrieval(string contentAddress, StorageMetadata metadata)
        {
            _mockCAS
                .Setup(c => c.ExistsAsync(contentAddress))
                .ReturnsAsync(true);

            _mockCAS
                .Setup(c => c.GetSizeAsync(contentAddress))
                .ReturnsAsync(metadata.Size);

            var signedContent = CreateTestSignedContent();
            signedContent.Timestamp = metadata.Timestamp;
            var serializedContent = SerializeSignedContent(signedContent);

            _mockCAS
                .Setup(c => c.GetAsync(contentAddress))
                .ReturnsAsync(serializedContent);
        }

        private async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                yield return item;
                await Task.CompletedTask;
            }
        }

        #endregion
    }
}