using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Models;
using NoLock.Social.Core.OCR.Services;

namespace NoLock.Social.Core.Tests.OCR.Services
{
    /// <summary>
    /// Unit tests for the enhanced plugin registry management features of DocumentProcessorRegistry.
    /// Tests metadata management, priority-based selection, and dynamic enable/disable functionality.
    /// </summary>
    [TestClass]
    public class DocumentProcessorRegistryPluginTests
    {
        private Mock<ILogger<DocumentProcessorRegistry>> _loggerMock;
        private DocumentProcessorRegistry _registry;
        private Mock<IDocumentProcessor> _receiptProcessorMock;
        private Mock<IDocumentProcessor> _checkProcessorMock;
        private Mock<IDocumentProcessor> _w4ProcessorMock;

        [TestInitialize]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<DocumentProcessorRegistry>>();
            _registry = new DocumentProcessorRegistry(_loggerMock.Object);
            
            // Setup mock processors
            _receiptProcessorMock = new Mock<IDocumentProcessor>();
            _receiptProcessorMock.Setup(p => p.DocumentType).Returns("Receipt");
            _receiptProcessorMock.Setup(p => p.CanProcess(It.IsAny<string>())).Returns(false);
            
            _checkProcessorMock = new Mock<IDocumentProcessor>();
            _checkProcessorMock.Setup(p => p.DocumentType).Returns("Check");
            _checkProcessorMock.Setup(p => p.CanProcess(It.IsAny<string>())).Returns(false);
            
            _w4ProcessorMock = new Mock<IDocumentProcessor>();
            _w4ProcessorMock.Setup(p => p.DocumentType).Returns("W4");
            _w4ProcessorMock.Setup(p => p.CanProcess(It.IsAny<string>())).Returns(false);
        }

        #region Metadata Registration Tests

        [TestMethod]
        public void RegisterProcessor_WithMetadata_ShouldStoreMetadata()
        {
            // Arrange
            var metadata = new ProcessorMetadata
            {
                DisplayName = "Test Processor",
                Description = "Test Description",
                Version = new Version(1, 2, 3),
                Priority = 100,
                Capabilities = new[] { "capability1", "capability2" },
                SupportedExtensions = new[] { ".pdf", ".jpg" }
            };

            // Act
            _registry.RegisterProcessor(_receiptProcessorMock.Object, metadata);
            var info = _registry.GetProcessorInfo("Receipt");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Test Processor", info.DisplayName);
            Assert.AreEqual("Test Description", info.Description);
            Assert.AreEqual(new Version(1, 2, 3), info.Version);
            Assert.AreEqual(100, info.Priority);
            Assert.IsTrue(info.IsEnabled);
            CollectionAssert.AreEquivalent(new[] { "capability1", "capability2" }, info.Capabilities.ToArray());
            CollectionAssert.AreEquivalent(new[] { ".pdf", ".jpg" }, info.SupportedExtensions.ToArray());
        }

        [TestMethod]
        public void RegisterProcessor_WithDetailedMetadata_ShouldStoreAllDetails()
        {
            // Act
            _registry.RegisterProcessor(
                _receiptProcessorMock.Object,
                "Receipt Scanner",
                "Scans and processes receipts",
                new Version(2, 0, 0),
                priority: 150,
                capabilities: new[] { "ocr", "extraction" },
                supportedExtensions: new[] { ".png", ".tiff" }
            );

            var info = _registry.GetProcessorInfo("Receipt");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Receipt Scanner", info.DisplayName);
            Assert.AreEqual("Scans and processes receipts", info.Description);
            Assert.AreEqual(new Version(2, 0, 0), info.Version);
            Assert.AreEqual(150, info.Priority);
            Assert.AreEqual(2, info.Capabilities.Count);
            Assert.AreEqual(2, info.SupportedExtensions.Count);
        }

        [TestMethod]
        public void RegisterProcessor_WithoutMetadata_ShouldUseDefaults()
        {
            // Act
            _registry.RegisterProcessor(_receiptProcessorMock.Object);
            var info = _registry.GetProcessorInfo("Receipt");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Receipt", info.DisplayName);
            Assert.IsTrue(info.Description.Contains("Receipt"));
            Assert.AreEqual(new Version(1, 0, 0), info.Version);
            Assert.AreEqual(0, info.Priority);
            Assert.IsTrue(info.IsEnabled);
        }

