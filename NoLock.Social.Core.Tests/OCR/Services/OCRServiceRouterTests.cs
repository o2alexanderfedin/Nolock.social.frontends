using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Models;
using NoLock.Social.Core.OCR.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NoLock.Social.Core.Tests.OCR.Services
{
    public class OCRServiceRouterTests
    {
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly Mock<ILogger<OCRServiceRouter>> _mockLogger;
        private readonly Mock<IOCRService> _mockReceiptService;
        private readonly Mock<IOCRService> _mockCheckService;
        private readonly OCRServiceRouter _router;
        private readonly Dictionary<(Type, object), object> _keyedServices;

        public OCRServiceRouterTests()
        {
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockLogger = new Mock<ILogger<OCRServiceRouter>>();
            _mockReceiptService = new Mock<IOCRService>();
            _mockCheckService = new Mock<IOCRService>();
            _keyedServices = new Dictionary<(Type, object), object>();
            
            // Setup service provider to also implement IKeyedServiceProvider
            _mockServiceProvider.As<IKeyedServiceProvider>()
                .Setup(x => x.GetKeyedService(It.IsAny<Type>(), It.IsAny<object>()))
                .Returns((Type serviceType, object key) =>
                {
                    if (_keyedServices.TryGetValue((serviceType, key), out var service))
                        return service;
                    return null;
                });

            _mockServiceProvider.As<IKeyedServiceProvider>()
                .Setup(x => x.GetRequiredKeyedService(It.IsAny<Type>(), It.IsAny<object>()))
                .Returns((Type serviceType, object key) =>
                {
                    if (_keyedServices.TryGetValue((serviceType, key), out var service))
                        return service;
                    throw new InvalidOperationException($"No service for type '{serviceType}' and service key '{key}' has been registered.");
                });

            _router = new OCRServiceRouter(_mockServiceProvider.Object, _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_NullServiceProvider_ThrowsArgumentNullException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => 
                new OCRServiceRouter(null!, _mockLogger.Object));
            Assert.Equal("serviceProvider", ex.ParamName);
        }

        [Fact]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => 
                new OCRServiceRouter(_mockServiceProvider.Object, null!));
            Assert.Equal("logger", ex.ParamName);
        }

        [Fact]
        public void Constructor_ValidParameters_CreatesInstance()
        {
            // Arrange & Act
            var router = new OCRServiceRouter(_mockServiceProvider.Object, _mockLogger.Object);

            // Assert
            Assert.NotNull(router);
        }

        #endregion

        #region Request Validation Tests

        [Fact]
        public async Task SubmitDocumentAsync_NullRequest_ThrowsArgumentNullException()
        {
            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentNullException>(
                () => _router.SubmitDocumentAsync(null!));
            Assert.Contains("request", ex.Message);
        }

        #endregion

        #region Document Type Routing Tests

        [Theory]
        [MemberData(nameof(DocumentTypeRoutingTestCases))]
        public async Task SubmitDocumentAsync_ValidDocumentType_RoutesToCorrectService(
            DocumentType documentType, string expectedLogMessage)
        {
            // Arrange
            var imageData = new byte[] { 1, 2, 3, 4 };
            var request = new OCRSubmissionRequest 
            { 
                ImageData = imageData, 
                DocumentType = documentType 
            };
            var cancellationToken = new CancellationToken();

            var mockService = documentType == DocumentType.Receipt ? _mockReceiptService : _mockCheckService;
            
            SetupServiceProvider(documentType, mockService.Object);

            mockService.Setup(x => x.SubmitDocumentAsync(request, cancellationToken))
                .Returns(Task.CompletedTask);

            // Act
            await _router.SubmitDocumentAsync(request, cancellationToken);

            // Assert
            VerifyServiceResolution(documentType);
            mockService.Verify(x => x.SubmitDocumentAsync(request, cancellationToken), Times.Once);
            VerifyLogMessage(LogLevel.Information, $"Routing OCR request for document type: {expectedLogMessage}");
        }

        public static IEnumerable<object[]> DocumentTypeRoutingTestCases()
        {
            yield return new object[] { DocumentType.Receipt, "Receipt" };
            yield return new object[] { DocumentType.Check, "Check" };
        }

        [Theory]
        [InlineData(DocumentType.Receipt)]
        [InlineData(DocumentType.Check)]
        public async Task SubmitDocumentAsync_DefaultCancellationToken_PassesToUnderlyingService(
            DocumentType documentType)
        {
            // Arrange
            var imageData = new byte[] { 1, 2, 3, 4 };
            var request = new OCRSubmissionRequest 
            { 
                ImageData = imageData, 
                DocumentType = documentType 
            };

            var mockService = new Mock<IOCRService>();
            SetupServiceProvider(documentType, mockService.Object);

            mockService.Setup(x => x.SubmitDocumentAsync(request, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _router.SubmitDocumentAsync(request);

            // Assert
            mockService.Verify(x => x.SubmitDocumentAsync(request, default(CancellationToken)), Times.Once);
        }

        #endregion

        #region Service Resolution Error Tests

        [Fact]
        public async Task SubmitDocumentAsync_ServiceProviderReturnsNull_ThrowsInvalidOperationException()
        {
            // Arrange
            var imageData = new byte[] { 1, 2, 3, 4 };
            var request = new OCRSubmissionRequest 
            { 
                ImageData = imageData, 
                DocumentType = DocumentType.Receipt 
            };

            // Don't set up the service, so GetRequiredKeyedService will throw

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _router.SubmitDocumentAsync(request));
            
            Assert.Contains("No service for type", ex.Message);
            VerifyLogMessage(LogLevel.Information, "Routing OCR request for document type: Receipt");
        }

        [Theory]
        [MemberData(nameof(ServiceProviderExceptionTestCases))]
        public async Task SubmitDocumentAsync_ServiceProviderThrowsException_PropagatesException(
            Type exceptionType, string exceptionMessage)
        {
            // Arrange
            var imageData = new byte[] { 1, 2, 3, 4 };
            var request = new OCRSubmissionRequest 
            { 
                ImageData = imageData, 
                DocumentType = DocumentType.Receipt 
            };
            
            var expectedException = (Exception)Activator.CreateInstance(exceptionType, exceptionMessage)!;

            _mockServiceProvider.As<IKeyedServiceProvider>()
                .Setup(x => x.GetRequiredKeyedService(typeof(IOCRService), DocumentType.Receipt))
                .Throws(expectedException);

            // Act & Assert
            var ex = await Assert.ThrowsAsync(exceptionType, 
                () => _router.SubmitDocumentAsync(request));
            
            Assert.Equal(exceptionMessage, ex.Message);
            VerifyLogMessage(LogLevel.Information, "Routing OCR request for document type: Receipt");
        }

        public static IEnumerable<object[]> ServiceProviderExceptionTestCases()
        {
            yield return new object[] { typeof(InvalidOperationException), "Service resolution failed" };
            yield return new object[] { typeof(ArgumentException), "Invalid service key" };
            yield return new object[] { typeof(NotSupportedException), "Service not registered" };
        }

        #endregion

        #region Underlying Service Exception Tests

        [Theory]
        [MemberData(nameof(UnderlyingServiceExceptionTestCases))]
        public async Task SubmitDocumentAsync_UnderlyingServiceThrowsException_PropagatesException(
            Type exceptionType, string exceptionMessage, DocumentType documentType)
        {
            // Arrange
            var imageData = new byte[] { 1, 2, 3, 4 };
            var request = new OCRSubmissionRequest 
            { 
                ImageData = imageData, 
                DocumentType = documentType 
            };
            var cancellationToken = new CancellationToken();
            var expectedException = (Exception)Activator.CreateInstance(exceptionType, exceptionMessage)!;

            var mockService = new Mock<IOCRService>();
            SetupServiceProvider(documentType, mockService.Object);

            mockService.Setup(x => x.SubmitDocumentAsync(request, cancellationToken))
                .ThrowsAsync(expectedException);

            // Act & Assert
            var ex = await Assert.ThrowsAsync(exceptionType,
                () => _router.SubmitDocumentAsync(request, cancellationToken));
            
            Assert.Equal(exceptionMessage, ex.Message);
            mockService.Verify(x => x.SubmitDocumentAsync(request, cancellationToken), Times.Once);
        }

        public static IEnumerable<object[]> UnderlyingServiceExceptionTestCases()
        {
            yield return new object[] { typeof(InvalidOperationException), "OCR processing failed", DocumentType.Receipt };
            yield return new object[] { typeof(ArgumentException), "Invalid document", DocumentType.Check };
            yield return new object[] { typeof(TimeoutException), "Processing timeout", DocumentType.Receipt };
            yield return new object[] { typeof(NotSupportedException), "Unsupported format", DocumentType.Check };
        }

        #endregion

        #region Multiple Calls Tests

        [Theory]
        [InlineData(DocumentType.Receipt)]
        [InlineData(DocumentType.Check)]
        public async Task SubmitDocumentAsync_MultipleCallsWithSameDocumentType_ResolvesServiceEachTime(
            DocumentType documentType)
        {
            // Arrange
            var imageData1 = new byte[] { 1, 2, 3, 4 };
            var imageData2 = new byte[] { 5, 6, 7, 8 };
            var request1 = new OCRSubmissionRequest { ImageData = imageData1, DocumentType = documentType };
            var request2 = new OCRSubmissionRequest { ImageData = imageData2, DocumentType = documentType };

            var mockService = new Mock<IOCRService>();
            SetupServiceProvider(documentType, mockService.Object);

            mockService.Setup(x => x.SubmitDocumentAsync(It.IsAny<OCRSubmissionRequest>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _router.SubmitDocumentAsync(request1);
            await _router.SubmitDocumentAsync(request2);

            // Assert
            VerifyServiceResolution(documentType, Times.Exactly(2));
            mockService.Verify(x => x.SubmitDocumentAsync(request1, default(CancellationToken)), Times.Once);
            mockService.Verify(x => x.SubmitDocumentAsync(request2, default(CancellationToken)), Times.Once);
        }

        [Fact]
        public async Task SubmitDocumentAsync_DifferentDocumentTypes_RoutesToDifferentServices()
        {
            // Arrange
            var receiptRequest = new OCRSubmissionRequest 
            { 
                ImageData = new byte[] { 1, 2, 3, 4 }, 
                DocumentType = DocumentType.Receipt 
            };
            var checkRequest = new OCRSubmissionRequest 
            { 
                ImageData = new byte[] { 5, 6, 7, 8 }, 
                DocumentType = DocumentType.Check 
            };

            SetupServiceProvider(DocumentType.Receipt, _mockReceiptService.Object);
            SetupServiceProvider(DocumentType.Check, _mockCheckService.Object);

            _mockReceiptService.Setup(x => x.SubmitDocumentAsync(receiptRequest, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _mockCheckService.Setup(x => x.SubmitDocumentAsync(checkRequest, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _router.SubmitDocumentAsync(receiptRequest);
            await _router.SubmitDocumentAsync(checkRequest);

            // Assert
            _mockReceiptService.Verify(x => x.SubmitDocumentAsync(receiptRequest, default(CancellationToken)), Times.Once);
            _mockCheckService.Verify(x => x.SubmitDocumentAsync(checkRequest, default(CancellationToken)), Times.Once);
        }

        #endregion

        #region Concurrent Execution Tests

        [Fact]
        public async Task SubmitDocumentAsync_ConcurrentCallsWithSameType_HandledCorrectly()
        {
            // Arrange
            var request1 = new OCRSubmissionRequest 
            { 
                ImageData = new byte[] { 1, 2, 3, 4 }, 
                DocumentType = DocumentType.Receipt 
            };
            var request2 = new OCRSubmissionRequest 
            { 
                ImageData = new byte[] { 5, 6, 7, 8 }, 
                DocumentType = DocumentType.Receipt 
            };

            var mockService = new Mock<IOCRService>();
            SetupServiceProvider(DocumentType.Receipt, mockService.Object);

            var completionSource1 = new TaskCompletionSource<bool>();
            var completionSource2 = new TaskCompletionSource<bool>();

            mockService.Setup(x => x.SubmitDocumentAsync(request1, It.IsAny<CancellationToken>()))
                .Returns(async () => { await completionSource1.Task; });
            mockService.Setup(x => x.SubmitDocumentAsync(request2, It.IsAny<CancellationToken>()))
                .Returns(async () => { await completionSource2.Task; });

            // Act
            var task1 = _router.SubmitDocumentAsync(request1);
            var task2 = _router.SubmitDocumentAsync(request2);

            // Complete tasks
            completionSource1.SetResult(true);
            completionSource2.SetResult(true);

            await Task.WhenAll(task1, task2);

            // Assert
            mockService.Verify(x => x.SubmitDocumentAsync(request1, default(CancellationToken)), Times.Once);
            mockService.Verify(x => x.SubmitDocumentAsync(request2, default(CancellationToken)), Times.Once);
        }

        [Fact]
        public async Task SubmitDocumentAsync_ConcurrentCallsWithDifferentTypes_HandledCorrectly()
        {
            // Arrange
            var request1 = new OCRSubmissionRequest 
            { 
                ImageData = new byte[] { 1, 2, 3, 4 }, 
                DocumentType = DocumentType.Receipt 
            };
            var request2 = new OCRSubmissionRequest 
            { 
                ImageData = new byte[] { 5, 6, 7, 8 }, 
                DocumentType = DocumentType.Check 
            };

            SetupServiceProvider(DocumentType.Receipt, _mockReceiptService.Object);
            SetupServiceProvider(DocumentType.Check, _mockCheckService.Object);

            _mockReceiptService.Setup(x => x.SubmitDocumentAsync(request1, It.IsAny<CancellationToken>()))
                .Returns(Task.Delay(100));
            _mockCheckService.Setup(x => x.SubmitDocumentAsync(request2, It.IsAny<CancellationToken>()))
                .Returns(Task.Delay(100));

            // Act
            var task1 = _router.SubmitDocumentAsync(request1);
            var task2 = _router.SubmitDocumentAsync(request2);
            await Task.WhenAll(task1, task2);

            // Assert
            _mockReceiptService.Verify(x => x.SubmitDocumentAsync(request1, default(CancellationToken)), Times.Once);
            _mockCheckService.Verify(x => x.SubmitDocumentAsync(request2, default(CancellationToken)), Times.Once);
        }

        #endregion

        #region Cancellation Token Tests

        [Fact]
        public async Task SubmitDocumentAsync_CancellationTokenPropagated_PassesToService()
        {
            // Arrange
            var request = new OCRSubmissionRequest 
            { 
                ImageData = new byte[] { 1, 2, 3, 4 }, 
                DocumentType = DocumentType.Receipt 
            };
            using var cts = new CancellationTokenSource();

            var mockService = new Mock<IOCRService>();
            SetupServiceProvider(DocumentType.Receipt, mockService.Object);

            mockService.Setup(x => x.SubmitDocumentAsync(request, cts.Token))
                .Returns(Task.CompletedTask);

            // Act
            await _router.SubmitDocumentAsync(request, cts.Token);

            // Assert
            mockService.Verify(x => x.SubmitDocumentAsync(request, cts.Token), Times.Once);
        }

        [Fact]
        public async Task SubmitDocumentAsync_CancelledToken_PropagatesCancellation()
        {
            // Arrange
            var request = new OCRSubmissionRequest 
            { 
                ImageData = new byte[] { 1, 2, 3, 4 }, 
                DocumentType = DocumentType.Receipt 
            };
            using var cts = new CancellationTokenSource();
            
            var mockService = new Mock<IOCRService>();
            SetupServiceProvider(DocumentType.Receipt, mockService.Object);

            mockService.Setup(x => x.SubmitDocumentAsync(request, It.IsAny<CancellationToken>()))
                .Returns(async (OCRSubmissionRequest _, CancellationToken ct) =>
                {
                    await Task.Delay(100, ct);
                });

            // Act
            var task = _router.SubmitDocumentAsync(request, cts.Token);
            cts.Cancel();

            // Assert
            await Assert.ThrowsAsync<TaskCanceledException>(() => task);
        }

        #endregion

        #region Logging Tests

        [Theory]
        [InlineData(DocumentType.Receipt, "Receipt")]
        [InlineData(DocumentType.Check, "Check")]
        public async Task SubmitDocumentAsync_Always_LogsRoutingInformation(
            DocumentType documentType, string expectedDocumentTypeName)
        {
            // Arrange
            var request = new OCRSubmissionRequest 
            { 
                ImageData = new byte[] { 1, 2, 3, 4 }, 
                DocumentType = documentType 
            };

            var mockService = new Mock<IOCRService>();
            SetupServiceProvider(documentType, mockService.Object);

            mockService.Setup(x => x.SubmitDocumentAsync(request, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _router.SubmitDocumentAsync(request);

            // Assert
            VerifyLogMessage(LogLevel.Information, $"Routing OCR request for document type: {expectedDocumentTypeName}");
        }

        #endregion

        #region Edge Cases Tests

        [Fact]
        public async Task SubmitDocumentAsync_ServiceThrowsImmediately_PropagatesException()
        {
            // Arrange
            var request = new OCRSubmissionRequest 
            { 
                ImageData = new byte[] { 1, 2, 3, 4 }, 
                DocumentType = DocumentType.Receipt 
            };

            var mockService = new Mock<IOCRService>();
            SetupServiceProvider(DocumentType.Receipt, mockService.Object);

            mockService.Setup(x => x.SubmitDocumentAsync(request, It.IsAny<CancellationToken>()))
                .Throws(new InvalidOperationException("Immediate failure"));

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _router.SubmitDocumentAsync(request));
            Assert.Equal("Immediate failure", ex.Message);
        }

        [Fact]
        public async Task SubmitDocumentAsync_EmptyImageData_PropagatedToService()
        {
            // Arrange - Router doesn't validate, just routes
            var request = new OCRSubmissionRequest 
            { 
                ImageData = new byte[0], 
                DocumentType = DocumentType.Receipt 
            };

            var mockService = new Mock<IOCRService>();
            SetupServiceProvider(DocumentType.Receipt, mockService.Object);

            mockService.Setup(x => x.SubmitDocumentAsync(request, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _router.SubmitDocumentAsync(request);

            // Assert - Router passes request as-is to service
            mockService.Verify(x => x.SubmitDocumentAsync(request, default(CancellationToken)), Times.Once);
        }

        [Fact]
        public async Task SubmitDocumentAsync_ServiceReturnsNull_HandledCorrectly()
        {
            // Arrange
            var request = new OCRSubmissionRequest 
            { 
                ImageData = new byte[] { 1, 2, 3, 4 }, 
                DocumentType = DocumentType.Receipt 
            };

            // The GetRequiredKeyedService check for null is actually handled within the method
            // If it returns null (which it shouldn't for GetRequired*), it would throw NotSupportedException
            var mockService = new Mock<IOCRService>();
            
            // Override the default setup to return null
            _mockServiceProvider.As<IKeyedServiceProvider>()
                .Setup(x => x.GetRequiredKeyedService(typeof(IOCRService), DocumentType.Receipt))
                .Returns((object)null!);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<NotSupportedException>(
                () => _router.SubmitDocumentAsync(request));
            
            Assert.Equal("Document type Receipt is not supported", ex.Message);
        }

        #endregion

        #region Performance and Stress Tests

        [Fact]
        public async Task SubmitDocumentAsync_LargeImageData_HandledCorrectly()
        {
            // Arrange - 10MB image
            var largeImageData = new byte[10 * 1024 * 1024];
            new Random().NextBytes(largeImageData);
            
            var request = new OCRSubmissionRequest 
            { 
                ImageData = largeImageData, 
                DocumentType = DocumentType.Receipt 
            };

            var mockService = new Mock<IOCRService>();
            SetupServiceProvider(DocumentType.Receipt, mockService.Object);

            mockService.Setup(x => x.SubmitDocumentAsync(request, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _router.SubmitDocumentAsync(request);

            // Assert
            mockService.Verify(x => x.SubmitDocumentAsync(request, default(CancellationToken)), Times.Once);
        }

        [Fact]
        public async Task SubmitDocumentAsync_RapidSuccessiveCalls_HandledCorrectly()
        {
            // Arrange
            var requests = new List<OCRSubmissionRequest>();
            for (int i = 0; i < 100; i++)
            {
                requests.Add(new OCRSubmissionRequest
                {
                    ImageData = new byte[] { (byte)i },
                    DocumentType = i % 2 == 0 ? DocumentType.Receipt : DocumentType.Check
                });
            }

            SetupServiceProvider(DocumentType.Receipt, _mockReceiptService.Object);
            SetupServiceProvider(DocumentType.Check, _mockCheckService.Object);

            _mockReceiptService.Setup(x => x.SubmitDocumentAsync(It.IsAny<OCRSubmissionRequest>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _mockCheckService.Setup(x => x.SubmitDocumentAsync(It.IsAny<OCRSubmissionRequest>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var tasks = requests.Select(r => _router.SubmitDocumentAsync(r)).ToList();
            await Task.WhenAll(tasks);

            // Assert
            _mockReceiptService.Verify(x => x.SubmitDocumentAsync(It.IsAny<OCRSubmissionRequest>(), It.IsAny<CancellationToken>()), 
                Times.Exactly(50));
            _mockCheckService.Verify(x => x.SubmitDocumentAsync(It.IsAny<OCRSubmissionRequest>(), It.IsAny<CancellationToken>()), 
                Times.Exactly(50));
        }

        #endregion

        #region Helper Methods

        private void SetupServiceProvider(DocumentType documentType, IOCRService service)
        {
            _keyedServices[(typeof(IOCRService), documentType)] = service;
        }

        private void VerifyServiceResolution(DocumentType documentType, Times? times = null)
        {
            _mockServiceProvider.As<IKeyedServiceProvider>().Verify(
                x => x.GetRequiredKeyedService(typeof(IOCRService), documentType), 
                times ?? Times.Once());
        }

        private void VerifyLogMessage(LogLevel level, string message)
        {
            _mockLogger.Verify(
                x => x.Log(
                    level,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(message)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        #endregion
    }
}