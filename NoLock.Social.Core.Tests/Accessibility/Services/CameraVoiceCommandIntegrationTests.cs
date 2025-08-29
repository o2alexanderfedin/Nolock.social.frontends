using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using NoLock.Social.Core.Accessibility.Interfaces;
using NoLock.Social.Core.Accessibility.Services;
using NoLock.Social.Core.Camera.Interfaces;
using NoLock.Social.Core.Camera.Models;
using Xunit;

namespace NoLock.Social.Core.Tests.Accessibility.Services
{
    public class CameraVoiceCommandIntegrationTests : IDisposable
    {
        private readonly Mock<IVoiceCommandService> _voiceCommandServiceMock;
        private readonly Mock<ICameraService> _cameraServiceMock;
        private readonly Mock<ILogger<CameraVoiceCommandIntegration>> _loggerMock;
        private readonly CameraVoiceCommandIntegration _integration;

        public CameraVoiceCommandIntegrationTests()
        {
            _voiceCommandServiceMock = new Mock<IVoiceCommandService>();
            _cameraServiceMock = new Mock<ICameraService>();
            _loggerMock = new Mock<ILogger<CameraVoiceCommandIntegration>>();
            
            _integration = new CameraVoiceCommandIntegration(
                _voiceCommandServiceMock.Object,
                _cameraServiceMock.Object,
                _loggerMock.Object
            );
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_ShouldThrowArgumentNullException_WhenVoiceCommandServiceIsNull()
        {
            // Act & Assert
            var act = () => new CameraVoiceCommandIntegration(
                null!,
                _cameraServiceMock.Object,
                _loggerMock.Object
            );

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("voiceCommandService");
        }

        [Fact]
        public void Constructor_ShouldThrowArgumentNullException_WhenCameraServiceIsNull()
        {
            // Act & Assert
            var act = () => new CameraVoiceCommandIntegration(
                _voiceCommandServiceMock.Object,
                null!,
                _loggerMock.Object
            );

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("cameraService");
        }

        [Fact]
        public void Constructor_ShouldThrowArgumentNullException_WhenLoggerIsNull()
        {
            // Act & Assert
            var act = () => new CameraVoiceCommandIntegration(
                _voiceCommandServiceMock.Object,
                _cameraServiceMock.Object,
                null!
            );

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("logger");
        }

        #endregion

        #region InitializeAsync Tests

        [Fact]
        public async Task InitializeAsync_ShouldRegisterAllCommands_Successfully()
        {
            // Arrange
            Dictionary<string, Func<Task>>? registeredCommands = null;
            
            _voiceCommandServiceMock
                .Setup(x => x.SetCommandsAsync(It.IsAny<Dictionary<string, Func<Task>>>()))
                .Callback<Dictionary<string, Func<Task>>>(commands => registeredCommands = commands)
                .Returns(Task.CompletedTask);

            // Act
            await _integration.InitializeAsync();

            // Assert
            registeredCommands.Should().NotBeNull();
            registeredCommands!.Should().ContainKeys(
                "capture", "take photo", "snap", "take picture",
                "start camera", "begin scanning", "activate camera",
                "stop camera", "end scanning", "deactivate camera",
                "torch on", "flash on", "light on",
                "torch off", "flash off", "light off",
                "zoom in", "zoom out", "reset zoom",
                "switch camera", "flip camera", "change camera"
            );
        }

        [Fact]
        public async Task InitializeAsync_ShouldSubscribeToEvents()
        {
            // Arrange
            _voiceCommandServiceMock
                .Setup(x => x.SetCommandsAsync(It.IsAny<Dictionary<string, Func<Task>>>()))
                .Returns(Task.CompletedTask);

            // Act
            await _integration.InitializeAsync();
            
            // Assert - Verify event subscriptions by raising events
            _voiceCommandServiceMock.Raise(
                x => x.OnCommandRecognized += null,
                this,
                new VoiceCommandEventArgs
                {
                    RecognizedText = "test",
                    MatchedCommand = "test",
                    Confidence = 0.9
                });
            
            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Voice command recognized")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task InitializeAsync_ShouldLogWarning_WhenAlreadyInitialized()
        {
            // Arrange
            _voiceCommandServiceMock
                .Setup(x => x.SetCommandsAsync(It.IsAny<Dictionary<string, Func<Task>>>()))
                .Returns(Task.CompletedTask);

            await _integration.InitializeAsync();

            // Act
            await _integration.InitializeAsync();

            // Assert
            _loggerMock.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("already initialized")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task InitializeAsync_ShouldThrowObjectDisposedException_WhenDisposed()
        {
            // Arrange
            await _integration.DisposeAsync();

            // Act
            var act = async () => await _integration.InitializeAsync();

            // Assert
            await act.Should().ThrowAsync<ObjectDisposedException>();
        }

        #endregion

        #region StartListeningAsync Tests

        [Fact]
        public async Task StartListeningAsync_ShouldInitializeFirst_WhenNotInitialized()
        {
            // Arrange
            _voiceCommandServiceMock
                .Setup(x => x.SetCommandsAsync(It.IsAny<Dictionary<string, Func<Task>>>()))
                .Returns(Task.CompletedTask);
            
            _voiceCommandServiceMock
                .Setup(x => x.StartListeningAsync())
                .Returns(Task.CompletedTask);

            // Act
            await _integration.StartListeningAsync();

            // Assert
            _voiceCommandServiceMock.Verify(x => x.SetCommandsAsync(It.IsAny<Dictionary<string, Func<Task>>>()), Times.Once);
            _voiceCommandServiceMock.Verify(x => x.StartListeningAsync(), Times.Once);
        }

        [Fact]
        public async Task StartListeningAsync_ShouldStartListening_Successfully()
        {
            // Arrange
            _voiceCommandServiceMock
                .Setup(x => x.SetCommandsAsync(It.IsAny<Dictionary<string, Func<Task>>>()))
                .Returns(Task.CompletedTask);
            
            _voiceCommandServiceMock
                .Setup(x => x.StartListeningAsync())
                .Returns(Task.CompletedTask);
            
            await _integration.InitializeAsync();

            // Act
            await _integration.StartListeningAsync();

            // Assert
            _voiceCommandServiceMock.Verify(x => x.StartListeningAsync(), Times.Once);
        }

        [Fact]
        public async Task StartListeningAsync_ShouldThrowObjectDisposedException_WhenDisposed()
        {
            // Arrange
            await _integration.DisposeAsync();

            // Act
            var act = async () => await _integration.StartListeningAsync();

            // Assert
            await act.Should().ThrowAsync<ObjectDisposedException>();
        }

        #endregion

        #region StopListeningAsync Tests

        [Fact]
        public async Task StopListeningAsync_ShouldStopListening_Successfully()
        {
            // Arrange
            _voiceCommandServiceMock
                .Setup(x => x.StopListeningAsync())
                .Returns(Task.CompletedTask);

            // Act
            await _integration.StopListeningAsync();

            // Assert
            _voiceCommandServiceMock.Verify(x => x.StopListeningAsync(), Times.Once);
        }

        [Fact]
        public async Task StopListeningAsync_ShouldThrowObjectDisposedException_WhenDisposed()
        {
            // Arrange
            await _integration.DisposeAsync();

            // Act
            var act = async () => await _integration.StopListeningAsync();

            // Assert
            await act.Should().ThrowAsync<ObjectDisposedException>();
        }

        #endregion

        #region Voice Command Execution Tests

        [Theory]
        [InlineData("capture")]
        [InlineData("take photo")]
        [InlineData("snap")]
        [InlineData("take picture")]
        public async Task CaptureCommands_ShouldCallCaptureImage(string command)
        {
            // Arrange
            var captureCommand = await SetupAndGetCommand(command);
            
            _cameraServiceMock
                .Setup(x => x.CaptureImageAsync())
                .ReturnsAsync(new CapturedImage { ImageData = "image-data" });

            // Act
            await captureCommand();

            // Assert
            _cameraServiceMock.Verify(x => x.CaptureImageAsync(), Times.Once);
        }

        [Theory]
        [InlineData("start camera")]
        [InlineData("begin scanning")]
        [InlineData("activate camera")]
        public async Task StartCameraCommands_ShouldCallStartStream(string command)
        {
            // Arrange
            var startCommand = await SetupAndGetCommand(command);
            
            _cameraServiceMock
                .Setup(x => x.StartStreamAsync())
                .ReturnsAsync(new CameraStream { DeviceId = "test" });

            // Act
            await startCommand();

            // Assert
            _cameraServiceMock.Verify(x => x.StartStreamAsync(), Times.Once);
        }

        [Theory]
        [InlineData("stop camera")]
        [InlineData("end scanning")]
        [InlineData("deactivate camera")]
        public async Task StopCameraCommands_ShouldCallStopStream(string command)
        {
            // Arrange
            var stopCommand = await SetupAndGetCommand(command);
            
            _cameraServiceMock
                .Setup(x => x.StopStreamAsync())
                .Returns(Task.CompletedTask);

            // Act
            await stopCommand();

            // Assert
            _cameraServiceMock.Verify(x => x.StopStreamAsync(), Times.Once);
        }

        [Theory]
        [InlineData("torch on", true)]
        [InlineData("flash on", true)]
        [InlineData("light on", true)]
        [InlineData("torch off", false)]
        [InlineData("flash off", false)]
        [InlineData("light off", false)]
        public async Task TorchCommands_ShouldToggleTorch(string command, bool enabled)
        {
            // Arrange
            var torchCommand = await SetupAndGetCommand(command);
            
            _cameraServiceMock
                .Setup(x => x.ToggleTorchAsync(enabled))
                .ReturnsAsync(true);

            // Act
            await torchCommand();

            // Assert
            _cameraServiceMock.Verify(x => x.ToggleTorchAsync(enabled), Times.Once);
        }

        [Fact]
        public async Task ZoomIn_ShouldIncreaseZoom()
        {
            // Arrange
            var zoomInCommand = await SetupAndGetCommand("zoom in");
            
            _cameraServiceMock
                .Setup(x => x.GetZoomAsync())
                .ReturnsAsync(1.5);
            
            _cameraServiceMock
                .Setup(x => x.SetZoomAsync(2.0))
                .ReturnsAsync(true);

            // Act
            await zoomInCommand();

            // Assert
            _cameraServiceMock.Verify(x => x.GetZoomAsync(), Times.Once);
            _cameraServiceMock.Verify(x => x.SetZoomAsync(2.0), Times.Once);
        }

        [Fact]
        public async Task ZoomIn_ShouldNotExceedMaxZoom()
        {
            // Arrange
            var zoomInCommand = await SetupAndGetCommand("zoom in");
            
            _cameraServiceMock
                .Setup(x => x.GetZoomAsync())
                .ReturnsAsync(2.8);
            
            _cameraServiceMock
                .Setup(x => x.SetZoomAsync(3.0))
                .ReturnsAsync(true);

            // Act
            await zoomInCommand();

            // Assert
            _cameraServiceMock.Verify(x => x.SetZoomAsync(3.0), Times.Once);
        }

        [Fact]
        public async Task ZoomOut_ShouldDecreaseZoom()
        {
            // Arrange
            var zoomOutCommand = await SetupAndGetCommand("zoom out");
            
            _cameraServiceMock
                .Setup(x => x.GetZoomAsync())
                .ReturnsAsync(2.0);
            
            _cameraServiceMock
                .Setup(x => x.SetZoomAsync(1.5))
                .ReturnsAsync(true);

            // Act
            await zoomOutCommand();

            // Assert
            _cameraServiceMock.Verify(x => x.GetZoomAsync(), Times.Once);
            _cameraServiceMock.Verify(x => x.SetZoomAsync(1.5), Times.Once);
        }

        [Fact]
        public async Task ZoomOut_ShouldNotGoBelowMinZoom()
        {
            // Arrange
            var zoomOutCommand = await SetupAndGetCommand("zoom out");
            
            _cameraServiceMock
                .Setup(x => x.GetZoomAsync())
                .ReturnsAsync(1.2);
            
            _cameraServiceMock
                .Setup(x => x.SetZoomAsync(1.0))
                .ReturnsAsync(true);

            // Act
            await zoomOutCommand();

            // Assert
            _cameraServiceMock.Verify(x => x.SetZoomAsync(1.0), Times.Once);
        }

        [Fact]
        public async Task ResetZoom_ShouldSetZoomToOne()
        {
            // Arrange
            var resetCommand = await SetupAndGetCommand("reset zoom");
            
            _cameraServiceMock
                .Setup(x => x.SetZoomAsync(1.0))
                .ReturnsAsync(true);

            // Act
            await resetCommand();

            // Assert
            _cameraServiceMock.Verify(x => x.SetZoomAsync(1.0), Times.Once);
        }

        [Theory]
        [InlineData("switch camera")]
        [InlineData("flip camera")]
        [InlineData("change camera")]
        public async Task SwitchCamera_ShouldSwitchToNextCamera_WhenMultipleCamerasAvailable(string command)
        {
            // Arrange
            var switchCommand = await SetupAndGetCommand(command);
            
            var cameras = new[]
            {
                "camera1",
                "camera2"
            };
            
            _cameraServiceMock
                .Setup(x => x.GetAvailableCamerasAsync())
                .ReturnsAsync(cameras);
            
            _cameraServiceMock
                .Setup(x => x.SwitchCameraAsync(cameras[1]))
                .ReturnsAsync(true);

            // Act
            await switchCommand();

            // Assert
            _cameraServiceMock.Verify(x => x.SwitchCameraAsync(cameras[1]), Times.Once);
        }

        [Fact]
        public async Task SwitchCamera_ShouldLogWarning_WhenOnlyOneCameraAvailable()
        {
            // Arrange
            var switchCommand = await SetupAndGetCommand("switch camera");
            
            var cameras = new[]
            {
                "camera1"
            };
            
            _cameraServiceMock
                .Setup(x => x.GetAvailableCamerasAsync())
                .ReturnsAsync(cameras);

            // Act
            await switchCommand();

            // Assert
            _cameraServiceMock.Verify(x => x.SwitchCameraAsync(It.IsAny<string>()), Times.Never);
            _loggerMock.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("only one camera available")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region Event Handler Tests

        [Fact]
        public async Task OnVoiceCommandRecognized_ShouldLogInformation()
        {
            // Arrange
            _voiceCommandServiceMock
                .Setup(x => x.SetCommandsAsync(It.IsAny<Dictionary<string, Func<Task>>>()))
                .Returns(Task.CompletedTask);
            
            await _integration.InitializeAsync();

            // Act
            _voiceCommandServiceMock.Raise(
                x => x.OnCommandRecognized += null,
                this,
                new VoiceCommandEventArgs
                {
                    RecognizedText = "take a photo",
                    MatchedCommand = "take photo",
                    Confidence = 0.95
                });

            // Assert
            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains("Voice command recognized") &&
                    v.ToString()!.Contains("take photo") &&
                    v.ToString()!.Contains("0.95")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task OnSpeechError_ShouldLogError()
        {
            // Arrange
            _voiceCommandServiceMock
                .Setup(x => x.SetCommandsAsync(It.IsAny<Dictionary<string, Func<Task>>>()))
                .Returns(Task.CompletedTask);
            
            await _integration.InitializeAsync();

            // Act
            _voiceCommandServiceMock.Raise(
                x => x.OnSpeechError += null,
                this,
                new SpeechErrorEventArgs
                {
                    ErrorMessage = "Network error",
                    ErrorCode = "NETWORK_ERROR"
                });

            // Assert
            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains("Speech recognition error") &&
                    v.ToString()!.Contains("NETWORK_ERROR") &&
                    v.ToString()!.Contains("Network error")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region DisposeAsync Tests

        [Fact]
        public async Task DisposeAsync_ShouldUnsubscribeEvents_WhenInitialized()
        {
            // Arrange
            _voiceCommandServiceMock
                .Setup(x => x.SetCommandsAsync(It.IsAny<Dictionary<string, Func<Task>>>()))
                .Returns(Task.CompletedTask);
            
            _voiceCommandServiceMock
                .Setup(x => x.IsListeningAsync())
                .ReturnsAsync(false);
            
            await _integration.InitializeAsync();

            // Act
            await _integration.DisposeAsync();
            
            // Try to raise event after disposal
            _voiceCommandServiceMock.Raise(
                x => x.OnCommandRecognized += null,
                this,
                new VoiceCommandEventArgs { RecognizedText = "test", MatchedCommand = "test", Confidence = 0.9 });

            // Assert - Should not log after disposal (event should be unsubscribed)
            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Voice command recognized")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        [Fact]
        public async Task DisposeAsync_ShouldStopListening_WhenListening()
        {
            // Arrange
            _voiceCommandServiceMock
                .Setup(x => x.SetCommandsAsync(It.IsAny<Dictionary<string, Func<Task>>>()))
                .Returns(Task.CompletedTask);
            
            _voiceCommandServiceMock
                .Setup(x => x.IsListeningAsync())
                .ReturnsAsync(true);
            
            _voiceCommandServiceMock
                .Setup(x => x.StopListeningAsync())
                .Returns(Task.CompletedTask);
            
            await _integration.InitializeAsync();

            // Act
            await _integration.DisposeAsync();

            // Assert
            _voiceCommandServiceMock.Verify(x => x.StopListeningAsync(), Times.Once);
        }

        [Fact]
        public async Task DisposeAsync_ShouldBeIdempotent()
        {
            // Act & Assert
            await _integration.DisposeAsync();
            await _integration.DisposeAsync(); // Should not throw
        }

        #endregion

        #region Error Handling and Edge Case Tests

        [Fact]
        public async Task CameraCommandExecution_WithCameraServiceException_HandlesGracefully()
        {
            // Arrange
            var captureCommand = await SetupAndGetCommand("capture");
            
            _cameraServiceMock
                .Setup(x => x.CaptureImageAsync())
                .ThrowsAsync(new InvalidOperationException("Camera not available"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => captureCommand());
        }

        [Fact]
        public async Task ZoomCommands_WithZoomServiceException_HandlesGracefully()
        {
            // Arrange
            var zoomInCommand = await SetupAndGetCommand("zoom in");
            
            _cameraServiceMock
                .Setup(x => x.GetZoomAsync())
                .ThrowsAsync(new JSException("JS Error"));

            // Act & Assert
            await Assert.ThrowsAsync<JSException>(() => zoomInCommand());
        }

        [Fact]
        public async Task SwitchCamera_WithEmptyAvailableCameras_LogsWarningAndSkips()
        {
            // Arrange
            var switchCommand = await SetupAndGetCommand("switch camera");
            
            _cameraServiceMock
                .Setup(x => x.GetAvailableCamerasAsync())
                .ReturnsAsync(Array.Empty<string>());

            // Act
            await switchCommand();

            // Assert
            _cameraServiceMock.Verify(x => x.SwitchCameraAsync(It.IsAny<string>()), Times.Never);
            _loggerMock.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("only one camera available")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task TorchCommands_WithServiceException_ThrowsException()
        {
            // Arrange
            var torchOnCommand = await SetupAndGetCommand("torch on");
            
            _cameraServiceMock
                .Setup(x => x.ToggleTorchAsync(true))
                .ThrowsAsync(new NotSupportedException("Torch not supported"));

            // Act & Assert
            await Assert.ThrowsAsync<NotSupportedException>(() => torchOnCommand());
        }

        [Theory]
        [InlineData("zoom in", 2.9, 3.0)] // Near max zoom
        [InlineData("zoom out", 1.1, 1.0)] // Near min zoom
        public async Task ZoomCommands_AtBoundaryValues_HandlesBoundariesCorrectly(string command, double currentZoom, double expectedZoom)
        {
            // Arrange
            var zoomCommand = await SetupAndGetCommand(command);
            
            _cameraServiceMock
                .Setup(x => x.GetZoomAsync())
                .ReturnsAsync(currentZoom);
            
            _cameraServiceMock
                .Setup(x => x.SetZoomAsync(expectedZoom))
                .ReturnsAsync(true);

            // Act
            await zoomCommand();

            // Assert
            _cameraServiceMock.Verify(x => x.SetZoomAsync(expectedZoom), Times.Once);
        }

        [Fact]
        public async Task InitializeAsync_WhenVoiceCommandServiceFails_ThrowsException()
        {
            // Arrange
            var integration = new CameraVoiceCommandIntegration(
                _voiceCommandServiceMock.Object,
                _cameraServiceMock.Object,
                _loggerMock.Object
            );

            _voiceCommandServiceMock
                .Setup(x => x.SetCommandsAsync(It.IsAny<Dictionary<string, Func<Task>>>()))
                .ThrowsAsync(new InvalidOperationException("Voice service error"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => integration.InitializeAsync());
        }

        [Fact]
        public async Task StartListeningAsync_WhenVoiceServiceFails_ThrowsException()
        {
            // Arrange
            _voiceCommandServiceMock
                .Setup(x => x.SetCommandsAsync(It.IsAny<Dictionary<string, Func<Task>>>()))
                .Returns(Task.CompletedTask);
            
            _voiceCommandServiceMock
                .Setup(x => x.StartListeningAsync())
                .ThrowsAsync(new InvalidOperationException("Cannot start listening"));

            await _integration.InitializeAsync();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _integration.StartListeningAsync());
        }

        [Fact]
        public async Task StopListeningAsync_WhenVoiceServiceFails_ThrowsException()
        {
            // Arrange
            _voiceCommandServiceMock
                .Setup(x => x.StopListeningAsync())
                .ThrowsAsync(new InvalidOperationException("Cannot stop listening"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _integration.StopListeningAsync());
        }

        [Fact]
        public async Task DisposeAsync_WhenStopListeningThrows_LogsErrorButCompletes()
        {
            // Arrange
            _voiceCommandServiceMock
                .Setup(x => x.SetCommandsAsync(It.IsAny<Dictionary<string, Func<Task>>>()))
                .Returns(Task.CompletedTask);
            
            _voiceCommandServiceMock
                .Setup(x => x.IsListeningAsync())
                .ReturnsAsync(true);
            
            _voiceCommandServiceMock
                .Setup(x => x.StopListeningAsync())
                .ThrowsAsync(new InvalidOperationException("Cannot stop"));
            
            await _integration.InitializeAsync();

            // Act
            await _integration.DisposeAsync();

            // Assert
            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error during camera voice command integration disposal")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region Helper Methods

        private async Task<Func<Task>> SetupAndGetCommand(string commandName)
        {
            Dictionary<string, Func<Task>>? registeredCommands = null;
            
            _voiceCommandServiceMock
                .Setup(x => x.SetCommandsAsync(It.IsAny<Dictionary<string, Func<Task>>>()))
                .Callback<Dictionary<string, Func<Task>>>(commands => registeredCommands = commands)
                .Returns(Task.CompletedTask);
            
            await _integration.InitializeAsync();
            
            registeredCommands.Should().NotBeNull();
            registeredCommands!.Should().ContainKey(commandName);
            return registeredCommands[commandName];
        }

        #endregion

        public void Dispose()
        {
            _integration?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}