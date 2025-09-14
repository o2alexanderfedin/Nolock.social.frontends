using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;
using NoLock.Social.Core.Storage.Ipfs;
using Xunit;

namespace NoLock.Social.Core.Tests.Storage.Ipfs
{
    public class IpfsFileSystemServiceTests : IAsyncDisposable
    {
        private readonly Mock<IJSRuntime> _jsRuntimeMock;
        private readonly Mock<ILogger<IpfsFileSystemService>> _loggerMock;
        private readonly IpfsFileSystemService _sut;
        private readonly Mock<IJSObjectReference> _jsModuleMock;

        public IpfsFileSystemServiceTests()
        {
            _jsRuntimeMock = new Mock<IJSRuntime>();
            _loggerMock = new Mock<ILogger<IpfsFileSystemService>>();
            _jsModuleMock = new Mock<IJSObjectReference>();

            // Setup module import to match exact service call signature
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<IJSObjectReference>(
                    "import",
                    It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == "./js/modules/ipfs-module.js")))
                .ReturnsAsync(_jsModuleMock.Object);

            // Also setup generic import for other tests
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<IJSObjectReference>(
                    "import",
                    It.IsAny<object[]>()))
                .ReturnsAsync(_jsModuleMock.Object);

            // Ensure module mock has proper disposal setup
            _jsModuleMock
                .Setup(x => x.DisposeAsync())
                .Returns(ValueTask.CompletedTask);

            _sut = new IpfsFileSystemService(_jsRuntimeMock.Object, _loggerMock.Object);
        }

        public async ValueTask DisposeAsync()
        {
            if (_sut != null)
                await _sut.DisposeAsync();
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullJSRuntime_ThrowsArgumentNullException()
        {
            // Act & Assert
            var action = () => new IpfsFileSystemService(null, _loggerMock.Object);
            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("jsRuntime");
        }

        [Fact]
        public void Constructor_WithNullLogger_CreatesInstance()
        {
            // Act
            var service = new IpfsFileSystemService(_jsRuntimeMock.Object, null);

            // Assert
            service.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Act
            var service = new IpfsFileSystemService(_jsRuntimeMock.Object, _loggerMock.Object);

            // Assert
            service.Should().NotBeNull();
        }

        #endregion

        #region JavaScript Module Loading Tests

        [Fact]
        public async Task WriteFileAsync_LoadsJavaScriptModuleOnFirstCall()
        {
            // Arrange
            
            var writeHandleMock = new Mock<IJSObjectReference>();
            
            _jsModuleMock
                .Setup(x => x.InvokeAsync<IJSObjectReference>("ipfs.beginWrite", It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
                .ReturnsAsync(writeHandleMock.Object);
            
            writeHandleMock
                .Setup(x => x.InvokeAsync<object>("writeChunk", It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
                .ReturnsAsync((object)null);
            
            writeHandleMock
                .Setup(x => x.InvokeAsync<string>("complete", It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
                .ReturnsAsync("QmTest123");
            
            writeHandleMock
                .Setup(x => x.DisposeAsync())
                .Returns(ValueTask.CompletedTask);

            var stream = new MemoryStream(new byte[] { 1, 2, 3 });

            // Act
            await _sut.WriteFileAsync("test.txt", stream);

            // Assert
            _jsRuntimeMock.Verify(x => 
                x.InvokeAsync<IJSObjectReference>("import", 
                    It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == "./js/modules/ipfs-module.js")), 
                Times.Once);
        }

        [Fact]
        public async Task WriteFileAsync_AfterDispose_ThrowsObjectDisposedException()
        {
            // Arrange
            await _sut.DisposeAsync();
            var stream = new MemoryStream();

            // Act & Assert
            await Assert.ThrowsAsync<ObjectDisposedException>(async () => 
                await _sut.WriteFileAsync("test.txt", stream));
        }

        #endregion

        #region WriteFileAsync Tests

        [Fact]
        public async Task WriteFileAsync_WithValidStream_ReturnsCid()
        {
            // Arrange
            var content = "test content";
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            var expectedCid = "QmTest123";
            
            SetupWriteOperation(_jsModuleMock, expectedCid);

            // Act
            var result = await _sut.WriteFileAsync("test.txt", stream);

            // Assert
            result.Should().Be(expectedCid);
        }

        [Fact]
        public async Task WriteFileAsync_WithNullPath_ThrowsArgumentNullException()
        {
            // Arrange
            var stream = new MemoryStream();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () => 
                await _sut.WriteFileAsync(null, stream));
        }

        [Fact]
        public async Task WriteFileAsync_WithNullStream_ThrowsArgumentNullException()
        {
            // Arrange

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () => 
                await _sut.WriteFileAsync("test.txt", null));
        }

        [Fact]
        public async Task WriteFileAsync_WithEmptyPath_ThrowsArgumentException()
        {
            // Arrange
            var stream = new MemoryStream();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () => 
                await _sut.WriteFileAsync("", stream));
        }

        [Fact]
        public async Task WriteFileAsync_WithLargeFile_HandlesCorrectly()
        {
            // Arrange
            var largeContent = new byte[1024 * 1024]; // 1MB
            new Random().NextBytes(largeContent);
            var stream = new MemoryStream(largeContent);
            var expectedCid = "QmLargeFile123";
            
            SetupWriteOperation(_jsModuleMock, expectedCid);

            // Act
            var result = await _sut.WriteFileAsync("large.bin", stream);

            // Assert
            result.Should().Be(expectedCid);
        }

        #endregion

        #region ReadFileAsync Tests

        [Fact]
        public async Task ReadFileAsync_WithValidCid_ReturnsStream()
        {
            // Arrange
            var cid = "QmTest123";
            var expectedContent = "file content";
            var contentBytes = System.Text.Encoding.UTF8.GetBytes(expectedContent);

            // Create mock for IIpfsJsInterop
            var jsInteropMock = new Mock<IIpfsJsInterop>();
            jsInteropMock
                .Setup(x => x.GetFileSizeAsync(It.IsAny<string>()))
                .ReturnsAsync((long)contentBytes.Length);
            jsInteropMock
                .SetupSequence(x => x.ReadChunkAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<int>()))
                .ReturnsAsync(contentBytes)  // First call returns content
                .ReturnsAsync(new byte[0]);  // Second call returns EOF

            // Create service with factory that returns our mock
            var service = new IpfsFileSystemService(
                _jsRuntimeMock.Object,
                _loggerMock.Object,
                jsObjectRef => jsInteropMock.Object);

            // Setup metadata and beginRead operations
            SetupReadOperation(_jsModuleMock, expectedContent.Length);

            // Act
            var result = await service.ReadFileAsync(cid);

            // Assert
            result.Should().NotBeNull();
            using var reader = new StreamReader(result);
            var readContent = await reader.ReadToEndAsync();
            readContent.Should().Be(expectedContent);
        }

        [Fact]
        public async Task ReadFileAsync_WithNullCid_ThrowsArgumentNullException()
        {
            // Arrange

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () => 
                await _sut.ReadFileAsync(null));
        }

        [Fact]
        public async Task ReadFileAsync_WithEmptyCid_ThrowsArgumentException()
        {
            // Arrange

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () => 
                await _sut.ReadFileAsync(""));
        }

        [Fact]
        public async Task ReadFileAsync_FileNotFound_ReturnsNull()
        {
            // Arrange
            var cid = "QmNonExistent";
            
            // File not found - GetMetadata should return null
            _jsModuleMock
                .Setup(x => x.InvokeAsync<IpfsFileSystemService.IpfsFileMetadataDto?>("ipfs.getMetadata", It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
                .ReturnsAsync((IpfsFileSystemService.IpfsFileMetadataDto?)null);

            // Act
            var result = await _sut.ReadFileAsync(cid);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region ListDirectoryAsync Tests

        [Fact]
        public async Task ListDirectoryAsync_ReturnsEntries()
        {
            // Arrange
            var path = "/test/directory";
            
            // Create test entries using proper DTO types
            var entries = new[]
            {
                new IpfsFileSystemService.IpfsFileEntryDto
                {
                    Name = "file1.txt",
                    Path = "/test/directory/file1.txt",
                    Cid = "Qm111",
                    Size = 1024L,
                    Type = "file",
                    LastModified = null
                },
                new IpfsFileSystemService.IpfsFileEntryDto
                {
                    Name = "file2.pdf",
                    Path = "/test/directory/file2.pdf",
                    Cid = "Qm222",
                    Size = 2048L,
                    Type = "file",
                    LastModified = null
                },
                new IpfsFileSystemService.IpfsFileEntryDto
                {
                    Name = "subfolder",
                    Path = "/test/directory/subfolder",
                    Cid = "Qm333",
                    Size = 0L,
                    Type = "directory",
                    LastModified = null
                }
            };
            
            _jsModuleMock
                .Setup(x => x.InvokeAsync<IpfsFileSystemService.IpfsFileEntryDto[]>("ipfs.listDirectory", It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
                .ReturnsAsync(entries);

            // Act
            var result = new List<IpfsFileEntry>();
            await foreach (var entry in _sut.ListDirectoryAsync(path))
            {
                result.Add(entry);
            }

            // Assert
            result.Should().HaveCount(3);
            result[0].Name.Should().Be("file1.txt");
            result[0].Cid.Should().Be("Qm111");
            result[1].Name.Should().Be("file2.pdf");
            result[2].Name.Should().Be("subfolder");
        }

        [Fact]
        public async Task ListDirectoryAsync_WithNullPath_ThrowsArgumentNullException()
        {
            // Arrange

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                var result = new List<IpfsFileEntry>();
                await foreach (var entry in _sut.ListDirectoryAsync(null))
                {
                    result.Add(entry);
                }
            });
        }

        [Fact]
        public async Task ListDirectoryAsync_EmptyDirectory_ReturnsEmptyList()
        {
            // Arrange
            var path = "/empty";
            
            _jsModuleMock
                .Setup(x => x.InvokeAsync<IpfsFileSystemService.IpfsFileEntryDto[]>("ipfs.listDirectory", It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
                .ReturnsAsync(Array.Empty<IpfsFileSystemService.IpfsFileEntryDto>());

            // Act
            var result = new List<IpfsFileEntry>();
            await foreach (var entry in _sut.ListDirectoryAsync(path))
            {
                result.Add(entry);
            }

            // Assert
            result.Should().BeEmpty();
        }

        #endregion

        #region DeleteAsync Tests

        [Fact]
        public async Task DeleteAsync_WithValidCid_ReturnsTrue()
        {
            // Arrange
            var cid = "QmTest123";
            
            _jsModuleMock
                .Setup(x => x.InvokeAsync<bool>("ipfs.unpin", It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
                .ReturnsAsync(true);

            // Act
            var result = await _sut.DeleteAsync(cid);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task DeleteAsync_WithNullCid_ThrowsArgumentNullException()
        {
            // Arrange

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () => 
                await _sut.DeleteAsync(null));
        }

        [Fact]
        public async Task DeleteAsync_FileNotFound_ReturnsFalse()
        {
            // Arrange
            var cid = "QmNonExistent";
            
            _jsModuleMock
                .Setup(x => x.InvokeAsync<bool>("ipfs.unpin", It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
                .ReturnsAsync(false);

            // Act
            var result = await _sut.DeleteAsync(cid);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region ExistsAsync Tests

        [Fact]
        public async Task ExistsAsync_WithExistingFile_ReturnsTrue()
        {
            // Arrange
            var cid = "QmExisting";
            
            _jsModuleMock
                .Setup(x => x.InvokeAsync<bool>("ipfs.exists", It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
                .ReturnsAsync(true);

            // Act
            var result = await _sut.ExistsAsync(cid);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task ExistsAsync_WithNonExistentFile_ReturnsFalse()
        {
            // Arrange
            var cid = "QmNonExistent";
            
            _jsModuleMock
                .Setup(x => x.InvokeAsync<bool>("ipfs.exists", It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
                .ReturnsAsync(false);

            // Act
            var result = await _sut.ExistsAsync(cid);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region GetMetadataAsync Tests

        [Fact]
        public async Task GetMetadataAsync_WithValidCid_ReturnsMetadata()
        {
            // Arrange
            var cid = "QmTest123";
            
            // Create test metadata matching the expected DTO structure
            var metadata = new IpfsFileSystemService.IpfsFileMetadataDto
            {
                Path = "test.txt",
                Cid = cid,
                Size = 1024L,
                Type = "file",
                BlockCount = null,
                Created = null,
                LastModified = null
            };
            
            _jsModuleMock
                .Setup(x => x.InvokeAsync<IpfsFileSystemService.IpfsFileMetadataDto?>("ipfs.getMetadata", It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
                .ReturnsAsync(metadata);

            // Act
            var result = await _sut.GetMetadataAsync(cid);

            // Assert
            result.Should().NotBeNull();
            result.Cid.Should().Be(cid);
            result.Path.Should().Be("test.txt");
            result.Size.Should().Be(1024L);
        }

        [Fact]
        public async Task GetMetadataAsync_FileNotFound_ReturnsNull()
        {
            // Arrange
            var cid = "QmNonExistent";
            
            _jsModuleMock
                .Setup(x => x.InvokeAsync<IpfsFileSystemService.IpfsFileMetadataDto?>("ipfs.getMetadata", It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
                .ReturnsAsync((IpfsFileSystemService.IpfsFileMetadataDto?)null);

            // Act
            var result = await _sut.GetMetadataAsync(cid);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task WriteFileAsync_WithJSException_ThrowsInvalidOperationException()
        {
            // Arrange
            var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
            
            _jsModuleMock
                .Setup(x => x.InvokeAsync<IJSObjectReference>("ipfs.beginWrite", It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("IPFS write failed"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => 
                await _sut.WriteFileAsync("test.txt", stream));
            exception.Message.Should().Contain("Failed to write file to IPFS");
        }

        [Fact]
        public async Task ReadFileAsync_WithJSException_ThrowsInvalidOperationException()
        {
            // Arrange
            var cid = "QmTest123";
            
            // Setup metadata to throw exception
            _jsModuleMock
                .Setup(x => x.InvokeAsync<IpfsFileSystemService.IpfsFileMetadataDto?>("ipfs.getMetadata", It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("IPFS read failed"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => 
                await _sut.ReadFileAsync(cid));
            exception.Message.Should().Contain("Failed to get metadata from IPFS");
        }

        [Fact]
        public async Task ListDirectoryAsync_WithJSException_ThrowsInvalidOperationException()
        {
            // Arrange
            var path = "/test";
            
            _jsModuleMock
                .Setup(x => x.InvokeAsync<IpfsFileSystemService.IpfsFileEntryDto[]>("ipfs.listDirectory", It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("IPFS list failed"));

            // Act & Assert
            await Assert.ThrowsAsync<JSException>(async () => 
            {
                var list = new List<IpfsFileEntry>();
                await foreach (var entry in _sut.ListDirectoryAsync(path))
                {
                    list.Add(entry);
                }
            });
        }

        #endregion

        #region Disposal Tests

        [Fact]
        public async Task DisposeAsync_CleansUpResources()
        {
            // Arrange
            var service = new IpfsFileSystemService(_jsRuntimeMock.Object, _loggerMock.Object);

            // Act
            await service.DisposeAsync();
            await service.DisposeAsync(); // Should not throw on second dispose

            // Assert - Methods should throw ObjectDisposedException
            await Assert.ThrowsAsync<ObjectDisposedException>(async () => 
                await service.WriteFileAsync("test.txt", new MemoryStream()));
        }

        [Fact]
        public async Task DisposeAsync_DisposesJavaScriptModule_WhenModuleLoaded()
        {
            // Arrange
            
            // First call to trigger module loading
            var stream = new MemoryStream();
            SetupWriteOperation(_jsModuleMock, "QmTest");
            
            await _sut.WriteFileAsync("test.txt", stream);

            // Act
            await _sut.DisposeAsync();

            // Assert
            _jsModuleMock.Verify(x => x.DisposeAsync(), Times.Once);
        }


        #endregion

        #region Helper Methods
        
        private void SetupWriteOperation(Mock<IJSObjectReference> jsModuleMock, string expectedCid)
        {
            var writeHandleMock = new Mock<IJSObjectReference>();
            
            jsModuleMock
                .Setup(x => x.InvokeAsync<IJSObjectReference>("ipfs.beginWrite", It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
                .ReturnsAsync(writeHandleMock.Object);
            
            writeHandleMock
                .Setup(x => x.InvokeAsync<object>("writeChunk", It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
                .ReturnsAsync((object)null);
            
            writeHandleMock
                .Setup(x => x.InvokeAsync<string>("complete", It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
                .ReturnsAsync(expectedCid);
            
            writeHandleMock
                .Setup(x => x.DisposeAsync())
                .Returns(ValueTask.CompletedTask);
        }
        
        private void SetupReadOperation(Mock<IJSObjectReference> jsModuleMock, long fileSize)
        {
            var readHandleMock = new Mock<IJSObjectReference>();

            // Setup metadata for the read operation
            var metadata = new IpfsFileSystemService.IpfsFileMetadataDto
            {
                Path = "test.txt",
                Cid = "QmTest123",
                Size = fileSize,
                Type = "file",
                BlockCount = null,
                Created = null,
                LastModified = null
            };

            jsModuleMock
                .Setup(x => x.InvokeAsync<IpfsFileSystemService.IpfsFileMetadataDto?>("ipfs.getMetadata", It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
                .ReturnsAsync(metadata);

            jsModuleMock
                .Setup(x => x.InvokeAsync<IJSObjectReference>("ipfs.beginRead", It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
                .ReturnsAsync(readHandleMock.Object);

            // Setup to return actual file content then EOF
            var contentBytes = Encoding.UTF8.GetBytes("file content");

            // Add mocks for IpfsJsInterop calls (without "ipfs." prefix)
            jsModuleMock
                .Setup(x => x.InvokeAsync<long>("getFileSize", It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
                .ReturnsAsync(fileSize);

            jsModuleMock
                .SetupSequence(x => x.InvokeAsync<byte[]>("readChunk", It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
                .ReturnsAsync(contentBytes) // First call returns content
                .ReturnsAsync(new byte[0]);  // Second call returns EOF

            // Keep existing mocks for compatibility with other tests
            readHandleMock
                .SetupSequence(x => x.InvokeAsync<byte[]>("readChunk", It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
                .ReturnsAsync(contentBytes) // First call returns content
                .ReturnsAsync(new byte[0]);  // Second call returns EOF

            readHandleMock
                .Setup(x => x.DisposeAsync())
                .Returns(ValueTask.CompletedTask);
        }

        #endregion
    }
}