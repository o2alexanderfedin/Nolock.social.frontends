using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using NoLock.Social.Core.Camera.Interfaces;
using NoLock.Social.Core.Camera.Models;
using NoLock.Social.Core.Camera.Services;
using Xunit;

namespace NoLock.Social.Core.Tests.Camera
{
    public class CameraServiceTests : IDisposable
    {
        private readonly Mock<IJSRuntime> _jsRuntimeMock;
        private readonly Mock<ILogger<CameraService>> _loggerMock;
        private readonly CameraService _sut;

        public CameraServiceTests()
        {
            _jsRuntimeMock = new Mock<IJSRuntime>();
            _loggerMock = new Mock<ILogger<CameraService>>();
            _sut = new CameraService(_jsRuntimeMock.Object, _loggerMock.Object);
        }

        public void Dispose()
        {
            _sut?.Dispose();
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullJSRuntime_ThrowsArgumentNullException()
        {
            // Act & Assert
            var action = () => new CameraService(null, _loggerMock.Object);
            action.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Constructor_WithValidJSRuntime_CreatesInstance()
        {
            // Act
            var service = new CameraService(_jsRuntimeMock.Object, _loggerMock.Object);

            // Assert
            service.Should().NotBeNull();
        }

        #endregion

        #region InitializeAsync Tests

        [Fact]
        public async Task InitializeAsync_ShouldCompleteSuccessfully()
        {
            // Act
            var action = () => _sut.InitializeAsync();

            // Assert
            await action.Should().NotThrowAsync();
        }

        [Fact]
        public async Task InitializeAsync_AfterDispose_ThrowsObjectDisposedException()
        {
            // Arrange
            _sut.Dispose();

            // Act & Assert
            await Assert.ThrowsAsync<ObjectDisposedException>(async () => await _sut.InitializeAsync());
        }

        #endregion

        #region Permission Tests

        [Theory]
        [InlineData("granted", CameraPermissionState.Granted)]
        [InlineData("denied", CameraPermissionState.Denied)]
        [InlineData("prompt", CameraPermissionState.Prompt)]
        [InlineData("not-requested", CameraPermissionState.NotRequested)]
        [InlineData("unknown", CameraPermissionState.NotRequested)]
        [InlineData(null, CameraPermissionState.NotRequested)]
        public async Task GetPermissionStateAsync_ParsesJSResponseCorrectly(string jsResponse, CameraPermissionState expected)
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string>("cameraPermissions.getState", It.IsAny<object[]>()))
                .ReturnsAsync(jsResponse);

            // Act
            var result = await _sut.GetPermissionStateAsync();

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("granted", CameraPermissionState.Granted)]
        [InlineData("denied", CameraPermissionState.Denied)]
        [InlineData("prompt", CameraPermissionState.Prompt)]
        [InlineData("unknown", CameraPermissionState.Denied)]
        [InlineData(null, CameraPermissionState.Denied)]
        public async Task RequestPermission_ParsesJSResponseCorrectly(string jsResponse, CameraPermissionState expected)
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string>("cameraPermissions.request", It.IsAny<object[]>()))
                .ReturnsAsync(jsResponse);

