using System;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Core.Storage.Ipfs;
using Xunit;

namespace NoLock.Social.Core.Tests.Storage.Ipfs
{
    public class IpfsReadStreamTests : IAsyncDisposable
    {
        private readonly Mock<IIpfsJsInterop> _jsInteropMock;
        private readonly Mock<ILogger> _loggerMock;
        private readonly string _testPath = "/test/file.txt";

        public IpfsReadStreamTests()
        {
            _jsInteropMock = new Mock<IIpfsJsInterop>();
            _loggerMock = new Mock<ILogger>();
        }

        [Fact]
        public async Task ReadStream_Should_Read_Chunks_From_JavaScript()
        {
            // Arrange
            var testData = Encoding.UTF8.GetBytes("Hello IPFS World!");
            var fileSize = testData.Length;
            
            _jsInteropMock.Setup(x => x.GetFileSizeAsync(_testPath))
                .ReturnsAsync(fileSize);
            
            _jsInteropMock.Setup(x => x.ReadChunkAsync(_testPath, 0, It.IsAny<int>()))
                .ReturnsAsync(testData);
            
            var stream = new IpfsReadStream(_jsInteropMock.Object, _testPath, _loggerMock.Object);
            
            // Act
            var buffer = new byte[100];
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            
            // Assert
            bytesRead.Should().Be(testData.Length);
            var readData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            readData.Should().Be("Hello IPFS World!");
            
            _jsInteropMock.Verify(x => x.GetFileSizeAsync(_testPath), Times.Once);
            // Should read only the actual available bytes (17), not the requested 100
            _jsInteropMock.Verify(x => x.ReadChunkAsync(_testPath, 0, testData.Length), Times.Once);
        }

        [Fact]
        public async Task ReadStream_Should_Support_Seeking()
        {
            // Arrange
            var fullData = Encoding.UTF8.GetBytes("Hello IPFS World!");
            var fileSize = fullData.Length;
            
            _jsInteropMock.Setup(x => x.GetFileSizeAsync(_testPath))
                .ReturnsAsync(fileSize);
            
            // Setup to return different chunks based on offset
            _jsInteropMock.Setup(x => x.ReadChunkAsync(_testPath, 6, It.IsAny<int>()))
                .ReturnsAsync(() =>
                {
                    var chunk = new byte[11];
                    Array.Copy(fullData, 6, chunk, 0, 11);
                    return chunk;
                });
            
            var stream = new IpfsReadStream(_jsInteropMock.Object, _testPath, _loggerMock.Object);
            
            // Act - Seek to position 6
            stream.Seek(6, System.IO.SeekOrigin.Begin);
            
            // Read from the new position
            var buffer = new byte[11];
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            
            // Assert
            stream.Position.Should().Be(17); // 6 + 11
            bytesRead.Should().Be(11);
            var readData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            readData.Should().Be("IPFS World!");
            
            _jsInteropMock.Verify(x => x.ReadChunkAsync(_testPath, 6, 11), Times.Once);
        }

        [Fact]
        public async Task ReadStream_Should_Handle_Multiple_Sequential_Reads()
        {
            // Arrange
            var fullData = Encoding.UTF8.GetBytes("Hello IPFS World! This is a longer test string.");
            var fileSize = fullData.Length;
            
            _jsInteropMock.Setup(x => x.GetFileSizeAsync(_testPath))
                .ReturnsAsync(fileSize);
            
            // First read (0-10)
            _jsInteropMock.Setup(x => x.ReadChunkAsync(_testPath, 0, 10))
                .ReturnsAsync(() =>
                {
                    var chunk = new byte[10];
                    Array.Copy(fullData, 0, chunk, 0, 10);
                    return chunk;
                });
            
            // Second read (10-20)
            _jsInteropMock.Setup(x => x.ReadChunkAsync(_testPath, 10, 10))
                .ReturnsAsync(() =>
                {
                    var chunk = new byte[10];
                    Array.Copy(fullData, 10, chunk, 0, 10);
                    return chunk;
                });
            
            var stream = new IpfsReadStream(_jsInteropMock.Object, _testPath, _loggerMock.Object);
            
            // Act - First read
            var buffer1 = new byte[10];
            var bytesRead1 = await stream.ReadAsync(buffer1, 0, 10);
            
            // Act - Second read
            var buffer2 = new byte[10];
            var bytesRead2 = await stream.ReadAsync(buffer2, 0, 10);
            
            // Assert
            bytesRead1.Should().Be(10);
            bytesRead2.Should().Be(10);
            
            var data1 = Encoding.UTF8.GetString(buffer1);
            var data2 = Encoding.UTF8.GetString(buffer2);
            
            data1.Should().Be("Hello IPFS");
            data2.Should().Be(" World! Th");
            
            stream.Position.Should().Be(20);
            
            _jsInteropMock.Verify(x => x.ReadChunkAsync(_testPath, 0, 10), Times.Once);
            _jsInteropMock.Verify(x => x.ReadChunkAsync(_testPath, 10, 10), Times.Once);
        }

        [Fact]
        public async Task ReadStream_Should_Return_Zero_At_End_Of_File()
        {
            // Arrange
            var testData = Encoding.UTF8.GetBytes("Short");
            var fileSize = testData.Length;
            
            _jsInteropMock.Setup(x => x.GetFileSizeAsync(_testPath))
                .ReturnsAsync(fileSize);
            
            _jsInteropMock.Setup(x => x.ReadChunkAsync(_testPath, 0, It.IsAny<int>()))
                .ReturnsAsync(testData);
            
            _jsInteropMock.Setup(x => x.ReadChunkAsync(_testPath, 5, It.IsAny<int>()))
                .ReturnsAsync(Array.Empty<byte>());
            
            var stream = new IpfsReadStream(_jsInteropMock.Object, _testPath, _loggerMock.Object);
            
            // Act - Read all data
            var buffer1 = new byte[10];
            var bytesRead1 = await stream.ReadAsync(buffer1, 0, 10);
            
            // Act - Try to read past EOF
            var buffer2 = new byte[10];
            var bytesRead2 = await stream.ReadAsync(buffer2, 0, 10);
            
            // Assert
            bytesRead1.Should().Be(5);
            bytesRead2.Should().Be(0, "Should return 0 when at end of file");
            
            stream.Position.Should().Be(5);
        }

        public async ValueTask DisposeAsync()
        {
            await Task.CompletedTask;
        }
    }
}