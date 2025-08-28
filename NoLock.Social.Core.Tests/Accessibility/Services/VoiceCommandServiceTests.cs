using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using NoLock.Social.Core.Accessibility.Interfaces;
using NoLock.Social.Core.Accessibility.Services;
using Xunit;

namespace NoLock.Social.Core.Tests.Accessibility.Services
{
    public class VoiceCommandServiceTests : IDisposable
    {
        private readonly Mock<IJSRuntime> _jsRuntimeMock;
        private readonly Mock<ILogger<VoiceCommandService>> _loggerMock;
        private readonly VoiceCommandService _service;

        public VoiceCommandServiceTests()
        {
            _jsRuntimeMock = new Mock<IJSRuntime>();
            _loggerMock = new Mock<ILogger<VoiceCommandService>>();
            _service = new VoiceCommandService(_jsRuntimeMock.Object, _loggerMock.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_ShouldThrowArgumentNullException_WhenJSRuntimeIsNull()
        {
            // Act & Assert
            var act = () => new VoiceCommandService(null!, _loggerMock.Object);
            act.Should().Throw<ArgumentNullException>().WithParameterName("jsRuntime");
        }

        [Fact]
        public void Constructor_ShouldThrowArgumentNullException_WhenLoggerIsNull()
        {
            // Act & Assert
            var act = () => new VoiceCommandService(_jsRuntimeMock.Object, null!);
            act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
        }

        [Fact]
        public void Constructor_ShouldInitializeSuccessfully_WithValidDependencies()
        {
            // Act
            var service = new VoiceCommandService(_jsRuntimeMock.Object, _loggerMock.Object);

            // Assert
            service.Should().NotBeNull();
        }

        #endregion

        #region StartListeningAsync Tests

        [Fact]
        public async Task StartListeningAsync_ShouldStartSuccessfully_WhenNotAlreadyListening()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("speechRecognition.startListening", It.IsAny<object[]>()))
                .ReturnsAsync(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult));

            // Act
            await _service.StartListeningAsync();
            var isListening = await _service.IsListeningAsync();