        #endregion

        #region Enable/Disable Tests

        [TestMethod]
        public void SetProcessorEnabled_DisableProcessor_ShouldDisable()
        {
            // Arrange
            _registry.RegisterProcessor(_receiptProcessorMock.Object);

            // Act
            var result = _registry.SetProcessorEnabled("Receipt", false);
            var processor = _registry.GetProcessor("Receipt");

            // Assert
            Assert.IsTrue(result);
            Assert.IsNull(processor); // Disabled processors return null
        }

        [TestMethod]
        public void SetProcessorEnabled_EnableProcessor_ShouldEnable()
        {
            // Arrange
            _registry.RegisterProcessor(_receiptProcessorMock.Object);
            _registry.SetProcessorEnabled("Receipt", false);

            // Act
            var result = _registry.SetProcessorEnabled("Receipt", true);
            var processor = _registry.GetProcessor("Receipt");

            // Assert
            Assert.IsTrue(result);
            Assert.IsNotNull(processor);
        }

        [TestMethod]
        public void SetProcessorEnabled_NonExistentProcessor_ShouldReturnFalse()
        {
            // Act
            var result = _registry.SetProcessorEnabled("NonExistent", true);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void GetEnabledProcessors_ShouldReturnOnlyEnabled()
        {
            // Arrange
            _registry.RegisterProcessor(_receiptProcessorMock.Object, "Receipt", "Receipt processor", priority: 100);
            _registry.RegisterProcessor(_checkProcessorMock.Object, "Check", "Check processor", priority: 90);
            _registry.RegisterProcessor(_w4ProcessorMock.Object, "W4", "W4 processor", priority: 80);
            
            _registry.SetProcessorEnabled("Check", false);

            // Act
            var enabled = _registry.GetEnabledProcessors().ToList();

            // Assert
            Assert.AreEqual(2, enabled.Count);
            Assert.IsTrue(enabled.Any(p => p.Processor.DocumentType == "Receipt"));
            Assert.IsTrue(enabled.Any(p => p.Processor.DocumentType == "W4"));
            Assert.IsFalse(enabled.Any(p => p.Processor.DocumentType == "Check"));
        }

        [TestMethod]
        public void GetDisabledProcessors_ShouldReturnOnlyDisabled()
        {
            // Arrange
            _registry.RegisterProcessor(_receiptProcessorMock.Object, "Receipt", "Receipt processor");
            _registry.RegisterProcessor(_checkProcessorMock.Object, "Check", "Check processor");
            
            _registry.SetProcessorEnabled("Check", false);

            // Act
            var disabled = _registry.GetDisabledProcessors().ToList();

            // Assert
            Assert.AreEqual(1, disabled.Count);
            Assert.AreEqual("Check", disabled[0].Processor.DocumentType);
        }

        #endregion

        #region Priority-Based Selection Tests

        [TestMethod]
        public void GetHighestPriorityProcessor_ShouldReturnHighestPriority()
        {
            // Arrange
            var ocrData = "test data";
            
            _receiptProcessorMock.Setup(p => p.CanProcess(ocrData)).Returns(true);
            _checkProcessorMock.Setup(p => p.CanProcess(ocrData)).Returns(true);
            _w4ProcessorMock.Setup(p => p.CanProcess(ocrData)).Returns(true);
            
            _registry.RegisterProcessor(_receiptProcessorMock.Object, "Receipt", "Receipt", priority: 50);
            _registry.RegisterProcessor(_checkProcessorMock.Object, "Check", "Check", priority: 100);
            _registry.RegisterProcessor(_w4ProcessorMock.Object, "W4", "W4", priority: 75);

            // Act
            var processor = _registry.GetHighestPriorityProcessor(ocrData);

            // Assert
            Assert.IsNotNull(processor);
            Assert.AreEqual("Check", processor.DocumentType);
        }

        [TestMethod]
        public void GetHighestPriorityProcessor_WithDisabledHighPriority_ShouldSkipDisabled()
        {
            // Arrange
            var ocrData = "test data";
            
            _receiptProcessorMock.Setup(p => p.CanProcess(ocrData)).Returns(true);
            _checkProcessorMock.Setup(p => p.CanProcess(ocrData)).Returns(true);
            
            _registry.RegisterProcessor(_receiptProcessorMock.Object, "Receipt", "Receipt", priority: 50);
            _registry.RegisterProcessor(_checkProcessorMock.Object, "Check", "Check", priority: 100);
            
            _registry.SetProcessorEnabled("Check", false);

            // Act
            var processor = _registry.GetHighestPriorityProcessor(ocrData);

            // Assert
            Assert.IsNotNull(processor);
            Assert.AreEqual("Receipt", processor.DocumentType);
        }

        [TestMethod]
        public void GetCompatibleProcessors_ShouldReturnOrderedByPriority()
        {
            // Arrange
            var ocrData = "test data";
            
            _receiptProcessorMock.Setup(p => p.CanProcess(ocrData)).Returns(true);
            _checkProcessorMock.Setup(p => p.CanProcess(ocrData)).Returns(true);
            _w4ProcessorMock.Setup(p => p.CanProcess(ocrData)).Returns(false);
            
            _registry.RegisterProcessor(_receiptProcessorMock.Object, "Receipt", "Receipt", priority: 50);
            _registry.RegisterProcessor(_checkProcessorMock.Object, "Check", "Check", priority: 100);
            _registry.RegisterProcessor(_w4ProcessorMock.Object, "W4", "W4", priority: 200);

            // Act
            var compatible = _registry.GetCompatibleProcessors(ocrData).ToList();

            // Assert
            Assert.AreEqual(2, compatible.Count);
            Assert.AreEqual("Check", compatible[0].Processor.DocumentType);
            Assert.AreEqual(100, compatible[0].Priority);
            Assert.AreEqual("Receipt", compatible[1].Processor.DocumentType);
            Assert.AreEqual(50, compatible[1].Priority);
        }

        [TestMethod]
        public void GetCompatibleProcessors_WithNoCompatible_ShouldReturnEmpty()
        {
            // Arrange
            var ocrData = "test data";
            
            _receiptProcessorMock.Setup(p => p.CanProcess(ocrData)).Returns(false);
            _registry.RegisterProcessor(_receiptProcessorMock.Object);

            // Act
            var compatible = _registry.GetCompatibleProcessors(ocrData);

            // Assert
            Assert.AreEqual(0, compatible.Count());
        }

        #endregion

        #region Priority Update Tests

        [TestMethod]
        public void UpdateProcessorPriority_ShouldUpdatePriority()
        {
            // Arrange
            _registry.RegisterProcessor(_receiptProcessorMock.Object, "Receipt", "Receipt", priority: 50);

            // Act
            var result = _registry.UpdateProcessorPriority("Receipt", 200);
            var info = _registry.GetProcessorInfo("Receipt");

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(200, info.Priority);
        }

        [TestMethod]
        public void UpdateProcessorPriority_ShouldPreserveOtherMetadata()
        {
            // Arrange
            _registry.RegisterProcessor(
                _receiptProcessorMock.Object,
                "Receipt Scanner",
                "Scans receipts",
                new Version(1, 2, 3),
                priority: 50,
                capabilities: new[] { "ocr" }
            );
            _registry.SetProcessorEnabled("Receipt", false);

            // Act
            _registry.UpdateProcessorPriority("Receipt", 200);
            var info = _registry.GetProcessorInfo("Receipt");

            // Assert
            Assert.AreEqual("Receipt Scanner", info.DisplayName);
            Assert.AreEqual("Scans receipts", info.Description);
            Assert.AreEqual(new Version(1, 2, 3), info.Version);
            Assert.AreEqual(200, info.Priority);
            Assert.IsFalse(info.IsEnabled);
            Assert.AreEqual(1, info.Capabilities.Count);
        }

        [TestMethod]
        public void UpdateProcessorPriority_NonExistent_ShouldReturnFalse()
        {
            // Act
            var result = _registry.UpdateProcessorPriority("NonExistent", 100);

            // Assert
            Assert.IsFalse(result);
        }

        #endregion

        #region GetProcessorInfo Tests

        [TestMethod]
        public void GetProcessorInfo_AllProcessors_ShouldReturnAllOrdered()
        {
            // Arrange
            _registry.RegisterProcessor(_receiptProcessorMock.Object, "Receipt", "Receipt", priority: 50);
            _registry.RegisterProcessor(_checkProcessorMock.Object, "Check", "Check", priority: 100);
            _registry.RegisterProcessor(_w4ProcessorMock.Object, "W4", "W4", priority: 75);

            // Act
            var allInfo = _registry.GetProcessorInfo().ToList();

            // Assert
            Assert.AreEqual(3, allInfo.Count);
            Assert.AreEqual("Check", allInfo[0].Processor.DocumentType);
            Assert.AreEqual("W4", allInfo[1].Processor.DocumentType);
            Assert.AreEqual("Receipt", allInfo[2].Processor.DocumentType);
        }

        [TestMethod]
        public void GetProcessorInfo_SpecificProcessor_ShouldReturnInfo()
        {
            // Arrange
            _registry.RegisterProcessor(
                _receiptProcessorMock.Object,
                "Receipt Scanner",
                "Scans receipts",
                new Version(2, 1, 0),
                priority: 100
            );

            // Act
            var info = _registry.GetProcessorInfo("Receipt");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Receipt Scanner", info.DisplayName);
            Assert.AreEqual(new Version(2, 1, 0), info.Version);
            Assert.AreEqual(100, info.Priority);
        }

        [TestMethod]
        public void GetProcessorInfo_NonExistent_ShouldReturnNull()
        {
            // Act
            var info = _registry.GetProcessorInfo("NonExistent");

            // Assert
            Assert.IsNull(info);
        }

        #endregion

        #region Data-Driven Tests

        [TestMethod]
        [DataRow("Receipt", 100, true, true)]
        [DataRow("Check", 50, false, false)]
        [DataRow("W4", 200, true, true)]
        public void RegisterAndToggleProcessor_DataDriven(string documentType, int priority, bool initialEnabled, bool expectedEnabled)
        {
            // Arrange
            var processor = new Mock<IDocumentProcessor>();
            processor.Setup(p => p.DocumentType).Returns(documentType);
            
            // Act
            _registry.RegisterProcessor(processor.Object, documentType, $"{documentType} processor", priority: priority);
            if (!initialEnabled)
            {
                _registry.SetProcessorEnabled(documentType, false);
            }
            
            var info = _registry.GetProcessorInfo(documentType);
            
            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual(priority, info.Priority);
            Assert.AreEqual(expectedEnabled, info.IsEnabled);
        }

        [TestMethod]
        [DataRow(100, 50, 75, "Type1")]  // Highest priority wins
        [DataRow(50, 100, 75, "Type2")]  // Highest priority wins
        [DataRow(75, 75, 100, "Type3")]  // Highest priority wins
        public void PrioritySelection_DataDriven(int priority1, int priority2, int priority3, string expectedType)
        {
            // Arrange
            var ocrData = "test data";
            var processor1 = CreateMockProcessor("Type1", true);
            var processor2 = CreateMockProcessor("Type2", true);
            var processor3 = CreateMockProcessor("Type3", true);
            
            _registry.RegisterProcessor(processor1, "Type1", "Type1", priority: priority1);
            _registry.RegisterProcessor(processor2, "Type2", "Type2", priority: priority2);
            _registry.RegisterProcessor(processor3, "Type3", "Type3", priority: priority3);
            
            // Act
            var selected = _registry.GetHighestPriorityProcessor(ocrData);
            
            // Assert
            Assert.IsNotNull(selected);
            Assert.AreEqual(expectedType, selected.DocumentType);
        }

        #endregion

        #region Helper Methods

        private IDocumentProcessor CreateMockProcessor(string documentType, bool canProcess)
        {
            var mock = new Mock<IDocumentProcessor>();
            mock.Setup(p => p.DocumentType).Returns(documentType);
            mock.Setup(p => p.CanProcess(It.IsAny<string>())).Returns(canProcess);
            return mock.Object;
        }

        private class ProcessorMetadata : IProcessorMetadata
        {
            public string DisplayName { get; set; }
            public string Description { get; set; }
            public Version Version { get; set; }
            public int Priority { get; set; }
            public IReadOnlyCollection<string> Capabilities { get; set; }
            public bool IsEnabled { get; set; } = true;
            public IReadOnlyCollection<string> SupportedExtensions { get; set; }
            public IReadOnlyDictionary<string, object> AdditionalMetadata { get; set; } = new Dictionary<string, object>();
        }

        #endregion
    }
}