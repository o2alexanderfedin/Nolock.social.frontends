using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using NoLock.Social.Core.Storage.Ipfs;
using Xunit;

namespace NoLock.Social.Core.Tests.Storage.Ipfs
{
    /// <summary>
    /// Integration tests for IpfsJsInterop verifying JavaScript wrapper interaction.
    /// Tests the integration between C# IpfsJsInterop and ipfs-mfs.js module.
    /// </summary>
    public class IpfsJsInteropIntegrationTests : IAsyncDisposable
    {
        private readonly Mock<IJSObjectReference> _jsModuleMock;
        private readonly IpfsJsInterop _jsInterop;

        public IpfsJsInteropIntegrationTests()
        {
            _jsModuleMock = new Mock<IJSObjectReference>();
            _jsInterop = new IpfsJsInterop(_jsModuleMock.Object);
        }

        /// <summary>
        /// Verifies that writeBytes JavaScript function is called through the wrapper.
        /// Note: Direct verification of InvokeVoidAsync calls is not possible due to extension method limitations.
        /// This test verifies the wrapper behavior and exception handling.
        /// </summary>
        [Fact]
        public async Task WriteDataAsync_Should_Not_Throw_When_Module_Is_Valid()
        {
            // Arrange
            var testPath = "/test/document.pdf";
            var testData = Encoding.UTF8.GetBytes("Test content");

            // Act & Assert - should not throw when module is valid
            var exception = await Record.ExceptionAsync(async () =>
                await _jsInterop.WriteDataAsync(testPath, testData));

            exception.Should().BeNull("no exception should be thrown with valid module");
        }

        /// <summary>
        /// Verifies that AppendDataAsync doesn't throw when module is valid.
        /// Note: Direct verification of InvokeVoidAsync calls is not possible due to extension method limitations.
        /// </summary>
        [Fact]
        public async Task AppendDataAsync_Should_Not_Throw_When_Module_Is_Valid()
        {
            // Arrange
            var testPath = "/test/log.txt";
            var appendData = Encoding.UTF8.GetBytes("Appended content");

            // Act & Assert - should not throw when module is valid
            var exception = await Record.ExceptionAsync(async () =>
                await _jsInterop.AppendDataAsync(testPath, appendData));

            exception.Should().BeNull("no exception should be thrown with valid module");
        }

        /// <summary>
        /// Verifies that ReadChunkAsync doesn't throw when module is valid.
        /// Note: Direct verification of InvokeAsync calls is not possible due to extension method limitations.
        /// In real integration tests, this would return actual data from JavaScript.
        /// </summary>
        [Fact]
        public async Task ReadChunkAsync_Should_Not_Throw_When_Module_Is_Valid()
        {
            // Arrange
            var testPath = "/test/video.mp4";
            long offset = 1024;
            int length = 4096;

            // Act & Assert - should not throw when module is valid
            // In actual integration, this would return data from JavaScript
            var exception = await Record.ExceptionAsync(async () =>
                await _jsInterop.ReadChunkAsync(testPath, offset, length));

            exception.Should().BeNull("no exception should be thrown with valid module");
        }

        /// <summary>
        /// Verifies that GetFileSizeAsync doesn't throw when module is valid.
        /// Note: Direct verification of InvokeAsync calls is not possible due to extension method limitations.
        /// In real integration tests, this would return actual file size from JavaScript.
        /// </summary>
        [Fact]
        public async Task GetFileSizeAsync_Should_Not_Throw_When_Module_Is_Valid()
        {
            // Arrange
            var testPath = "/test/large-file.bin";

            // Act & Assert - should not throw when module is valid
            // In actual integration, this would return size from JavaScript
            var exception = await Record.ExceptionAsync(async () =>
                await _jsInterop.GetFileSizeAsync(testPath));

            exception.Should().BeNull("no exception should be thrown with valid module");
        }

        /// <summary>
        /// Verifies that operations throw ObjectDisposedException after disposal.
        /// </summary>
        [Fact]
        public async Task Operations_Should_Throw_ObjectDisposedException_After_Disposal()
        {
            // Arrange
            await _jsInterop.DisposeAsync();
            var testPath = "/test/file.txt";
            var testData = new byte[] { 1, 2, 3 };

            // Act & Assert
            await Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await _jsInterop.WriteDataAsync(testPath, testData));

            await Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await _jsInterop.AppendDataAsync(testPath, testData));

            await Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await _jsInterop.ReadChunkAsync(testPath, 0, 100));

            await Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await _jsInterop.GetFileSizeAsync(testPath));
        }

        /// <summary>
        /// Verifies that constructor throws ArgumentNullException for null module.
        /// </summary>
        [Fact]
        public void Constructor_Should_Throw_ArgumentNullException_For_Null_Module()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new IpfsJsInterop(null));
        }

        public async ValueTask DisposeAsync()
        {
            await _jsInterop.DisposeAsync();
        }
    }
}