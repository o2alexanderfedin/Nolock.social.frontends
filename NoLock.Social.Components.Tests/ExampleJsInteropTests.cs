using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.JSInterop;
using Moq;
using Xunit;

namespace NoLock.Social.Components.Tests
{
    public class ExampleJsInteropTests
    {
        private readonly Mock<IJSRuntime> _jsRuntimeMock;

        public ExampleJsInteropTests()
        {
            _jsRuntimeMock = new Mock<IJSRuntime>();
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidJSRuntime_ShouldInitialize()
        {
            // Arrange & Act
            var interop = new ExampleJsInterop(_jsRuntimeMock.Object);

            // Assert
            interop.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithNullJSRuntime_ShouldNotThrow()
        {
            // Arrange & Act
            var action = () => new ExampleJsInterop(null!);

            // Assert - Constructor accepts null but will fail on usage
            action.Should().NotThrow();
        }

        [Fact]
        public void Constructor_ShouldNotImmediatelyLoadModule()
        {
            // Arrange & Act
            var interop = new ExampleJsInterop(_jsRuntimeMock.Object);

            // Assert - Module loading is lazy, should not be invoked yet
            _jsRuntimeMock.Verify(
                js => js.InvokeAsync<IJSObjectReference>(
                    "import",
                    It.IsAny<object[]>()),
                Times.Never);
        }

        #endregion

        #region Prompt Method Tests

        [Theory]
        [InlineData("Hello, World!", "User Response", "Simple message")]
        [InlineData("", "Empty Response", "Empty message")]
        [InlineData("Special chars: @#$%^&*()", "Special Response", "Special characters")]
        [InlineData("Very long message " + 
                    "that contains a lot of text to test how the system handles " +
                    "longer messages with multiple lines and various content", 
                    "Long Response", "Long message")]
        public async Task Prompt_WithVariousMessages_ShouldReturnExpectedResponse(
            string message, string expectedResponse, string scenario)
        {
            // Arrange
            var mockModule = new Mock<IJSObjectReference>();
            mockModule
                .Setup(m => m.InvokeAsync<string>("showPrompt", It.IsAny<object[]>()))
                .ReturnsAsync(expectedResponse);

            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<IJSObjectReference>(
                    "import",
                    It.Is<object[]>(args => args[0].ToString() == "./_content/NoLock.Social.Components/exampleJsInterop.js")))
                .ReturnsAsync(mockModule.Object);

            var interop = new ExampleJsInterop(_jsRuntimeMock.Object);

            // Act
            var result = await interop.Prompt(message);

            // Assert
            result.Should().Be(expectedResponse, $"Failed for scenario: {scenario}");
            mockModule.Verify(
                m => m.InvokeAsync<string>("showPrompt", It.Is<object[]>(args => 
                    args.Length == 1 && (string)args[0] == message)),
                Times.Once);
        }

        [Fact]
        public async Task Prompt_WithNullMessage_ShouldHandleGracefully()
        {
            // Arrange
            var mockModule = new Mock<IJSObjectReference>();
            mockModule
                .Setup(m => m.InvokeAsync<string>("showPrompt", It.IsAny<object[]>()))
                .ReturnsAsync("Null Response");

            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<IJSObjectReference>(
                    "import",
                    It.IsAny<object[]>()))
                .ReturnsAsync(mockModule.Object);

            var interop = new ExampleJsInterop(_jsRuntimeMock.Object);

            // Act
            var result = await interop.Prompt(null!);

            // Assert
            result.Should().Be("Null Response");
        }

        [Fact]
        public async Task Prompt_ShouldLoadModuleLazilyOnFirstCall()
        {
            // Arrange
            var mockModule = new Mock<IJSObjectReference>();
            mockModule
                .Setup(m => m.InvokeAsync<string>("showPrompt", It.IsAny<object[]>()))
                .ReturnsAsync("Response");

            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<IJSObjectReference>(
                    "import",
                    It.IsAny<object[]>()))
                .ReturnsAsync(mockModule.Object);

            var interop = new ExampleJsInterop(_jsRuntimeMock.Object);

            // Act - First call
            await interop.Prompt("First");
            
