using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using Moq;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Services;

namespace NoLock.Social.Core.Tests.OCR.Services
{
    /// <summary>
    /// Unit tests for the DocumentProcessorRegistry class.
    /// Tests processor registration, discovery, and document processing.
    /// </summary>
    public class DocumentProcessorRegistryTests
    {
        private Mock<ILogger<DocumentProcessorRegistry>> _loggerMock;
        private DocumentProcessorRegistry _registry;
        private Mock<IDocumentProcessor> _processorMock;

        public DocumentProcessorRegistryTests()
        {
            _loggerMock = new Mock<ILogger<DocumentProcessorRegistry>>();
            _registry = new DocumentProcessorRegistry(_loggerMock.Object);
            _processorMock = new Mock<IDocumentProcessor>();
        }

        [Fact]
        public void Constructor_WithLogger_ShouldInitialize()
        {
            // Arrange & Act
            var registry = new DocumentProcessorRegistry(_loggerMock.Object);

            // Assert
            Assert.NotNull(registry);
            Assert.Equal(0, registry.ProcessorCount);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DocumentProcessorRegistry(null));
        }

        [Fact]
        public void Constructor_WithProcessors_ShouldRegisterAll()
        {
            // Arrange
            var processor1 = new Mock<IDocumentProcessor>();
            processor1.Setup(p => p.DocumentType).Returns("Type1");
            
            var processor2 = new Mock<IDocumentProcessor>();
            processor2.Setup(p => p.DocumentType).Returns("Type2");

            var processors = new[] { processor1.Object, processor2.Object };

            // Act
            var registry = new DocumentProcessorRegistry(_loggerMock.Object, processors);

            // Assert
            Assert.Equal(2, registry.ProcessorCount);
            Assert.True(registry.IsProcessorRegistered("Type1"));
            Assert.True(registry.IsProcessorRegistered("Type2"));
        }

        [Fact]
        public void RegisterProcessor_WithValidProcessor_ShouldRegister()
        {
            // Arrange
            _processorMock.Setup(p => p.DocumentType).Returns("TestType");

            // Act
            _registry.RegisterProcessor(_processorMock.Object);

            // Assert
            Assert.Equal(1, _registry.ProcessorCount);
            Assert.True(_registry.IsProcessorRegistered("TestType"));
        }

