using FluentAssertions;
using NoLock.Social.Core.Cryptography.Services;

namespace NoLock.Social.Core.Tests.Cryptography
{
    public class SecureMemoryManagerTests : IDisposable
    {
        private readonly SecureMemoryManager _sut;

        public SecureMemoryManagerTests()
        {
            _sut = new SecureMemoryManager();
        }

        public void Dispose()
        {
            _sut?.Dispose();
        }

        [Fact]
        public void CreateSecureBuffer_WithSize_CreatesBufferOfCorrectSize()
        {
            // Act
            using var buffer = _sut.CreateSecureBuffer(32);

            // Assert
            buffer.Should().NotBeNull();
            buffer.Size.Should().Be(32);
            buffer.Data.Should().HaveCount(32);
            buffer.IsCleared.Should().BeFalse();
        }

        [Fact]
        public void CreateSecureBuffer_WithData_CopiesDataAndClearsOriginal()
        {
            // Arrange
            var originalData = new byte[] { 1, 2, 3, 4, 5 };
            var dataCopy = originalData.ToArray();

            // Act
            using var buffer = _sut.CreateSecureBuffer(originalData);

            // Assert
            buffer.Should().NotBeNull();
            buffer.Size.Should().Be(5);
            buffer.Data.Should().BeEquivalentTo(dataCopy);
            originalData.Should().AllBeEquivalentTo((byte)0); // Original should be cleared
        }

        [Fact]
        public void SecureBuffer_Clear_ZeroesData()
        {
            // Arrange
            var data = new byte[] { 1, 2, 3, 4, 5 };
            using var buffer = _sut.CreateSecureBuffer(data);

            // Act
            buffer.Clear();

            // Assert
            buffer.IsCleared.Should().BeTrue();
            buffer.Data.Should().AllBeEquivalentTo((byte)0);
        }

        [Fact]
        public void SecureBuffer_Dispose_ClearsData()
        {
            // Arrange
            var data = new byte[] { 1, 2, 3, 4, 5 };
            var buffer = _sut.CreateSecureBuffer(data);

            // Act
            buffer.Dispose();

            // Assert
            buffer.IsCleared.Should().BeTrue();
            // Cannot access Data after disposal - it should throw
        }

        [Fact]
        public void SecureBuffer_AsSpan_ProvidesReadOnlyView()
        {
            // Arrange
            var data = new byte[] { 1, 2, 3, 4, 5 };
            using var buffer = _sut.CreateSecureBuffer(data);

            // Act
            var span = buffer.AsSpan();

            // Assert
            span.Length.Should().Be(5);
            span.ToArray().Should().BeEquivalentTo(new byte[] { 1, 2, 3, 4, 5 });
        }

        [Fact]
        public void SecureBuffer_CopyTo_CopiesDataCorrectly()
        {
            // Arrange
            var sourceData = new byte[] { 1, 2, 3, 4, 5 };
            var expectedData = new byte[] { 1, 2, 3, 4, 5 }; // Keep a copy since sourceData will be cleared
            using var source = _sut.CreateSecureBuffer(sourceData);
            using var destination = _sut.CreateSecureBuffer(5);

            // Act
            source.CopyTo(destination);

            // Assert
            destination.Data.Should().BeEquivalentTo(expectedData);
        }

        [Fact]
        public void SecureBuffer_CopyTo_ThrowsIfDestinationTooSmall()
        {
            // Arrange
            var sourceData = new byte[] { 1, 2, 3, 4, 5 };
            using var source = _sut.CreateSecureBuffer(sourceData);
            using var destination = _sut.CreateSecureBuffer(3);

            // Act & Assert
            var act = () => source.CopyTo(destination);
            act.Should().Throw<ArgumentException>()
                .WithMessage("*too small*");
        }

        [Fact]
        public void ClearAllBuffers_ClearsAllTrackedBuffers()
        {
            // Arrange
            var buffer1 = _sut.CreateSecureBuffer(new byte[] { 1, 2, 3 });
            var buffer2 = _sut.CreateSecureBuffer(new byte[] { 4, 5, 6 });
            var buffer3 = _sut.CreateSecureBuffer(new byte[] { 7, 8, 9 });

            // Act
            _sut.ClearAllBuffers();

            // Assert
            buffer1.IsCleared.Should().BeTrue();
            buffer2.IsCleared.Should().BeTrue();
            buffer3.IsCleared.Should().BeTrue();
            buffer1.Data.Should().AllBeEquivalentTo((byte)0);
            buffer2.Data.Should().AllBeEquivalentTo((byte)0);
            buffer3.Data.Should().AllBeEquivalentTo((byte)0);
        }

        [Fact]
        public void SecureBuffer_MultipleClears_DoesNotThrow()
        {
            // Arrange
            using var buffer = _sut.CreateSecureBuffer(10);

            // Act & Assert
            var act = () =>
            {
                buffer.Clear();
                buffer.Clear(); // Second clear should not throw
            };

            act.Should().NotThrow();
            buffer.IsCleared.Should().BeTrue();
        }

        [Fact]
        public void SecureBuffer_AccessAfterDispose_ThrowsObjectDisposedException()
        {
            // Arrange
            var buffer = _sut.CreateSecureBuffer(10);
            buffer.Dispose();

            // Act & Assert
            var act = () => _ = buffer.Data;
            act.Should().Throw<ObjectDisposedException>();
        }

        [Fact]
        public void SecureMemoryManager_Dispose_ClearsAllBuffers()
        {
            // Arrange
            var manager = new SecureMemoryManager();
            var buffer1 = manager.CreateSecureBuffer(new byte[] { 1, 2, 3 });
            var buffer2 = manager.CreateSecureBuffer(new byte[] { 4, 5, 6 });

            // Act
            manager.Dispose();

            // Assert
            buffer1.IsCleared.Should().BeTrue();
            buffer2.IsCleared.Should().BeTrue();
        }
    }
}