            // Assert - Module should be loaded once
            _jsRuntimeMock.Verify(
                js => js.InvokeAsync<IJSObjectReference>(
                    "import",
                    It.IsAny<object[]>()),
                Times.Once);

            // Act - Second call
            await interop.Prompt("Second");

            // Assert - Module should still only be loaded once (lazy loading)
            _jsRuntimeMock.Verify(
                js => js.InvokeAsync<IJSObjectReference>(
                    "import",
                    It.IsAny<object[]>()),
                Times.Once);
        }

        [Fact]
        public async Task Prompt_WhenModuleLoadingFails_ShouldPropagateException()
        {
            // Arrange
            var expectedException = new JSException("Failed to load module");
            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<IJSObjectReference>(
                    "import",
                    It.IsAny<object[]>()))
                .ThrowsAsync(expectedException);

            var interop = new ExampleJsInterop(_jsRuntimeMock.Object);

            // Act
            var action = async () => await interop.Prompt("Test");

            // Assert
            await action.Should().ThrowAsync<JSException>()
                .WithMessage("Failed to load module");
        }

        [Fact]
        public async Task Prompt_WhenShowPromptFails_ShouldPropagateException()
        {
            // Arrange
            var mockModule = new Mock<IJSObjectReference>();
            var expectedException = new JSException("showPrompt failed");
            mockModule
                .Setup(m => m.InvokeAsync<string>("showPrompt", It.IsAny<object[]>()))
                .ThrowsAsync(expectedException);

            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<IJSObjectReference>(
                    "import",
                    It.IsAny<object[]>()))
                .ReturnsAsync(mockModule.Object);

            var interop = new ExampleJsInterop(_jsRuntimeMock.Object);

            // Act
            var action = async () => await interop.Prompt("Test");

            // Assert
            await action.Should().ThrowAsync<JSException>()
                .WithMessage("showPrompt failed");
        }

        #endregion

        #region DisposeAsync Tests

        [Fact]
        public async Task DisposeAsync_WhenModuleNotCreated_ShouldNotDisposeModule()
        {
            // Arrange
            var interop = new ExampleJsInterop(_jsRuntimeMock.Object);

            // Act
            await interop.DisposeAsync();

            // Assert - Should not attempt to load or dispose module
            _jsRuntimeMock.Verify(
                js => js.InvokeAsync<IJSObjectReference>(
                    "import",
                    It.IsAny<object[]>()),
                Times.Never);
        }

        [Fact]
        public async Task DisposeAsync_WhenModuleCreated_ShouldDisposeModule()
        {
            // Arrange
            var mockModule = new Mock<IJSObjectReference>();
            mockModule
                .Setup(m => m.InvokeAsync<string>("showPrompt", It.IsAny<object[]>()))
                .ReturnsAsync("Response");
            mockModule
                .Setup(m => m.DisposeAsync())
                .Returns(ValueTask.CompletedTask);

            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<IJSObjectReference>(
                    "import",
                    It.IsAny<object[]>()))
                .ReturnsAsync(mockModule.Object);

            var interop = new ExampleJsInterop(_jsRuntimeMock.Object);
            
            // Create the module by calling Prompt
            await interop.Prompt("Test");

            // Act
            await interop.DisposeAsync();

            // Assert
            mockModule.Verify(m => m.DisposeAsync(), Times.Once);
        }

        [Fact]
        public async Task DisposeAsync_CalledMultipleTimes_ShouldCallDisposeMultipleTimes()
        {
            // Arrange
            var mockModule = new Mock<IJSObjectReference>();
            mockModule
                .Setup(m => m.InvokeAsync<string>("showPrompt", It.IsAny<object[]>()))
                .ReturnsAsync("Response");
            mockModule
                .Setup(m => m.DisposeAsync())
                .Returns(ValueTask.CompletedTask);

            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<IJSObjectReference>(
                    "import",
                    It.IsAny<object[]>()))
                .ReturnsAsync(mockModule.Object);

            var interop = new ExampleJsInterop(_jsRuntimeMock.Object);
            await interop.Prompt("Test");

            // Act - Dispose multiple times
            await interop.DisposeAsync();
            await interop.DisposeAsync();
            await interop.DisposeAsync();

            // Assert - Module will be disposed multiple times since ExampleJsInterop doesn't track disposal state
            // This is the actual behavior of the implementation
            mockModule.Verify(m => m.DisposeAsync(), Times.Exactly(3));
        }

        [Fact]
        public async Task DisposeAsync_WhenModuleDisposeThrows_ShouldPropagateException()
        {
            // Arrange
            var mockModule = new Mock<IJSObjectReference>();
            mockModule
                .Setup(m => m.InvokeAsync<string>("showPrompt", It.IsAny<object[]>()))
                .ReturnsAsync("Response");
            
            var expectedException = new JSException("Dispose failed");
            mockModule
                .Setup(m => m.DisposeAsync())
                .ThrowsAsync(expectedException);

            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<IJSObjectReference>(
                    "import",
                    It.IsAny<object[]>()))
                .ReturnsAsync(mockModule.Object);

            var interop = new ExampleJsInterop(_jsRuntimeMock.Object);
            await interop.Prompt("Test");

            // Act
            var action = async () => await interop.DisposeAsync();

            // Assert
            await action.Should().ThrowAsync<JSException>()
                .WithMessage("Dispose failed");
        }

        #endregion

        #region Integration Tests

        [Fact]
        public async Task FullLifecycle_ShouldWorkCorrectly()
        {
            // Arrange
            var mockModule = new Mock<IJSObjectReference>();
            mockModule
                .Setup(m => m.InvokeAsync<string>("showPrompt", It.IsAny<object[]>()))
                .ReturnsAsync("Echo: Test");
            mockModule
                .Setup(m => m.DisposeAsync())
                .Returns(ValueTask.CompletedTask);

            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<IJSObjectReference>(
                    "import",
                    It.IsAny<object[]>()))
                .ReturnsAsync(mockModule.Object);

            // Act & Assert
            var interop = new ExampleJsInterop(_jsRuntimeMock.Object);
            
            // Multiple prompts
            var result1 = await interop.Prompt("First");
            result1.Should().Be("Echo: Test");
            
            var result2 = await interop.Prompt("Second");
            result2.Should().Be("Echo: Test");
            
            var result3 = await interop.Prompt("Third");
            result3.Should().Be("Echo: Test");
            
            // Module should be loaded only once
            _jsRuntimeMock.Verify(
                js => js.InvokeAsync<IJSObjectReference>(
                    "import",
                    It.IsAny<object[]>()),
                Times.Once);
            
            // Dispose
            await interop.DisposeAsync();
            mockModule.Verify(m => m.DisposeAsync(), Times.Once);
        }

        [Fact]
        public async Task ConcurrentPromptCalls_ShouldOnlyLoadModuleOnce()
        {
            // Arrange
            var tcs = new TaskCompletionSource<IJSObjectReference>();
            var mockModule = new Mock<IJSObjectReference>();
            
            mockModule
                .SetupSequence(m => m.InvokeAsync<string>("showPrompt", It.IsAny<object[]>()))
                .ReturnsAsync("Response for: Call1")
                .ReturnsAsync("Response for: Call2")
                .ReturnsAsync("Response for: Call3");

            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<IJSObjectReference>(
                    "import",
                    It.IsAny<object[]>()))
                .Returns(() => new ValueTask<IJSObjectReference>(tcs.Task));

            var interop = new ExampleJsInterop(_jsRuntimeMock.Object);

            // Act - Start multiple concurrent calls
            var task1 = interop.Prompt("Call1").AsTask();
            var task2 = interop.Prompt("Call2").AsTask();
            var task3 = interop.Prompt("Call3").AsTask();

            // Complete the module loading
            tcs.SetResult(mockModule.Object);

            var results = await Task.WhenAll(task1, task2, task3);

            // Assert
            _jsRuntimeMock.Verify(
                js => js.InvokeAsync<IJSObjectReference>(
                    "import",
                    It.IsAny<object[]>()),
                Times.Once,
                "Module should only be loaded once even with concurrent calls");
            
            results.Should().BeEquivalentTo(new[] 
            { 
                "Response for: Call1",
                "Response for: Call2",
                "Response for: Call3"
            });
        }

        #endregion
    }
}