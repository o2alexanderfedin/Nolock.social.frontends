using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Services;

namespace NoLock.Social.Core.Tests.OCR.Services
{
    /// <summary>
    /// Unit tests for the DocumentProcessorRegistry class.
    /// Tests processor registration, discovery, and document processing.
    /// </summary>
    [TestClass]
    public class DocumentProcessorRegistryTests
    {
        private Mock<ILogger<DocumentProcessorRegistry>> _loggerMock;
        private DocumentProcessorRegistry _registry;
        private Mock<IDocumentProcessor> _processorMock;

        [TestInitialize]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<DocumentProcessorRegistry>>();
            _registry = new DocumentProcessorRegistry(_loggerMock.Object);
            _processorMock = new Mock<IDocumentProcessor>();
        }

        [TestMethod]
        public void Constructor_WithLogger_ShouldInitialize()
        {
            // Arrange & Act
            var registry = new DocumentProcessorRegistry(_loggerMock.Object);

            // Assert
            Assert.IsNotNull(registry);
            Assert.AreEqual(0, registry.ProcessorCount);
        }

        [TestMethod]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => new DocumentProcessorRegistry(null));
        }

        [TestMethod]
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
            Assert.AreEqual(2, registry.ProcessorCount);
            Assert.IsTrue(registry.IsProcessorRegistered("Type1"));
            Assert.IsTrue(registry.IsProcessorRegistered("Type2"));
        }

        [TestMethod]
        public void RegisterProcessor_WithValidProcessor_ShouldRegister()
        {
            // Arrange
            _processorMock.Setup(p => p.DocumentType).Returns("TestType");

            // Act
            _registry.RegisterProcessor(_processorMock.Object);

            // Assert
            Assert.AreEqual(1, _registry.ProcessorCount);
            Assert.IsTrue(_registry.IsProcessorRegistered("TestType"));
        }

        [TestMethod]
        public void RegisterProcessor_WithNullProcessor_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => _registry.RegisterProcessor(null));
        }

        [TestMethod]
        public void RegisterProcessor_WithEmptyDocumentType_ShouldThrowArgumentException()
        {
            // Arrange
            _processorMock.Setup(p => p.DocumentType).Returns(string.Empty);

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => _registry.RegisterProcessor(_processorMock.Object));
        }

        [TestMethod]
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
            Assert.AreEqual(1, _registry.ProcessorCount);
            Assert.AreSame(processor2.Object, _registry.GetProcessor("TestType"));
        }

        [TestMethod]
        public void UnregisterProcessor_WithExistingType_ShouldRemove()
        {
            // Arrange
            _processorMock.Setup(p => p.DocumentType).Returns("TestType");
            _registry.RegisterProcessor(_processorMock.Object);

            // Act
            var result = _registry.UnregisterProcessor("TestType");

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(0, _registry.ProcessorCount);
            Assert.IsFalse(_registry.IsProcessorRegistered("TestType"));
        }

        [TestMethod]
        public void UnregisterProcessor_WithNonExistingType_ShouldReturnFalse()
        {
            // Act
            var result = _registry.UnregisterProcessor("NonExisting");

            // Assert
            Assert.IsFalse(result);
        }

        [DataTestMethod]
        [DataRow(null, false)]
        [DataRow("", false)]
        [DataRow(" ", false)]
        public void UnregisterProcessor_WithInvalidType_ShouldReturnFalse(string documentType, bool expected)
        {
            // Act
            var result = _registry.UnregisterProcessor(documentType);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void GetProcessor_WithExistingType_ShouldReturnProcessor()
        {
            // Arrange
            _processorMock.Setup(p => p.DocumentType).Returns("TestType");
            _registry.RegisterProcessor(_processorMock.Object);

            // Act
            var result = _registry.GetProcessor("TestType");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreSame(_processorMock.Object, result);
        }

        [TestMethod]
        public void GetProcessor_WithCaseInsensitiveType_ShouldReturnProcessor()
        {
            // Arrange
            _processorMock.Setup(p => p.DocumentType).Returns("TestType");
            _registry.RegisterProcessor(_processorMock.Object);

            // Act
            var result = _registry.GetProcessor("TESTTYPE");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreSame(_processorMock.Object, result);
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow(" ")]
        [DataRow("NonExisting")]
        public void GetProcessor_WithInvalidType_ShouldReturnNull(string documentType)
        {
            // Act
            var result = _registry.GetProcessor(documentType);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void FindProcessorForData_WithMatchingProcessor_ShouldReturnProcessor()
        {
            // Arrange
            _processorMock.Setup(p => p.DocumentType).Returns("TestType");
            _processorMock.Setup(p => p.CanProcess(It.IsAny<string>())).Returns(true);
            _registry.RegisterProcessor(_processorMock.Object);

            // Act
            var result = _registry.FindProcessorForData("test data");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreSame(_processorMock.Object, result);
        }

        [TestMethod]
        public void FindProcessorForData_WithNoMatchingProcessor_ShouldReturnNull()
        {
            // Arrange
            _processorMock.Setup(p => p.DocumentType).Returns("TestType");
            _processorMock.Setup(p => p.CanProcess(It.IsAny<string>())).Returns(false);
            _registry.RegisterProcessor(_processorMock.Object);

            // Act
            var result = _registry.FindProcessorForData("test data");

            // Assert
            Assert.IsNull(result);
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow(" ")]
        public void FindProcessorForData_WithInvalidData_ShouldReturnNull(string rawData)
        {
            // Act
            var result = _registry.FindProcessorForData(rawData);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void FindProcessorForData_WhenProcessorThrows_ShouldContinueAndReturnNull()
        {
            // Arrange
            _processorMock.Setup(p => p.DocumentType).Returns("TestType");
            _processorMock.Setup(p => p.CanProcess(It.IsAny<string>())).Throws<Exception>();
            _registry.RegisterProcessor(_processorMock.Object);

            // Act
            var result = _registry.FindProcessorForData("test data");

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
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
            Assert.AreEqual(3, types.Count);
            Assert.AreEqual("TypeA", types[0]);
            Assert.AreEqual("TypeB", types[1]);
            Assert.AreEqual("TypeC", types[2]);
        }

        [TestMethod]
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
            Assert.AreEqual("Processed Result", result);
            _processorMock.Verify(p => p.ProcessAsync("test data", It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
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
            Assert.AreEqual("Processed Result", result);
        }

        [TestMethod]
        public async Task ProcessDocumentAsync_WithNoSuitableProcessor_ShouldThrowInvalidOperationException()
        {
            // Act & Assert
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => _registry.ProcessDocumentAsync("test data"));
        }

        [TestMethod]
        public async Task ProcessDocumentAsync_WithNullData_ShouldThrowArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => _registry.ProcessDocumentAsync(null));
        }

        [TestMethod]
        public async Task ProcessDocumentAsync_WhenProcessorThrows_ShouldWrapInInvalidOperationException()
        {
            // Arrange
            _processorMock.Setup(p => p.DocumentType).Returns("TestType");
            _processorMock.Setup(p => p.CanProcess(It.IsAny<string>())).Returns(true);
            _processorMock.Setup(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Processing failed"));
            _registry.RegisterProcessor(_processorMock.Object);

            // Act & Assert
            var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => _registry.ProcessDocumentAsync("test data", "TestType"));
            Assert.IsTrue(ex.Message.Contains("Error processing document"));
            Assert.IsNotNull(ex.InnerException);
        }

        [TestMethod]
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

        [DataTestMethod]
        [DataRow("TestType", true)]
        [DataRow("NonExisting", false)]
        [DataRow(null, false)]
        [DataRow("", false)]
        public void IsProcessorRegistered_ShouldReturnCorrectValue(string documentType, bool expected)
        {
            // Arrange
            _processorMock.Setup(p => p.DocumentType).Returns("TestType");
            _registry.RegisterProcessor(_processorMock.Object);

            // Act
            var result = _registry.IsProcessorRegistered(documentType);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ProcessorCount_ShouldReturnCorrectCount()
        {
            // Arrange
            var processor1 = new Mock<IDocumentProcessor>();
            processor1.Setup(p => p.DocumentType).Returns("Type1");
            
            var processor2 = new Mock<IDocumentProcessor>();
            processor2.Setup(p => p.DocumentType).Returns("Type2");

            // Act & Assert
            Assert.AreEqual(0, _registry.ProcessorCount);
            
            _registry.RegisterProcessor(processor1.Object);
            Assert.AreEqual(1, _registry.ProcessorCount);
            
            _registry.RegisterProcessor(processor2.Object);
            Assert.AreEqual(2, _registry.ProcessorCount);
            
            _registry.UnregisterProcessor("Type1");
            Assert.AreEqual(1, _registry.ProcessorCount);
        }
    }
}