        [Fact]
        public void RegisterProcessor_WithNullProcessor_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _registry.RegisterProcessor(null));
        }

        [Fact]
        public void RegisterProcessor_WithEmptyDocumentType_ShouldThrowArgumentException()
        {
            // Arrange
            _processorMock.Setup(p => p.DocumentType).Returns(string.Empty);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _registry.RegisterProcessor(_processorMock.Object));
        }

        [Fact]
        public void RegisterProcessor_WithDuplicateType_ShouldUpdateExisting()
        {
            // Arrange
            var processor1 = new Mock<IDocumentProcessor>();
            processor1.Setup(p => p.DocumentType).Returns("TestType");
            
            var processor2 = new Mock<IDocumentProcessor>();
            processor2.Setup(p => p.DocumentType).Returns("TestType");

            // Act
            _registry.RegisterProcessor(processor1.Object);
            _registry.RegisterProcessor(processor2.Object);

            // Assert
            Assert.Equal(1, _registry.ProcessorCount);
            Assert.Same(processor2.Object, _registry.GetProcessor("TestType"));
        }

        [Fact]
        public void UnregisterProcessor_WithExistingType_ShouldRemove()
        {
            // Arrange
            _processorMock.Setup(p => p.DocumentType).Returns("TestType");
            _registry.RegisterProcessor(_processorMock.Object);

            // Act
            var result = _registry.UnregisterProcessor("TestType");

            // Assert
            Assert.True(result);
            Assert.Equal(0, _registry.ProcessorCount);
            Assert.False(_registry.IsProcessorRegistered("TestType"));
        }

        [Fact]
        public void UnregisterProcessor_WithNonExistingType_ShouldReturnFalse()
        {
            // Act
            var result = _registry.UnregisterProcessor("NonExisting");

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData(" ", false)]
        public void UnregisterProcessor_WithInvalidType_ShouldReturnFalse(string processorId, bool expected)
        {
            // Act
            var result = _registry.UnregisterProcessor(processorId);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void GetProcessor_WithExistingType_ShouldReturnProcessor()
        {
            // Arrange
            _processorMock.Setup(p => p.DocumentType).Returns("TestType");
            _registry.RegisterProcessor(_processorMock.Object);

            // Act
            var result = _registry.GetProcessor("TestType");

            // Assert
            Assert.NotNull(result);
            Assert.Same(_processorMock.Object, result);
        }

        [Fact]
        public void GetProcessor_WithCaseInsensitiveType_ShouldReturnProcessor()
        {
            // Arrange
            _processorMock.Setup(p => p.DocumentType).Returns("TestType");
            _registry.RegisterProcessor(_processorMock.Object);

            // Act
            var result = _registry.GetProcessor("TESTTYPE");

            // Assert
            Assert.NotNull(result);
            Assert.Same(_processorMock.Object, result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("NonExisting")]
        public void GetProcessor_WithInvalidType_ShouldReturnNull(string processorId)
        {
            // Act
            var result = _registry.GetProcessor(processorId);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task FindProcessorForData_WithMatchingProcessor_ShouldReturnProcessor()
        {
            // Arrange
            _processorMock.Setup(p => p.DocumentType).Returns("TestType");
            _processorMock.Setup(p => p.CanProcess(It.IsAny<string>())).Returns(true);
            _registry.RegisterProcessor(_processorMock.Object);

            // Act
            var result = await _registry.FindProcessorForDataAsync("test data");

            // Assert
            Assert.NotNull(result);
            Assert.Same(_processorMock.Object, result);
        }

        [Fact]
        public async Task FindProcessorForData_WithNoMatchingProcessor_ShouldReturnNull()
        {
            // Arrange
            _processorMock.Setup(p => p.DocumentType).Returns("TestType");
            _processorMock.Setup(p => p.CanProcess(It.IsAny<string>())).Returns(false);
            _registry.RegisterProcessor(_processorMock.Object);

            // Act
            var result = await _registry.FindProcessorForDataAsync("test data");

            // Assert
            Assert.Null(result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public async Task FindProcessorForData_WithInvalidData_ShouldReturnNull(string rawData)
        {
            // Act
            var result = await _registry.FindProcessorForDataAsync(rawData);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task FindProcessorForData_WhenProcessorThrows_ShouldContinueAndReturnNull()
        {
            // Arrange
            _processorMock.Setup(p => p.DocumentType).Returns("TestType");
            _processorMock.Setup(p => p.CanProcess(It.IsAny<string>())).Throws<Exception>();
            _registry.RegisterProcessor(_processorMock.Object);

            // Act
            var result = await _registry.FindProcessorForDataAsync("test data");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetRegisteredTypes_ShouldReturnSortedTypes()
        {
            // Arrange
            var processor1 = new Mock<IDocumentProcessor>();
            processor1.Setup(p => p.DocumentType).Returns("TypeB");
            
            var processor2 = new Mock<IDocumentProcessor>();
            processor2.Setup(p => p.DocumentType).Returns("TypeA");
            
            var processor3 = new Mock<IDocumentProcessor>();
            processor3.Setup(p => p.DocumentType).Returns("TypeC");

            _registry.RegisterProcessor(processor1.Object);
            _registry.RegisterProcessor(processor2.Object);
            _registry.RegisterProcessor(processor3.Object);

            // Act
            var types = _registry.GetRegisteredTypes().ToList();

            // Assert
            Assert.Equal(3, types.Count);
            Assert.Equal("TypeA", types[0]);
            Assert.Equal("TypeB", types[1]);
            Assert.Equal("TypeC", types[2]);
        }

        [Fact]
        public async Task ProcessDocumentAsync_WithSpecificType_ShouldUseCorrectProcessor()
        {
            // Arrange
            _processorMock.Setup(p => p.DocumentType).Returns("TestType");
            _processorMock.Setup(p => p.CanProcess(It.IsAny<string>())).Returns(true);
            _processorMock.Setup(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("Processed Result");
            _registry.RegisterProcessor(_processorMock.Object);

            // Act
            var result = await _registry.ProcessDocumentAsync("test data", "TestType");

            // Assert
            Assert.Equal("Processed Result", result);
            _processorMock.Verify(p => p.ProcessAsync("test data", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ProcessDocumentAsync_WithAutoDetection_ShouldFindAndUseProcessor()
        {
            // Arrange
            _processorMock.Setup(p => p.DocumentType).Returns("TestType");
            _processorMock.Setup(p => p.CanProcess("test data")).Returns(true);
            _processorMock.Setup(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("Processed Result");
            _registry.RegisterProcessor(_processorMock.Object);

            // Act
            var result = await _registry.ProcessDocumentAsync("test data");

            // Assert
            Assert.Equal("Processed Result", result);
        }

        [Fact]
        public async Task ProcessDocumentAsync_WithNoSuitableProcessor_ShouldThrowInvalidOperationException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _registry.ProcessDocumentAsync("test data"));
        }

        [Fact]
        public async Task ProcessDocumentAsync_WithNullData_ShouldThrowArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _registry.ProcessDocumentAsync(null));
        }

        [Fact]
        public async Task ProcessDocumentAsync_WhenProcessorThrows_ShouldWrapInInvalidOperationException()
        {
            // Arrange
            _processorMock.Setup(p => p.DocumentType).Returns("TestType");
            _processorMock.Setup(p => p.CanProcess(It.IsAny<string>())).Returns(true);
            _processorMock.Setup(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Processing failed"));
            _registry.RegisterProcessor(_processorMock.Object);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _registry.ProcessDocumentAsync("test data", "TestType"));
            Assert.Contains("Error processing document", ex.Message);
            Assert.NotNull(ex.InnerException);
        }

        [Fact]
        public async Task ProcessDocumentAsync_WithCancellation_ShouldPassTokenToProcessor()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            _processorMock.Setup(p => p.DocumentType).Returns("TestType");
            _processorMock.Setup(p => p.CanProcess(It.IsAny<string>())).Returns(true);
            _processorMock.Setup(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("Result");
            _registry.RegisterProcessor(_processorMock.Object);

            // Act
            await _registry.ProcessDocumentAsync("test data", "TestType", cts.Token);

            // Assert
            _processorMock.Verify(p => p.ProcessAsync("test data", cts.Token), Times.Once);
        }

        [Theory]
        [InlineData("TestType", true)]
        [InlineData("NonExisting", false)]
        [InlineData(null, false)]
        [InlineData("", false)]
        public void IsProcessorRegistered_ShouldReturnCorrectValue(string processorId, bool expected)
        {
            // Arrange
            _processorMock.Setup(p => p.DocumentType).Returns("TestType");
            _registry.RegisterProcessor(_processorMock.Object);

            // Act
            var result = _registry.IsProcessorRegistered(processorId);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ProcessorCount_ShouldReturnCorrectCount()
        {
            // Arrange
            var processor1 = new Mock<IDocumentProcessor>();
            processor1.Setup(p => p.DocumentType).Returns("Type1");
            
            var processor2 = new Mock<IDocumentProcessor>();
            processor2.Setup(p => p.DocumentType).Returns("Type2");

            // Act & Assert
            Assert.Equal(0, _registry.ProcessorCount);
            
            _registry.RegisterProcessor(processor1.Object);
            Assert.Equal(1, _registry.ProcessorCount);
            
            _registry.RegisterProcessor(processor2.Object);
            Assert.Equal(2, _registry.ProcessorCount);
            
            _registry.UnregisterProcessor("Type1");
            Assert.Equal(1, _registry.ProcessorCount);
        }
    }
}