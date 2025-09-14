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
    public class IpfsWriteStreamTests : IAsyncDisposable
    {
        private readonly Mock<IIpfsJsInterop> _jsInteropMock;
        private readonly Mock<ILogger> _loggerMock;
        private readonly string _testPath = "/test/file.txt";

        public IpfsWriteStreamTests()
        {
            _jsInteropMock = new Mock<IIpfsJsInterop>();
            _loggerMock = new Mock<ILogger>();
        }

        [Fact]
        public async Task WriteStream_Should_Buffer_Data()
        {
            // Arrange
            var stream = new IpfsWriteStream(_jsInteropMock.Object, _testPath, _loggerMock.Object);
            var testData = Encoding.UTF8.GetBytes("Hello IPFS");

            // Act
            await stream.WriteAsync(testData, 0, testData.Length);

            // Assert
            stream.Position.Should().Be(testData.Length);
            
            // Verify that JS interop was NOT called (data should be buffered)
            _jsInteropMock.Verify(
                x => x.AppendDataAsync(It.IsAny<string>(), It.IsAny<byte[]>()),
                Times.Never);
        }

        [Fact]
        public async Task WriteStream_Should_AutoFlush_When_Buffer_Exceeds_256KB()
        {
            // Arrange
            var stream = new IpfsWriteStream(_jsInteropMock.Object, _testPath, _loggerMock.Object);
            const int bufferThreshold = 256 * 1024; // 256KB
            const int chunkSize = 64 * 1024; // 64KB chunks
            var largeData = new byte[bufferThreshold + chunkSize]; // Just over threshold
            
            // Fill with test data
            for (int i = 0; i < largeData.Length; i++)
            {
                largeData[i] = (byte)(i % 256);
            }

            // Act - Write data that exceeds buffer threshold
            await stream.WriteAsync(largeData, 0, largeData.Length);

            // Assert
            stream.Position.Should().Be(largeData.Length);
            
            // Verify that JS interop WAS called due to auto-flush
            _jsInteropMock.Verify(
                x => x.AppendDataAsync(
                    _testPath,
                    It.Is<byte[]>(data => data.Length == bufferThreshold)),
                Times.Once,
                "Should auto-flush exactly 256KB when threshold is exceeded");
            
            // Verify remaining data stays in buffer (64KB that went over threshold)
            // This would be verified by checking internal buffer state
            // but since we're in RED phase, this will fail until implementation
        }

        [Fact]
        public async Task WriteStream_Should_Flush_Remaining_Data_On_Dispose()
        {
            // Arrange
            var stream = new IpfsWriteStream(_jsInteropMock.Object, _testPath, _loggerMock.Object);
            var testData = Encoding.UTF8.GetBytes("Important data that must be saved");
            
            // Act - Write data that doesn't trigger auto-flush (less than 256KB)
            await stream.WriteAsync(testData, 0, testData.Length);
            
            // Verify no flush yet (data is buffered)
            _jsInteropMock.Verify(
                x => x.AppendDataAsync(It.IsAny<string>(), It.IsAny<byte[]>()),
                Times.Never,
                "Data should be buffered before dispose");
            
            // Act - Dispose the stream
            await stream.DisposeAsync();
            
            // Assert - Verify that buffered data was flushed on dispose
            _jsInteropMock.Verify(
                x => x.AppendDataAsync(
                    _testPath,
                    It.Is<byte[]>(data => 
                        data.Length == testData.Length &&
                        Encoding.UTF8.GetString(data) == "Important data that must be saved")),
                Times.Once,
                "Should flush all remaining buffered data on dispose");
            
            // Verify stream is marked as disposed
            stream.CanWrite.Should().BeFalse("Stream should not be writable after disposal");
        }

        public async ValueTask DisposeAsync()
        {
            await Task.CompletedTask;
        }
    }
}