            // Assert
            isListening.Should().BeTrue();
            _jsRuntimeMock.Verify(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "speechRecognition.startListening",
                It.IsAny<object[]>()),
                Times.Once);
        }

        [Fact]
        public async Task StartListeningAsync_ShouldLogWarning_WhenAlreadyListening()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("speechRecognition.startListening", It.IsAny<object[]>()))
                .ReturnsAsync(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult));

            await _service.StartListeningAsync();

            // Act
            await _service.StartListeningAsync();

            // Assert
            _loggerMock.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("already listening")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task StartListeningAsync_ShouldThrowAndFireEvent_WhenJSExceptionOccurs()
        {
            // Arrange
            var jsException = new JSException("Speech API not available");
            SpeechErrorEventArgs? errorEventArgs = null;
            
            _service.OnSpeechError += (sender, args) => errorEventArgs = args;
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("speechRecognition.startListening", It.IsAny<object[]>()))
                .ThrowsAsync(jsException);

            // Act
            var act = async () => await _service.StartListeningAsync();

            // Assert
            await act.Should().ThrowAsync<JSException>();
            var isListening = await _service.IsListeningAsync();
            isListening.Should().BeFalse();
            
            errorEventArgs.Should().NotBeNull();
            errorEventArgs!.ErrorMessage.Should().Be("Speech API not available");
            errorEventArgs.ErrorCode.Should().Be("JS_EXCEPTION");
        }

        [Fact]
        public async Task StartListeningAsync_ShouldThrowObjectDisposedException_WhenDisposed()
        {
            // Arrange
            await _service.DisposeAsync();

            // Act
            var act = async () => await _service.StartListeningAsync();

            // Assert
            await act.Should().ThrowAsync<ObjectDisposedException>();
        }

        #endregion

        #region StopListeningAsync Tests

        [Fact]
        public async Task StopListeningAsync_ShouldStopSuccessfully_WhenListening()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(It.IsAny<string>(), It.IsAny<object[]>()))
                .ReturnsAsync(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult));

            await _service.StartListeningAsync();

            // Act
            await _service.StopListeningAsync();
            var isListening = await _service.IsListeningAsync();

            // Assert
            isListening.Should().BeFalse();
            _jsRuntimeMock.Verify(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "speechRecognition.stopListening",
                It.IsAny<object[]>()),
                Times.Once);
        }

        [Fact]
        public async Task StopListeningAsync_ShouldLogWarning_WhenNotListening()
        {
            // Act
            await _service.StopListeningAsync();

            // Assert
            _loggerMock.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("not currently listening")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task StopListeningAsync_ShouldThrowAndFireEvent_WhenJSExceptionOccurs()
        {
            // Arrange
            var jsException = new JSException("Failed to stop");
            SpeechErrorEventArgs? errorEventArgs = null;
            
            _service.OnSpeechError += (sender, args) => errorEventArgs = args;
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("speechRecognition.startListening", It.IsAny<object[]>()))
                .ReturnsAsync(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult));
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("speechRecognition.stopListening", It.IsAny<object[]>()))
                .ThrowsAsync(jsException);

            await _service.StartListeningAsync();

            // Act
            var act = async () => await _service.StopListeningAsync();

            // Assert
            await act.Should().ThrowAsync<JSException>();
            
            errorEventArgs.Should().NotBeNull();
            errorEventArgs!.ErrorMessage.Should().Be("Failed to stop");
        }

        #endregion

        #region SetCommandsAsync Tests

        [Fact]
        public async Task SetCommandsAsync_ShouldSetCommands_Successfully()
        {
            // Arrange
            var commands = new Dictionary<string, Func<Task>>
            {
                { "test command", async () => await Task.CompletedTask },
                { "another command", async () => await Task.CompletedTask }
            };

            // Act
            await _service.SetCommandsAsync(commands);
            var retrievedCommands = await _service.GetCommandsAsync();

            // Assert
            retrievedCommands.Should().HaveCount(2);
            retrievedCommands.Should().ContainKey("test command");
            retrievedCommands.Should().ContainKey("another command");
        }

        [Fact]
        public async Task SetCommandsAsync_ShouldBeCaseInsensitive()
        {
            // Arrange
            var commands = new Dictionary<string, Func<Task>>
            {
                { "Test Command", async () => await Task.CompletedTask }
            };

            // Act
            await _service.SetCommandsAsync(commands);
            var retrievedCommands = await _service.GetCommandsAsync();

            // Assert
            retrievedCommands.Should().ContainKey("test command");
            retrievedCommands.Should().ContainKey("TEST COMMAND");
        }

        [Fact]
        public async Task SetCommandsAsync_ShouldThrowArgumentNullException_WhenCommandsIsNull()
        {
            // Act
            var act = async () => await _service.SetCommandsAsync(null!);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("commands");
        }

        [Fact]
        public async Task SetCommandsAsync_ShouldThrowObjectDisposedException_WhenDisposed()
        {
            // Arrange
            await _service.DisposeAsync();
            var commands = new Dictionary<string, Func<Task>>();

            // Act
            var act = async () => await _service.SetCommandsAsync(commands);

            // Assert
            await act.Should().ThrowAsync<ObjectDisposedException>();
        }

        #endregion

        #region GetCommandsAsync Tests

        [Fact]
        public async Task GetCommandsAsync_ShouldReturnCopyOfCommands()
        {
            // Arrange
            var originalCommands = new Dictionary<string, Func<Task>>
            {
                { "command1", async () => await Task.CompletedTask }
            };
            
            await _service.SetCommandsAsync(originalCommands);

            // Act
            var retrievedCommands = await _service.GetCommandsAsync();
            retrievedCommands.Add("command2", async () => await Task.CompletedTask);
            
            var commandsAfterModification = await _service.GetCommandsAsync();

            // Assert
            commandsAfterModification.Should().HaveCount(1);
            commandsAfterModification.Should().NotContainKey("command2");
        }

        #endregion

        #region IsSpeechRecognitionSupportedAsync Tests

        [Fact]
        public async Task IsSpeechRecognitionSupportedAsync_ShouldReturnTrue_WhenSupported()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<bool>("speechRecognition.isSupported", It.IsAny<object[]>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.IsSpeechRecognitionSupportedAsync();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsSpeechRecognitionSupportedAsync_ShouldReturnFalse_WhenNotSupported()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<bool>("speechRecognition.isSupported", It.IsAny<object[]>()))
                .ReturnsAsync(false);

            // Act
            var result = await _service.IsSpeechRecognitionSupportedAsync();

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task IsSpeechRecognitionSupportedAsync_ShouldReturnFalse_WhenJSExceptionOccurs()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<bool>("speechRecognition.isSupported", It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("API not available"));

            // Act
            var result = await _service.IsSpeechRecognitionSupportedAsync();

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region OnSpeechRecognized Tests

        [Fact]
        public async Task OnSpeechRecognized_ShouldExecuteMatchingCommand_ExactMatch()
        {
            // Arrange
            var commandExecuted = false;
            var commands = new Dictionary<string, Func<Task>>
            {
                { "take photo", async () => { commandExecuted = true; await Task.CompletedTask; } }
            };
            
            VoiceCommandEventArgs? eventArgs = null;
            _service.OnCommandRecognized += (sender, args) => eventArgs = args;
            
            await _service.SetCommandsAsync(commands);

            // Act
            await _service.OnSpeechRecognized("take photo", 0.95);

            // Assert
            commandExecuted.Should().BeTrue();
            eventArgs.Should().NotBeNull();
            eventArgs!.RecognizedText.Should().Be("take photo");
            eventArgs.MatchedCommand.Should().Be("take photo");
            eventArgs.Confidence.Should().Be(0.95);
        }

        [Fact]
        public async Task OnSpeechRecognized_ShouldExecuteMatchingCommand_CaseInsensitive()
        {
            // Arrange
            var commandExecuted = false;
            var commands = new Dictionary<string, Func<Task>>
            {
                { "Take Photo", async () => { commandExecuted = true; await Task.CompletedTask; } }
            };
            
            await _service.SetCommandsAsync(commands);

            // Act
            await _service.OnSpeechRecognized("TAKE PHOTO", 0.9);

            // Assert
            commandExecuted.Should().BeTrue();
        }

        [Fact]
        public async Task OnSpeechRecognized_ShouldExecuteMatchingCommand_PartialMatch()
        {
            // Arrange
            var commandExecuted = false;
            var commands = new Dictionary<string, Func<Task>>
            {
                { "start camera", async () => { commandExecuted = true; await Task.CompletedTask; } }
            };
            
            await _service.SetCommandsAsync(commands);

            // Act
            await _service.OnSpeechRecognized("please start the camera now", 0.85);

            // Assert
            commandExecuted.Should().BeTrue();
        }

        [Fact]
        public async Task OnSpeechRecognized_ShouldNotExecuteCommand_WhenNoMatch()
        {
            // Arrange
            var commandExecuted = false;
            var commands = new Dictionary<string, Func<Task>>
            {
                { "take photo", async () => { commandExecuted = true; await Task.CompletedTask; } }
            };
            
            await _service.SetCommandsAsync(commands);

            // Act
            await _service.OnSpeechRecognized("play music", 0.9);

            // Assert
            commandExecuted.Should().BeFalse();
        }

        [Fact]
        public async Task OnSpeechRecognized_ShouldHandleCommandException_AndFireErrorEvent()
        {
            // Arrange
            var commands = new Dictionary<string, Func<Task>>
            {
                { "failing command", () => throw new InvalidOperationException("Command failed") }
            };
            
            SpeechErrorEventArgs? errorArgs = null;
            _service.OnSpeechError += (sender, args) => errorArgs = args;
            
            await _service.SetCommandsAsync(commands);

            // Act
            await _service.OnSpeechRecognized("failing command", 0.9);

            // Assert
            errorArgs.Should().NotBeNull();
            errorArgs!.ErrorMessage.Should().Contain("Command execution failed");
            errorArgs.ErrorCode.Should().Be("COMMAND_EXECUTION_ERROR");
        }

        [Fact]
        public async Task OnSpeechRecognized_ShouldIgnore_WhenRecognizedTextIsEmpty()
        {
            // Arrange
            var commandExecuted = false;
            var commands = new Dictionary<string, Func<Task>>
            {
                { "test", async () => { commandExecuted = true; await Task.CompletedTask; } }
            };
            
            await _service.SetCommandsAsync(commands);

            // Act
            await _service.OnSpeechRecognized("", 0.9);
            await _service.OnSpeechRecognized(null!, 0.9);
            await _service.OnSpeechRecognized("   ", 0.9);

            // Assert
            commandExecuted.Should().BeFalse();
        }

        #endregion

        #region HandleSpeechError Tests

        [Fact]
        public async Task HandleSpeechError_ShouldFireErrorEvent_AndStopListening()
        {
            // Arrange
            SpeechErrorEventArgs? errorArgs = null;
            _service.OnSpeechError += (sender, args) => errorArgs = args;
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("speechRecognition.startListening", It.IsAny<object[]>()))
                .ReturnsAsync(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult));
            
            await _service.StartListeningAsync();

            // Act
            await _service.HandleSpeechError("Network error", "NETWORK_ERROR");
            var isListening = await _service.IsListeningAsync();

            // Assert
            isListening.Should().BeFalse();
            errorArgs.Should().NotBeNull();
            errorArgs!.ErrorMessage.Should().Be("Network error");
            errorArgs.ErrorCode.Should().Be("NETWORK_ERROR");
        }

        [Fact]
        public async Task HandleSpeechError_ShouldNotThrow_WhenEventHandlerThrows()
        {
            // Arrange
            _service.OnSpeechError += (sender, args) => throw new Exception("Handler error");

            // Act
            var act = async () => await _service.HandleSpeechError("Test error", "TEST_ERROR");

            // Assert
            await act.Should().NotThrowAsync();
        }

        #endregion

        #region DisposeAsync Tests

        [Fact]
        public async Task DisposeAsync_ShouldStopListening_WhenCurrentlyListening()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(It.IsAny<string>(), It.IsAny<object[]>()))
                .ReturnsAsync(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult));
            
            await _service.StartListeningAsync();

            // Act
            await _service.DisposeAsync();

            // Assert
            _jsRuntimeMock.Verify(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "speechRecognition.stopListening",
                It.IsAny<object[]>()),
                Times.Once);
        }

        [Fact]
        public async Task DisposeAsync_ShouldClearCommands()
        {
            // Arrange
            var commands = new Dictionary<string, Func<Task>>
            {
                { "test", async () => await Task.CompletedTask }
            };
            
            await _service.SetCommandsAsync(commands);

            // Act
            await _service.DisposeAsync();

            // Assert
            var act = async () => await _service.GetCommandsAsync();
            await act.Should().ThrowAsync<ObjectDisposedException>();
        }

        [Fact]
        public async Task DisposeAsync_ShouldBeIdempotent()
        {
            // Act & Assert
            await _service.DisposeAsync();
            await _service.DisposeAsync(); // Should not throw
        }

        [Fact]
        public async Task DisposeAsync_ShouldHandleExceptionsGracefully()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("speechRecognition.startListening", It.IsAny<object[]>()))
                .ReturnsAsync(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult));
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("speechRecognition.stopListening", It.IsAny<object[]>()))
                .ThrowsAsync(new Exception("Stop failed"));
            
            await _service.StartListeningAsync();

            // Act
            var act = async () => await _service.DisposeAsync();

            // Assert
            await act.Should().NotThrowAsync();
        }

        #endregion

        public void Dispose()
        {
            _service?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}