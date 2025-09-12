using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Core.Camera.Interfaces;
using NoLock.Social.Core.Camera.Models;
using NoLock.Social.Core.Camera.Services;
using NoLock.Social.Core.Identity.Interfaces;
using NoLock.Social.Core.Identity.Models;
using NoLock.Social.Core.Storage;
using NoLock.Social.Core.OCR.Interfaces;
using BlazorPro.BlazorSize;
using NoLock.Social.Core.OCR.Models;
using OCRDocumentType = NoLock.Social.Core.OCR.Models.DocumentType;
using NoLock.Social.Core.Storage;
using NoLock.Social.Web.Pages;
using NoLock.Social.Web.Tests.Fixtures;
using NoLock.Social.Web.Tests.Helpers;
using System;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NoLock.Social.Web.Tests.Pages
{
    public class DocumentCaptureTests : TestContextFixture
    {
        private readonly Mock<ICameraService> _mockCameraService;
        private readonly Mock<IOCRService> _mockOCRService;
        private readonly Mock<ILoginAdapterService> _mockLoginService;
        private readonly FakeNavigationManager _navigationManager;
        private readonly Mock<ILogger<DocumentCapture>> _mockLogger;
        private readonly Mock<ILogger<ImageProcessingService>> _mockImageProcessingLogger;
        private readonly Mock<IContentAddressableStorage<ContentData<byte[]>>> _mockStorageService;
        private readonly Mock<IResizeListener> _mockResizeListener;
        private readonly Subject<LoginStateChange> _loginStateSubject;

        public DocumentCaptureTests() : base()
        {
            // Initialize all mocks
            _mockCameraService = new Mock<ICameraService>();
            _mockOCRService = new Mock<IOCRService>();
            _mockLoginService = new Mock<ILoginAdapterService>();
            _mockLogger = new Mock<ILogger<DocumentCapture>>();
            _mockImageProcessingLogger = new Mock<ILogger<ImageProcessingService>>();
            _mockStorageService = new Mock<IContentAddressableStorage<ContentData<byte[]>>>();
            _mockResizeListener = new Mock<IResizeListener>();
            _loginStateSubject = new Subject<LoginStateChange>();

            // Setup login service with observable
            _mockLoginService.Setup(x => x.LoginStateChanges).Returns(_loginStateSubject);

            // Register all services BEFORE getting any service
            Services.AddSingleton(_mockCameraService.Object);
            Services.AddSingleton(_mockOCRService.Object);
            Services.AddSingleton(_mockLoginService.Object);
            Services.AddSingleton(_mockLogger.Object);
            Services.AddSingleton(_mockImageProcessingLogger.Object);
            Services.AddSingleton(_mockStorageService.Object);
            Services.AddSingleton(_mockResizeListener.Object);
            
            // Register the real ImageProcessingService with dependency injection
            Services.AddSingleton<IImageProcessingService, ImageProcessingService>();
            
            // Get FakeNavigationManager AFTER all services are registered
            _navigationManager = Services.GetRequiredService<FakeNavigationManager>();
        }

        [Fact]
        public void DocumentCapture_Should_Initialize_WithLoginWarning_WhenNotLoggedIn()
        {
            // Arrange
            var loginState = new LoginState { IsLoggedIn = false };
            _mockLoginService.Setup(x => x.CurrentLoginState).Returns(loginState);

            // Act
            var component = RenderComponent<DocumentCapture>();

            // Assert
            Assert.NotNull(component);
            Assert.Contains("Please log in to capture documents", component.Markup);
            Assert.DoesNotContain("Camera Preview", component.Markup);
        }

        [Fact]
        public void DocumentCapture_Should_ShowCameraInterface_WhenLoggedIn()
        {
            // Arrange
            var loginState = new LoginState { IsLoggedIn = true, Username = "testuser" };
            _mockLoginService.Setup(x => x.CurrentLoginState).Returns(loginState);

            // Act
            var component = RenderComponent<DocumentCapture>();

            // Assert
            Assert.NotNull(component);
            Assert.Contains("Camera Preview", component.Markup);
            Assert.Contains("Document Type", component.Markup);
            Assert.Contains("Actions", component.Markup);
        }

        [Fact]
        public void DocumentCapture_Should_NavigateAway_WhenUserLogsOut()
        {
            // Arrange
            var loginState = new LoginState { IsLoggedIn = true, Username = "testuser" };
            _mockLoginService.Setup(x => x.CurrentLoginState).Returns(loginState);

            // Act
            var component = RenderComponent<DocumentCapture>();
            
            // Simulate logout
            var logoutChange = new LoginStateChange
            {
                PreviousState = loginState,
                NewState = new LoginState { IsLoggedIn = false }
            };
            _loginStateSubject.OnNext(logoutChange);

            // Assert
            Assert.Equal("http://localhost/", _navigationManager.Uri);
        }


        [Fact]
        public async Task DocumentCapture_Should_ProcessDocument_WithSelectedType()
        {
            // Arrange
            var loginState = new LoginState { IsLoggedIn = true, Username = "testuser" };
            _mockLoginService.Setup(x => x.CurrentLoginState).Returns(loginState);

            var testImageData = TestDataBuilder.CreateBase64TestImage();
            var contentHash = "test-hash-456";
            
            // Setup storage mock
            _mockStorageService.Setup(x => x.StoreAsync(It.IsAny<ContentData<byte[]>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(contentHash);

            _mockStorageService.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ContentData<byte[]>
                {
                    Data = TestDataBuilder.CreateTestImageData(),
                    MimeType = "image/png"
                });

            // Setup OCR service mock
            _mockOCRService.Setup(x => x.SubmitDocumentAsync(It.IsAny<OCRSubmissionRequest>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Navigation will be tracked by FakeNavigationManager

            // Act
            var component = RenderComponent<DocumentCapture>();
            var instance = component.Instance;

            // Capture an image first through the CameraCapture component
            var cameraCapture = component.FindComponent<NoLock.Social.Components.Camera.CameraCapture>();
            var capturedImage = new CapturedImage
            {
                ImageData = testImageData,
                Quality = 90
            };
            await cameraCapture.InvokeAsync(() => 
                cameraCapture.Instance.OnImageCaptured.InvokeAsync(capturedImage));
            component.Render();

            // Find and click the Process All Pages button
            var processButton = component.Find("button:contains('Process All Pages')");
            await processButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

            // Assert
            _mockOCRService.Verify(x => x.SubmitDocumentAsync(It.Is<OCRSubmissionRequest>(
                req => req.DocumentType == OCRDocumentType.Receipt && req.ImageData != null
            ), It.IsAny<CancellationToken>()), Times.Once);

            // Should navigate back to home after successful processing
            Assert.Equal("http://localhost/", _navigationManager.Uri);
        }

        [Fact]
        public async Task DocumentCapture_Should_RemovePage_WhenRemoveButtonClicked()
        {
            // Arrange
            var loginState = new LoginState { IsLoggedIn = true, Username = "testuser" };
            _mockLoginService.Setup(x => x.CurrentLoginState).Returns(loginState);

            var testImageData = TestDataBuilder.CreateBase64TestImage();
            var contentHash = "test-hash-789";
            
            _mockStorageService.Setup(x => x.StoreAsync(It.IsAny<ContentData<byte[]>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(contentHash);

            _mockStorageService.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ContentData<byte[]>
                {
                    Data = TestDataBuilder.CreateTestImageData(),
                    MimeType = "image/png"
                });

            // Act
            var component = RenderComponent<DocumentCapture>();
            var instance = component.Instance;

            // Add a page through the CameraCapture component
            var cameraCapture = component.FindComponent<NoLock.Social.Components.Camera.CameraCapture>();
            var capturedImage = new CapturedImage { ImageData = testImageData };
            await cameraCapture.InvokeAsync(async () => 
                await cameraCapture.Instance.OnImageCaptured.InvokeAsync(capturedImage));
            component.Render();

            // Find and click the remove button for the first page (now in FilmStrip)
            var removeButton = component.Find(".film-strip button.btn-danger");
            removeButton.Click();

            // Assert
            Assert.DoesNotContain("Captured Pages (1)", component.Markup);
            Assert.Contains("Captured Pages (0)", component.Markup);
        }

        [Fact]
        public async Task DocumentCapture_Should_DisplayQualityIndicators_AfterCapture()
        {
            // Arrange
            var loginState = new LoginState { IsLoggedIn = true, Username = "testuser" };
            _mockLoginService.Setup(x => x.CurrentLoginState).Returns(loginState);

            var testImageData = TestDataBuilder.CreateBase64TestImage();
            var contentHash = "quality-test-hash";
            
            _mockStorageService.Setup(x => x.StoreAsync(It.IsAny<ContentData<byte[]>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(contentHash);

            // Act
            var component = RenderComponent<DocumentCapture>();
            var instance = component.Instance;

            var cameraCapture = component.FindComponent<NoLock.Social.Components.Camera.CameraCapture>();
            var capturedImage = new CapturedImage
            {
                ImageData = testImageData,
                Quality = 95
            };

            await cameraCapture.InvokeAsync(async () => 
                await cameraCapture.Instance.OnImageCaptured.InvokeAsync(capturedImage));
            component.Render();

            // Assert
            Assert.Contains("Image Quality", component.Markup);
            Assert.Contains("Overall Quality", component.Markup);
            Assert.Contains("Sharpness", component.Markup);
            Assert.Contains("Lighting", component.Markup);
        }

        [Fact]
        public async Task DocumentCapture_Should_ClearAllPages_WhenClearButtonClicked()
        {
            // Arrange
            var loginState = new LoginState { IsLoggedIn = true, Username = "testuser" };
            _mockLoginService.Setup(x => x.CurrentLoginState).Returns(loginState);

            var testImageData = TestDataBuilder.CreateBase64TestImage();
            
            _mockStorageService.Setup(x => x.StoreAsync(It.IsAny<ContentData<byte[]>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("test-hash");

            _mockStorageService.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ContentData<byte[]>
                {
                    Data = TestDataBuilder.CreateTestImageData(),
                    MimeType = "image/png"
                });

            // Act
            var component = RenderComponent<DocumentCapture>();
            var instance = component.Instance;

            // Add multiple pages through the CameraCapture component
            var cameraCapture = component.FindComponent<NoLock.Social.Components.Camera.CameraCapture>();
            var capturedImage = new CapturedImage { ImageData = testImageData };
            await cameraCapture.InvokeAsync(async () => 
                await cameraCapture.Instance.OnImageCaptured.InvokeAsync(capturedImage));
            component.Render();
            await cameraCapture.InvokeAsync(async () => 
                await cameraCapture.Instance.OnImageCaptured.InvokeAsync(capturedImage));
            component.Render();

            // Find and click the Clear All Pages button
            var clearButton = component.Find("button:contains('Clear All Pages')");
            clearButton.Click();

            // Assert
            Assert.DoesNotContain("Captured Pages (2)", component.Markup);
            Assert.Contains("Captured Pages (0)", component.Markup);
            Assert.DoesNotContain("Image Quality", component.Markup);
        }

        [Fact]
        public async Task DocumentCapture_Should_HandleCameraError_Gracefully()
        {
            // Arrange
            var loginState = new LoginState { IsLoggedIn = true, Username = "testuser" };
            _mockLoginService.Setup(x => x.CurrentLoginState).Returns(loginState);

            // Act
            var component = RenderComponent<DocumentCapture>();
            var instance = component.Instance;

            // Trigger an error through the CameraCapture component
            var cameraCapture = component.FindComponent<NoLock.Social.Components.Camera.CameraCapture>();
            var errorMessage = "Camera access denied";
            await cameraCapture.InvokeAsync(() => 
                cameraCapture.Instance.OnError.InvokeAsync(errorMessage));

            // Assert - Expecting 2 calls: one from auto-initialization failure, one from explicit error
            _mockLogger.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Camera error")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ), Times.Exactly(2));
        }

        #region Error Handling Tests


        [Fact]
        public async Task DocumentCapture_Should_HandleOCRServiceFailure_WithCORSError()
        {
            // Arrange
            var loginState = new LoginState { IsLoggedIn = true, Username = "testuser" };
            _mockLoginService.Setup(x => x.CurrentLoginState).Returns(loginState);

            var testImageData = TestDataBuilder.CreateBase64TestImage();
            var contentHash = "test-hash";
            
            _mockStorageService.Setup(x => x.StoreAsync(It.IsAny<ContentData<byte[]>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(contentHash);

            _mockStorageService.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ContentData<byte[]>
                {
                    Data = TestDataBuilder.CreateTestImageData(),
                    MimeType = "image/png"
                });

            var corsException = new HttpRequestException("Failed to fetch");
            _mockOCRService.Setup(x => x.SubmitDocumentAsync(It.IsAny<OCRSubmissionRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(corsException);

            // Act
            var component = RenderComponent<DocumentCapture>();
            
            // Add an image first
            var cameraCapture = component.FindComponent<NoLock.Social.Components.Camera.CameraCapture>();
            await cameraCapture.InvokeAsync(() => 
                cameraCapture.Instance.OnImageCaptured.InvokeAsync(new CapturedImage { ImageData = testImageData }));

            // Process the document
            var processButton = component.Find("button:contains('Process All Pages')");
            await processButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

            // Assert - CORS error should be logged with specific message
            _mockLogger.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("CORS error calling OCR API")),
                It.Is<Exception>(e => e == corsException),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ), Times.Once);

            // Should not navigate on error - URI should remain at localhost (where component was rendered)
            Assert.Equal("http://localhost/", _navigationManager.Uri);
        }

        [Fact]
        public async Task DocumentCapture_Should_HandleGenericOCRServiceFailure()
        {
            // Arrange
            var loginState = new LoginState { IsLoggedIn = true, Username = "testuser" };
            _mockLoginService.Setup(x => x.CurrentLoginState).Returns(loginState);

            var testImageData = TestDataBuilder.CreateBase64TestImage();
            var contentHash = "test-hash";
            
            _mockStorageService.Setup(x => x.StoreAsync(It.IsAny<ContentData<byte[]>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(contentHash);

            _mockStorageService.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ContentData<byte[]>
                {
                    Data = TestDataBuilder.CreateTestImageData(),
                    MimeType = "image/png"
                });

            var genericException = new Exception("OCR service unavailable");
            _mockOCRService.Setup(x => x.SubmitDocumentAsync(It.IsAny<OCRSubmissionRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(genericException);

            // Act
            var component = RenderComponent<DocumentCapture>();
            
            // Add an image first
            var cameraCapture = component.FindComponent<NoLock.Social.Components.Camera.CameraCapture>();
            await cameraCapture.InvokeAsync(() => 
                cameraCapture.Instance.OnImageCaptured.InvokeAsync(new CapturedImage { ImageData = testImageData }));

            // Process the document
            var processButton = component.Find("button:contains('Process All Pages')");
            await processButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

            // Assert
            _mockLogger.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error calling OCR API")),
                It.Is<Exception>(e => e == genericException),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ), Times.Once);
        }

        [Fact]
        public async Task DocumentCapture_Should_HandleStorageRetrievalFailure_WhenProcessingDocument()
        {
            // Arrange
            var loginState = new LoginState { IsLoggedIn = true, Username = "testuser" };
            _mockLoginService.Setup(x => x.CurrentLoginState).Returns(loginState);

            var testImageData = TestDataBuilder.CreateBase64TestImage();
            var contentHash = "test-hash-missing";
            
            _mockStorageService.Setup(x => x.StoreAsync(It.IsAny<ContentData<byte[]>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(contentHash);

            // Storage returns null when attempting to retrieve
            _mockStorageService.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ContentData<byte[]>?)null);

            // Act
            var component = RenderComponent<DocumentCapture>();
            
            // Add an image first
            var cameraCapture = component.FindComponent<NoLock.Social.Components.Camera.CameraCapture>();
            await cameraCapture.InvokeAsync(() => 
                cameraCapture.Instance.OnImageCaptured.InvokeAsync(new CapturedImage { ImageData = testImageData }));

            // Process the document
            var processButton = component.Find("button:contains('Process All Pages')");
            await processButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

            // Assert
            _mockLogger.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Could not retrieve image data for hash {contentHash}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ), Times.Once);

            // OCR service should not be called if image retrieval fails
            _mockOCRService.Verify(x => x.SubmitDocumentAsync(It.IsAny<OCRSubmissionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion

        #region Document Type Tests

        [Theory]
        [InlineData(OCRDocumentType.Receipt, "receipt")]
        [InlineData(OCRDocumentType.Check, "check")]
        public async Task DocumentCapture_Should_ProcessWithCorrectDocumentType(OCRDocumentType expectedType, string radioId)
        {
            // Arrange
            var loginState = new LoginState { IsLoggedIn = true, Username = "testuser" };
            _mockLoginService.Setup(x => x.CurrentLoginState).Returns(loginState);

            var testImageData = TestDataBuilder.CreateBase64TestImage();
            var contentHash = "test-hash";
            
            _mockStorageService.Setup(x => x.StoreAsync(It.IsAny<ContentData<byte[]>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(contentHash);

            _mockStorageService.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ContentData<byte[]>
                {
                    Data = TestDataBuilder.CreateTestImageData(),
                    MimeType = "image/png"
                });

            _mockOCRService.Setup(x => x.SubmitDocumentAsync(It.IsAny<OCRSubmissionRequest>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var component = RenderComponent<DocumentCapture>();

            // Select the document type
            var radioButton = component.Find($"#{radioId}");
            await radioButton.ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = true });

            // Add an image
            var cameraCapture = component.FindComponent<NoLock.Social.Components.Camera.CameraCapture>();
            await cameraCapture.InvokeAsync(() => 
                cameraCapture.Instance.OnImageCaptured.InvokeAsync(new CapturedImage { ImageData = testImageData }));

            // Process the document
            var processButton = component.Find("button:contains('Process All Pages')");
            await processButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

            // Assert
            _mockOCRService.Verify(x => x.SubmitDocumentAsync(
                It.Is<OCRSubmissionRequest>(req => req.DocumentType == expectedType),
                It.IsAny<CancellationToken>()
            ), Times.Once);
        }

        #endregion

        #region Multiple Page Management Tests

        [Fact]
        public async Task DocumentCapture_Should_ProcessMultiplePages_Sequentially()
        {
            // Arrange
            var loginState = new LoginState { IsLoggedIn = true, Username = "testuser" };
            _mockLoginService.Setup(x => x.CurrentLoginState).Returns(loginState);

            var testImageData1 = TestDataBuilder.CreateBase64TestImage();
            var testImageData2 = TestDataBuilder.CreateBase64TestImage();
            var hash1 = "hash-page-1";
            var hash2 = "hash-page-2";
            
            var storeCallCount = 0;
            _mockStorageService.Setup(x => x.StoreAsync(It.IsAny<ContentData<byte[]>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => storeCallCount++ == 0 ? hash1 : hash2);

            _mockStorageService.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ContentData<byte[]>
                {
                    Data = TestDataBuilder.CreateTestImageData(),
                    MimeType = "image/png"
                });

            _mockOCRService.Setup(x => x.SubmitDocumentAsync(It.IsAny<OCRSubmissionRequest>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Navigation is handled by FakeNavigationManager

            // Act
            var component = RenderComponent<DocumentCapture>();
            var cameraCapture = component.FindComponent<NoLock.Social.Components.Camera.CameraCapture>();

            // Capture two pages
            await cameraCapture.InvokeAsync(() => 
                cameraCapture.Instance.OnImageCaptured.InvokeAsync(new CapturedImage { ImageData = testImageData1 }));
            await cameraCapture.InvokeAsync(() => 
                cameraCapture.Instance.OnImageCaptured.InvokeAsync(new CapturedImage { ImageData = testImageData2 }));

            // Verify both pages are displayed
            Assert.Contains("Captured Pages (2)", component.Markup);

            // Process the documents
            var processButton = component.Find("button:contains('Process All Pages')");
            await processButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

            // Assert
            _mockOCRService.Verify(x => x.SubmitDocumentAsync(It.IsAny<OCRSubmissionRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            // Should navigate to home after processing
            Assert.Equal("http://localhost/", _navigationManager.Uri);
        }

        [Fact]
        public void DocumentCapture_Should_DisableProcessButton_WhenNoPages()
        {
            // Arrange
            var loginState = new LoginState { IsLoggedIn = true, Username = "testuser" };
            _mockLoginService.Setup(x => x.CurrentLoginState).Returns(loginState);

            // Act
            var component = RenderComponent<DocumentCapture>();

            // Assert
            var processButton = component.Find("button:contains('Process All Pages')");
            Assert.True(processButton.HasAttribute("disabled"));
        }

        [Fact]
        public async Task DocumentCapture_Should_DisableProcessButton_WhileProcessing()
        {
            // Arrange
            var loginState = new LoginState { IsLoggedIn = true, Username = "testuser" };
            _mockLoginService.Setup(x => x.CurrentLoginState).Returns(loginState);

            var testImageData = TestDataBuilder.CreateBase64TestImage();
            var contentHash = "test-hash";
            var processingCompleted = new TaskCompletionSource<bool>();
            
            _mockStorageService.Setup(x => x.StoreAsync(It.IsAny<ContentData<byte[]>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(contentHash);

            _mockStorageService.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ContentData<byte[]>
                {
                    Data = TestDataBuilder.CreateTestImageData(),
                    MimeType = "image/png"
                });

            // Delay OCR processing to check button state during processing
            _mockOCRService.Setup(x => x.SubmitDocumentAsync(It.IsAny<OCRSubmissionRequest>(), It.IsAny<CancellationToken>()))
                .Returns(async () => await processingCompleted.Task);

            // Act
            var component = RenderComponent<DocumentCapture>();
            
            // Add an image
            var cameraCapture = component.FindComponent<NoLock.Social.Components.Camera.CameraCapture>();
            await cameraCapture.InvokeAsync(() => 
                cameraCapture.Instance.OnImageCaptured.InvokeAsync(new CapturedImage { ImageData = testImageData }));

            // Start processing
            var processButton = component.Find("button:contains('Process All Pages')");
            var processTask = processButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

            // Check button state while processing
            component.WaitForAssertion(() =>
            {
                var button = component.Find("button:contains('Process All Pages')");
                Assert.True(button.HasAttribute("disabled"));
                Assert.Contains("spinner-border", button.InnerHtml);
            });

            // Complete processing
            processingCompleted.SetResult(true);
            await processTask;
        }

        #endregion

        #region Image Quality Tests

        [Theory]
        [InlineData(95)]
        [InlineData(70)]
        [InlineData(50)]
        public async Task DocumentCapture_Should_DisplayQualityScore_InFilmStrip(int qualityScore)
        {
            // Arrange
            var loginState = new LoginState { IsLoggedIn = true, Username = "testuser" };
            _mockLoginService.Setup(x => x.CurrentLoginState).Returns(loginState);

            var testImageData = TestDataBuilder.CreateBase64TestImage();
            var contentHash = "test-hash";
            
            _mockStorageService.Setup(x => x.StoreAsync(It.IsAny<ContentData<byte[]>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(contentHash);

            // Act
            var component = RenderComponent<DocumentCapture>();
            var cameraCapture = component.FindComponent<NoLock.Social.Components.Camera.CameraCapture>();
            
            var capturedImage = new CapturedImage
            {
                ImageData = testImageData,
                Quality = qualityScore
            };

            await cameraCapture.InvokeAsync(() => 
                cameraCapture.Instance.OnImageCaptured.InvokeAsync(capturedImage));
            component.Render(); // Force re-render after async operation

            // Assert - FilmStrip displays quality as text, not CSS classes
            Assert.Contains($"Quality: {qualityScore}", component.Markup);
        }

        #endregion


        protected new void Dispose(bool disposing)
        {
            if (disposing)
            {
                _loginStateSubject?.Dispose();
                base.Dispose();
            }
        }
    }
}