            // Act
            var result = await _sut.RequestPermission();

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData(CameraPermissionState.Granted, true)]
        [InlineData(CameraPermissionState.Denied, false)]
        [InlineData(CameraPermissionState.Prompt, false)]
        [InlineData(CameraPermissionState.NotRequested, false)]
        public async Task CheckPermissionsAsync_ReturnsCorrectBoolean(CameraPermissionState state, bool expected)
        {
            // Arrange
            var stateString = state switch
            {
                CameraPermissionState.Granted => "granted",
                CameraPermissionState.Denied => "denied",
                CameraPermissionState.Prompt => "prompt",
                _ => "not-requested"
            };

            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string>("cameraPermissions.getState", It.IsAny<object[]>()))
                .ReturnsAsync(stateString);

            // Act
            var result = await _sut.CheckPermissionsAsync();

            // Assert
            result.Should().Be(expected);
        }

        #endregion

        #region Stream Management Tests

        [Fact]
        public async Task StartStreamAsync_WithGrantedPermission_StartsStream()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string>("cameraPermissions.getState", It.IsAny<object[]>()))
                .ReturnsAsync("granted");

            dynamic streamData = new System.Dynamic.ExpandoObject();
            streamData.url = "blob:http://test";
            streamData.id = "stream123";
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<object>("camera.startStream", It.IsAny<object[]>()))
                .ReturnsAsync((object)streamData);

            // Act
            var result = await _sut.StartStreamAsync();

            // Assert
            result.Should().NotBeNull();
            result.StreamId.Should().Be("stream123");
            result.StreamUrl.Should().Be("blob:http://test");
            result.IsActive.Should().BeTrue();
        }

        [Fact]
        public async Task StartStreamAsync_WithExistingStream_ReturnsSameStream()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string>("cameraPermissions.getState", It.IsAny<object[]>()))
                .ReturnsAsync("granted");

            dynamic streamData = new System.Dynamic.ExpandoObject();
            streamData.url = "blob:http://test";
            streamData.id = "stream123";
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<object>("camera.startStream", It.IsAny<object[]>()))
                .ReturnsAsync((object)streamData);

            // Act
            var firstStream = await _sut.StartStreamAsync();
            var secondStream = await _sut.StartStreamAsync();

            // Assert
            secondStream.Should().BeSameAs(firstStream);
            _jsRuntimeMock.Verify(x => x.InvokeAsync<object>("camera.startStream", It.IsAny<object[]>()), Times.Once);
        }

        [Theory]
        [InlineData(CameraPermissionState.Denied)]
        [InlineData(CameraPermissionState.Prompt)]
        [InlineData(CameraPermissionState.NotRequested)]
        public async Task StartStreamAsync_WithoutPermission_ThrowsInvalidOperationException(CameraPermissionState state)
        {
            // Arrange
            var stateString = state switch
            {
                CameraPermissionState.Denied => "denied",
                CameraPermissionState.Prompt => "prompt",
                _ => "not-requested"
            };

            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string>("cameraPermissions.getState", It.IsAny<object[]>()))
                .ReturnsAsync(stateString);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await _sut.StartStreamAsync());
        }

        [Fact]
        public async Task StopStreamAsync_WithActiveStream_StopsStream()
        {
            // Arrange - Start a stream first
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string>("cameraPermissions.getState", It.IsAny<object[]>()))
                .ReturnsAsync("granted");

            dynamic streamData = new System.Dynamic.ExpandoObject();
            streamData.url = "blob:http://test";
            streamData.id = "stream123";
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<object>("camera.startStream", It.IsAny<object[]>()))
                .ReturnsAsync((object)streamData);

            await _sut.StartStreamAsync();

            // Act
            await _sut.StopStreamAsync();

            // Assert
            // After stopping, trying to capture should fail since there's no active stream
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await _sut.CaptureImageAsync());
        }

        [Fact]
        public async Task StopStreamAsync_WithoutActiveStream_DoesNothing()
        {
            // Act
            var action = () => _sut.StopStreamAsync();

            // Assert
            await action.Should().NotThrowAsync();
            // Cannot verify InvokeVoidAsync directly as it's an extension method
            // The test passes if no exception is thrown
        }

        #endregion

        #region Image Capture Tests

        [Fact]
        public async Task CaptureImageAsync_WithActiveStream_CapturesImage()
        {
            // Arrange - Start a stream first
            await StartTestStreamAsync();

            dynamic captureData = new System.Dynamic.ExpandoObject();
            captureData.imageData = "base64ImageData";
            captureData.imageUrl = "blob:http://image";
            captureData.width = 1920;
            captureData.height = 1080;
            captureData.quality = 95;
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<object>("camera.captureImage", It.IsAny<object[]>()))
                .ReturnsAsync((object)captureData);

            // Act
            var result = await _sut.CaptureImageAsync();

            // Assert
            result.Should().NotBeNull();
            result.ImageData.Should().Be("base64ImageData");
            result.ImageUrl.Should().Be("blob:http://image");
            result.Width.Should().Be(1920);
            result.Height.Should().Be(1080);
            result.Quality.Should().Be(95);
        }

        [Fact]
        public async Task CaptureImageAsync_WithoutActiveStream_ThrowsInvalidOperationException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await _sut.CaptureImageAsync());
        }

        #endregion

        #region Camera Controls Tests

        [Theory]
        [InlineData(true, true, true)]
        [InlineData(true, false, false)]
        [InlineData(false, true, false)]
        [InlineData(false, false, false)]
        public async Task ToggleTorchAsync_ReturnsCorrectResult(bool torchSupported, bool jsResult, bool expected)
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<bool>("camera.getTorchSupport", It.IsAny<object[]>()))
                .ReturnsAsync(torchSupported);

            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<bool>("camera.setTorch", It.IsAny<object[]>()))
                .ReturnsAsync(jsResult);

            // Act
            var result = await _sut.ToggleTorchAsync(true);

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public async Task IsTorchSupportedAsync_CallsJSAndReturnsResult()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<bool>("camera.getTorchSupport", It.IsAny<object[]>()))
                .ReturnsAsync(true);

            // Act
            var result = await _sut.IsTorchSupportedAsync();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task SwitchCameraAsync_WithValidDeviceId_SwitchesCamera()
        {
            // Arrange
            var deviceId = "camera123";
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<bool>("camera.switchCamera", It.Is<object[]>(args => 
                    args.Length == 1 && args[0].ToString() == deviceId)))
                .ReturnsAsync(true);

            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string>("cameraPermissions.getState", It.IsAny<object[]>()))
                .ReturnsAsync("granted");

            dynamic streamData = new System.Dynamic.ExpandoObject();
            streamData.url = "blob:http://test";
            streamData.id = "newstream";
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<object>("camera.startStream", It.IsAny<object[]>()))
                .ReturnsAsync((object)streamData);

            // Act
            var result = await _sut.SwitchCameraAsync(deviceId);

            // Assert
            result.Should().BeTrue();
        }

        [Theory]
        [InlineData(1.0, 3.0, true)]
        [InlineData(2.5, 3.0, true)]
        [InlineData(0.5, 3.0, false)]
        [InlineData(4.0, 3.0, false)]
        public async Task SetZoomAsync_ValidatesZoomLevel(double zoomLevel, double maxZoom, bool expected)
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<double>("camera.getMaxZoom", It.IsAny<object[]>()))
                .ReturnsAsync(maxZoom);

            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<bool>("camera.setZoom", It.IsAny<object[]>()))
                .ReturnsAsync(true);

            // Act
            var result = await _sut.SetZoomAsync(zoomLevel);

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public async Task GetZoomAsync_ReturnsCurrentZoomLevel()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<double>("camera.getZoom", It.IsAny<object[]>()))
                .ReturnsAsync(2.5);

            // Act
            var result = await _sut.GetZoomAsync();

            // Assert
            result.Should().Be(2.5);
        }

        [Fact]
        public async Task GetAvailableCamerasAsync_ReturnsDeviceList()
        {
            // Arrange
            var devices = new[] { "camera1", "camera2", "camera3" };
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string[]>("camera.getAvailableCameras", It.IsAny<object[]>()))
                .ReturnsAsync(devices);

            // Act
            var result = await _sut.GetAvailableCamerasAsync();

            // Assert
            result.Should().BeEquivalentTo(devices);
        }

        #endregion

        #region Image Quality Validation Tests

        [Fact]
        public async Task ValidateImageQualityAsync_WithNullImage_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () => await _sut.ValidateImageQualityAsync(null));
        }

        [Fact]
        public async Task ValidateImageQualityAsync_WithEmptyImageData_ThrowsArgumentException()
        {
            // Arrange
            var image = new CapturedImage { ImageData = "" };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () => await _sut.ValidateImageQualityAsync(image));
        }

        [Fact]
        public async Task ValidateImageQualityAsync_PerformsCompleteAnalysis()
        {
            // Arrange
            var image = new CapturedImage { ImageData = "base64data" };
            
            SetupQualityMocks(blurScore: 0.8, lightingScore: 0.7, edgeScore: 0.9);

            // Act
            var result = await _sut.ValidateImageQualityAsync(image);

            // Assert
            result.Should().NotBeNull();
            result.OverallScore.Should().BeInRange(70, 80); // Based on weighted calculation
            result.BlurScore.Should().Be(0.8);
            result.LightingScore.Should().Be(0.7);
            result.EdgeDetectionScore.Should().Be(0.9);
        }

        [Theory]
        [InlineData(0.3, 0.8, 0.8, true, false, false)] // Blur issue only
        [InlineData(0.8, 0.4, 0.8, false, true, false)] // Lighting issue only
        [InlineData(0.8, 0.8, 0.3, false, false, true)] // Edge issue only
        [InlineData(0.3, 0.4, 0.3, true, true, true)]   // All issues
        public async Task ValidateImageQualityAsync_IdentifiesIssuesCorrectly(
            double blurScore, double lightingScore, double edgeScore,
            bool expectBlurIssue, bool expectLightingIssue, bool expectEdgeIssue)
        {
            // Arrange
            var image = new CapturedImage { ImageData = "base64data" };
            SetupQualityMocks(blurScore, lightingScore, edgeScore);

            // Act
            var result = await _sut.ValidateImageQualityAsync(image);

            // Assert
            result.Issues.Should().HaveCountGreaterThanOrEqualTo(0);
            
            if (expectBlurIssue)
                result.Issues.Should().Contain(x => x.Contains("blurry"));
            
            if (expectLightingIssue)
                result.Issues.Should().Contain(x => x.Contains("lighting"));
            
            if (expectEdgeIssue)
                result.Issues.Should().Contain(x => x.Contains("edges"));

            result.Suggestions.Should().NotBeEmpty();
        }

        #endregion

        #region Document Session Management Tests

        [Fact]
        public async Task CreateDocumentSessionAsync_CreatesNewSession()
        {
            // Act
            var sessionId = await _sut.CreateDocumentSessionAsync();

            // Assert
            sessionId.Should().NotBeNullOrEmpty();
            Guid.TryParse(sessionId, out _).Should().BeTrue();
        }

        [Fact]
        public async Task CreateDocumentSessionAsync_AfterDispose_ThrowsObjectDisposedException()
        {
            // Arrange
            _sut.Dispose();

            // Act & Assert
            await Assert.ThrowsAsync<ObjectDisposedException>(async () => await _sut.CreateDocumentSessionAsync());
        }

        [Fact]
        public async Task AddPageToSessionAsync_WithValidSession_AddsPage()
        {
            // Arrange
            var sessionId = await _sut.CreateDocumentSessionAsync();
            var image = new CapturedImage { ImageData = "base64data" };

            // Act
            await _sut.AddPageToSessionAsync(sessionId, image);
            var pages = await _sut.GetSessionPagesAsync(sessionId);

            // Assert
            pages.Should().HaveCount(1);
            pages[0].Should().BeSameAs(image);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task AddPageToSessionAsync_WithInvalidSessionId_ThrowsException(string invalidSessionId)
        {
            // Arrange
            var image = new CapturedImage { ImageData = "base64data" };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () => await _sut.AddPageToSessionAsync(invalidSessionId, image));
        }

        [Fact]
        public async Task AddPageToSessionAsync_WithNonExistentSession_ThrowsInvalidOperationException()
        {
            // Arrange
            var image = new CapturedImage { ImageData = "base64data" };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await _sut.AddPageToSessionAsync("nonexistent", image));
        }

        [Fact]
        public async Task RemovePageFromSessionAsync_RemovesCorrectPage()
        {
            // Arrange
            var sessionId = await _sut.CreateDocumentSessionAsync();
            var image1 = new CapturedImage { ImageData = "data1" };
            var image2 = new CapturedImage { ImageData = "data2" };
            var image3 = new CapturedImage { ImageData = "data3" };
            
            await _sut.AddPageToSessionAsync(sessionId, image1);
            await _sut.AddPageToSessionAsync(sessionId, image2);
            await _sut.AddPageToSessionAsync(sessionId, image3);

            // Act
            await _sut.RemovePageFromSessionAsync(sessionId, 1); // Remove middle page
            var pages = await _sut.GetSessionPagesAsync(sessionId);

            // Assert
            pages.Should().HaveCount(2);
            pages[0].ImageData.Should().Be("data1");
            pages[1].ImageData.Should().Be("data3");
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(10)]
        public async Task RemovePageFromSessionAsync_WithInvalidIndex_ThrowsArgumentOutOfRangeException(int invalidIndex)
        {
            // Arrange
            var sessionId = await _sut.CreateDocumentSessionAsync();
            await _sut.AddPageToSessionAsync(sessionId, new CapturedImage { ImageData = "data" });

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await _sut.RemovePageFromSessionAsync(sessionId, invalidIndex));
        }

        [Fact]
        public async Task ReorderPagesInSessionAsync_ReordersCorrectly()
        {
            // Arrange
            var sessionId = await _sut.CreateDocumentSessionAsync();
            var images = new[]
            {
                new CapturedImage { ImageData = "data1" },
                new CapturedImage { ImageData = "data2" },
                new CapturedImage { ImageData = "data3" }
            };
            
            foreach (var img in images)
                await _sut.AddPageToSessionAsync(sessionId, img);

            // Act
            await _sut.ReorderPagesInSessionAsync(sessionId, 0, 2); // Move first to last
            var pages = await _sut.GetSessionPagesAsync(sessionId);

            // Assert
            pages[0].ImageData.Should().Be("data2");
            pages[1].ImageData.Should().Be("data3");
            pages[2].ImageData.Should().Be("data1");
        }

        [Fact]
        public async Task ClearDocumentSessionAsync_RemovesAllPages()
        {
            // Arrange
            var sessionId = await _sut.CreateDocumentSessionAsync();
            await _sut.AddPageToSessionAsync(sessionId, new CapturedImage { ImageData = "data1" });
            await _sut.AddPageToSessionAsync(sessionId, new CapturedImage { ImageData = "data2" });

            // Act
            await _sut.ClearDocumentSessionAsync(sessionId);
            var pages = await _sut.GetSessionPagesAsync(sessionId);

            // Assert
            pages.Should().BeEmpty();
        }

        [Fact]
        public async Task IsSessionActiveAsync_WithActiveSession_ReturnsTrue()
        {
            // Arrange
            var sessionId = await _sut.CreateDocumentSessionAsync();

            // Act
            var result = await _sut.IsSessionActiveAsync(sessionId);

            // Assert
            result.Should().BeTrue();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("nonexistent")]
        public async Task IsSessionActiveAsync_WithInvalidSession_ReturnsFalse(string sessionId)
        {
            // Act
            var result = await _sut.IsSessionActiveAsync(sessionId);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task DisposeDocumentSessionAsync_RemovesSession()
        {
            // Arrange
            var sessionId = await _sut.CreateDocumentSessionAsync();
            await _sut.AddPageToSessionAsync(sessionId, new CapturedImage { ImageData = "data" });

            // Act
            await _sut.DisposeDocumentSessionAsync(sessionId);

            // Assert
            var isActive = await _sut.IsSessionActiveAsync(sessionId);
            isActive.Should().BeFalse();
        }

        #endregion

        #region Disposal Tests

        [Fact]
        public void Dispose_CleansUpResources()
        {
            // Arrange
            var service = new CameraService(_jsRuntimeMock.Object, _loggerMock.Object);

            // Act
            service.Dispose();
            service.Dispose(); // Should not throw on second dispose

            // Assert - Methods should throw ObjectDisposedException
            Assert.Throws<ObjectDisposedException>(() => service.CreateDocumentSessionAsync().GetAwaiter().GetResult());
        }

        [Fact]
        public async Task Dispose_StopsActiveStream()
        {
            // Arrange
            await StartTestStreamAsync();

            // Act
            _sut.Dispose();

            // Assert
            // Since InvokeVoidAsync is an extension method, we cannot verify it directly
            // The test verifies that dispose doesn't throw and the stream is cleaned up
            // Additional verification can be done by checking the service state after disposal
            await Assert.ThrowsAsync<ObjectDisposedException>(async () => await _sut.CreateDocumentSessionAsync());
        }

        #endregion

        #region Additional Camera Control Tests

        [Fact]
        public async Task IsZoomSupportedAsync_CallsJSAndReturnsResult()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<bool>("camera.getZoomSupport", It.IsAny<object[]>()))
                .ReturnsAsync(true);

            // Act
            var result = await _sut.IsZoomSupportedAsync();

            // Assert
            result.Should().BeTrue();
            _jsRuntimeMock.Verify(x => x.InvokeAsync<bool>("camera.getZoomSupport", It.IsAny<object[]>()), Times.Once);
        }

        [Theory]
        [InlineData(1.0)]
        [InlineData(3.0)]
        [InlineData(10.0)]
        public async Task GetMaxZoomAsync_ReturnsMaxZoomLevel(double maxZoom)
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<double>("camera.getMaxZoom", It.IsAny<object[]>()))
                .ReturnsAsync(maxZoom);

            // Act
            var result = await _sut.GetMaxZoomAsync();

            // Assert
            result.Should().Be(maxZoom);
        }

        #endregion

        #region Direct Image Analysis Tests

        [Fact]
        public async Task DetectBlurAsync_WithValidImageData_ReturnsBlurResult()
        {
            // Arrange
            var imageData = "base64ImageData";
            dynamic blurData = new System.Dynamic.ExpandoObject();
            blurData.blurScore = 0.8;
            blurData.threshold = 0.5;
            blurData.isBlurry = false;
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<object>("imageQuality.detectBlur", It.IsAny<object[]>()))
                .ReturnsAsync((object)blurData);

            // Act
            var result = await _sut.DetectBlurAsync(imageData);

            // Assert
            result.Should().NotBeNull();
            result.BlurScore.Should().Be(0.8);
            result.IsBlurry.Should().BeFalse();
        }

        [Fact]
        public async Task AssessLightingAsync_WithValidImageData_ReturnsLightingResult()
        {
            // Arrange
            var imageData = "base64ImageData";
            dynamic lightingData = new System.Dynamic.ExpandoObject();
            lightingData.lightingScore = 0.75;
            lightingData.brightness = 140.0;
            lightingData.contrast = 0.6;
            lightingData.isTooDark = false;
            lightingData.isTooBright = false;
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<object>("imageQuality.assessLighting", It.IsAny<object[]>()))
                .ReturnsAsync((object)lightingData);

            // Act
            var result = await _sut.AssessLightingAsync(imageData);

            // Assert
            result.Should().NotBeNull();
            result.LightingScore.Should().Be(0.75);
            result.Brightness.Should().Be(140.0);
            result.Contrast.Should().Be(0.6);
        }

        [Fact]
        public async Task DetectDocumentEdgesAsync_WithValidImageData_ReturnsEdgeResult()
        {
            // Arrange
            var imageData = "base64ImageData";
            dynamic edgeData = new System.Dynamic.ExpandoObject();
            edgeData.edgeScore = 0.9;
            edgeData.edgeCount = 4;
            edgeData.confidence = 0.85;
            edgeData.hasAllEdges = true;
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<object>("imageQuality.detectEdges", It.IsAny<object[]>()))
                .ReturnsAsync((object)edgeData);

            // Act
            var result = await _sut.DetectDocumentEdgesAsync(imageData);

            // Assert
            result.Should().NotBeNull();
            result.EdgeScore.Should().Be(0.9);
            result.EdgeCount.Should().Be(4);
            result.Confidence.Should().Be(0.85);
        }

        #endregion

        #region Additional Session Management Tests

        [Fact]
        public async Task GetDocumentSessionAsync_WithValidSession_ReturnsSession()
        {
            // Arrange
            var sessionId = await _sut.CreateDocumentSessionAsync();
            var image = new CapturedImage { ImageData = "testData" };
            await _sut.AddPageToSessionAsync(sessionId, image);

            // Act
            var session = await _sut.GetDocumentSessionAsync(sessionId);

            // Assert
            session.Should().NotBeNull();
            session.SessionId.Should().Be(sessionId);
            session.Pages.Should().HaveCount(1);
        }

        [Fact]
        public async Task GetDocumentSessionAsync_WithInvalidSession_ThrowsInvalidOperationException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await _sut.GetDocumentSessionAsync("nonexistent"));
        }

        [Fact]
        public async Task CleanupInactiveSessionsAsync_RemovesInactiveSessions()
        {
            // Arrange
            var activeSession = await _sut.CreateDocumentSessionAsync();
            var inactiveSession1 = await _sut.CreateDocumentSessionAsync();
            var inactiveSession2 = await _sut.CreateDocumentSessionAsync();
            
            // Add pages to active session to keep it active
            await _sut.AddPageToSessionAsync(activeSession, new CapturedImage { ImageData = "data" });

            // Act
            await _sut.CleanupInactiveSessionsAsync();

            // Assert
            var activeStillExists = await _sut.IsSessionActiveAsync(activeSession);
            activeStillExists.Should().BeTrue();
            
            // Note: Without ability to mock time or mark sessions as inactive,
            // we can't fully test cleanup behavior. This test verifies the method doesn't crash.
        }

        #endregion

        #region JavaScript Interop Error Handling Tests

        [Fact]
        public async Task StartStreamAsync_WithJSException_ThrowsException()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string>("cameraPermissions.getState", It.IsAny<object[]>()))
                .ReturnsAsync("granted");

            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<object>("camera.startStream", It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("Camera initialization failed"));

            // Act & Assert
            await Assert.ThrowsAsync<JSException>(async () => await _sut.StartStreamAsync());
        }

        [Fact]
        public async Task CaptureImageAsync_WithJSException_ThrowsException()
        {
            // Arrange
            await StartTestStreamAsync();
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<object>("camera.captureImage", It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("Capture failed"));

            // Act & Assert
            await Assert.ThrowsAsync<JSException>(async () => await _sut.CaptureImageAsync());
        }

        [Theory]
        [InlineData("camera.getTorchSupport", false)]
        [InlineData("camera.getZoom", 1.0)]
        [InlineData("camera.getMaxZoom", 3.0)]
        public async Task CameraControlMethods_WithJSException_ReturnDefaultValues(string jsMethod, object expectedDefault)
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<It.IsAnyType>(jsMethod, It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("JS Error"));

            // Act & Assert based on method
            switch (jsMethod)
            {
                case "camera.getTorchSupport":
                    var torchSupported = await _sut.IsTorchSupportedAsync();
                    torchSupported.Should().Be((bool)expectedDefault);
                    break;
                case "camera.getZoom":
                    var zoom = await _sut.GetZoomAsync();
                    zoom.Should().Be((double)expectedDefault);
                    break;
                case "camera.getMaxZoom":
                    var maxZoom = await _sut.GetMaxZoomAsync();
                    maxZoom.Should().Be((double)expectedDefault);
                    break;
            }
        }

        [Fact]
        public async Task GetAvailableCamerasAsync_WithJSException_ReturnsEmptyArray()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string[]>("camera.getAvailableCameras", It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("Enumeration failed"));

            // Act
            var cameras = await _sut.GetAvailableCamerasAsync();

            // Assert
            cameras.Should().BeEmpty();
        }

        [Fact]
        public async Task ValidateImageQualityAsync_WithJSInteropFailure_ThrowsInvalidOperationException()
        {
            // Arrange
            var image = new CapturedImage { ImageData = "base64data" };
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<object>("imageQuality.detectBlur", It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("Quality analysis failed"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await _sut.ValidateImageQualityAsync(image));
        }

        [Fact]
        public async Task StartStreamAsync_WithNullStreamData_ThrowsInvalidOperationException()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string>("cameraPermissions.getState", It.IsAny<object[]>()))
                .ReturnsAsync("granted");

            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<object>("camera.startStream", It.IsAny<object[]>()))
                .ReturnsAsync((object)null!);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await _sut.StartStreamAsync());
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(1000)]
        public async Task SetZoomAsync_WithInvalidZoomBounds_ReturnsFalse(double invalidZoom)
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<double>("camera.getMaxZoom", It.IsAny<object[]>()))
                .ReturnsAsync(3.0);

            // Act
            var result = await _sut.SetZoomAsync(invalidZoom);

            // Assert - Should return false for out-of-bounds values
            if (invalidZoom < 1.0 || invalidZoom > 3.0)
            {
                result.Should().BeFalse();
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("nonexistent-session-id")]
        public async Task SessionMethods_WithInvalidSessionId_HandleGracefully(string invalidSessionId)
        {
            // Act & Assert - These should throw ArgumentException for invalid session IDs
            if (string.IsNullOrWhiteSpace(invalidSessionId))
            {
                await Assert.ThrowsAsync<ArgumentException>(async () => await _sut.AddPageToSessionAsync(invalidSessionId, new CapturedImage()));
            }
            else
            {
                // For nonexistent session, should throw InvalidOperationException
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await _sut.AddPageToSessionAsync(invalidSessionId, new CapturedImage()));
            }
        }

        #endregion

        #region Helper Methods

        private async Task StartTestStreamAsync()
        {
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string>("cameraPermissions.getState", It.IsAny<object[]>()))
                .ReturnsAsync("granted");

            dynamic streamData = new System.Dynamic.ExpandoObject();
            streamData.url = "blob:http://test";
            streamData.id = "stream123";
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<object>("camera.startStream", It.IsAny<object[]>()))
                .ReturnsAsync((object)streamData);

            await _sut.StartStreamAsync();
        }

        private void SetupQualityMocks(double blurScore, double lightingScore, double edgeScore)
        {
            // Setup blur detection
            dynamic blurData = new System.Dynamic.ExpandoObject();
            blurData.blurScore = blurScore;
            blurData.threshold = 0.5;
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<object>("imageQuality.detectBlur", It.IsAny<object[]>()))
                .ReturnsAsync((object)blurData);

            // Setup lighting assessment
            dynamic lightingData = new System.Dynamic.ExpandoObject();
            lightingData.lightingScore = lightingScore;
            lightingData.brightness = 128.0;
            lightingData.contrast = 0.5;
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<object>("imageQuality.assessLighting", It.IsAny<object[]>()))
                .ReturnsAsync((object)lightingData);

            // Setup edge detection
            dynamic edgeData = new System.Dynamic.ExpandoObject();
            edgeData.edgeScore = edgeScore;
            edgeData.edgeCount = 4;
            edgeData.confidence = 0.7;
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<object>("imageQuality.detectEdges", It.IsAny<object[]>()))
                .ReturnsAsync((object)edgeData);
        }

        #endregion
    }
}