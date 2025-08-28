using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using NoLock.Social.Core.Accessibility.Services;
using Xunit;

namespace NoLock.Social.Core.Tests.Accessibility.Services
{
    public class FocusManagementServiceTests : IDisposable
    {
        private readonly Mock<IJSRuntime> _jsRuntimeMock;
        private readonly Mock<ILogger<FocusManagementService>> _loggerMock;
        private readonly Mock<IJSObjectReference> _jsModuleMock;
        private readonly FocusManagementService _service;

        public FocusManagementServiceTests()
        {
            _jsRuntimeMock = new Mock<IJSRuntime>();
            _loggerMock = new Mock<ILogger<FocusManagementService>>();
            _jsModuleMock = new Mock<IJSObjectReference>();
            
            // Setup JS module import
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
                .ReturnsAsync(_jsModuleMock.Object);
            
            _service = new FocusManagementService(_jsRuntimeMock.Object, _loggerMock.Object);
        }

        #region StoreFocusAsync Tests

        [Fact]
        public async Task StoreFocusAsync_ShouldStoreElement_Successfully()
        {
            // Arrange
            var elementRef = new ElementReference();

            // Act
            await _service.StoreFocusAsync(elementRef);

            // Assert
            _loggerMock.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Focus element stored")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region RestoreFocusAsync Tests

        [Fact]
        public async Task RestoreFocusAsync_ShouldRestoreFocus_WhenElementWasStored()
        {
            // Arrange
            var elementRef = new ElementReference(Guid.NewGuid().ToString());
            await _service.StoreFocusAsync(elementRef);
            
            _jsModuleMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("setFocus", It.IsAny<object[]>()))
                .ReturnsAsync(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult));

            // Act
            await _service.RestoreFocusAsync();

            // Assert
            _jsModuleMock.Verify(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "setFocus",
                It.Is<object[]>(args => args.Contains(elementRef))),
                Times.Once);
            
            _loggerMock.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Focus restored to stored element")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task RestoreFocusAsync_ShouldLogDebug_WhenNoStoredElement()
        {
            // Act
            await _service.RestoreFocusAsync();

            // Assert
            _loggerMock.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No stored focus element to restore")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
            
            _jsModuleMock.Verify(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "setFocus",
                It.IsAny<object[]>()),
                Times.Never);
        }

        #endregion

        #region SetFocusAsync Tests

        [Fact]
        public async Task SetFocusAsync_ShouldSetFocus_Successfully()
        {
            // Arrange
            var elementRef = new ElementReference(Guid.NewGuid().ToString());
            
            _jsModuleMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("setFocus", It.IsAny<object[]>()))
                .ReturnsAsync(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult));

            // Act
            await _service.SetFocusAsync(elementRef);

            // Assert
            _jsModuleMock.Verify(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "setFocus",
                It.Is<object[]>(args => args.Contains(elementRef))),
                Times.Once);
            
            _loggerMock.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Focus set to specified element")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region TrapFocusAsync Tests

        [Fact]
        public async Task TrapFocusAsync_ShouldTrapFocus_Successfully()
        {
            // Arrange
            var containerRef = new ElementReference(Guid.NewGuid().ToString());
            
            _jsModuleMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("trapFocus", It.IsAny<object[]>()))
                .ReturnsAsync(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult));

            // Act
            await _service.TrapFocusAsync(containerRef);

            // Assert
            _jsModuleMock.Verify(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "trapFocus",
                It.Is<object[]>(args => args.Contains(containerRef))),
                Times.Once);
            
            _loggerMock.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Focus trapped within container")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region ReleaseFocusTrapAsync Tests

        [Fact]
        public async Task ReleaseFocusTrapAsync_ShouldReleaseTrap_Successfully()
        {
            // Arrange
            _jsModuleMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("releaseFocusTrap", It.IsAny<object[]>()))
                .ReturnsAsync(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult));

            // Act
            await _service.ReleaseFocusTrapAsync();

            // Assert
            _jsModuleMock.Verify(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "releaseFocusTrap",
                It.IsAny<object[]>()),
                Times.Once);
            
            _loggerMock.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Focus trap released")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ReleaseFocusTrapAsync_ShouldLogError_WhenExceptionOccurs()
        {
            // Arrange
            var exception = new JSException("Failed to release");
            _jsModuleMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("releaseFocusTrap", It.IsAny<object[]>()))
                .ThrowsAsync(exception);

            // Act
            await _service.ReleaseFocusTrapAsync();

            // Assert
            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error releasing focus trap")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region FocusFirstElementAsync Tests

        [Fact]
        public async Task FocusFirstElementAsync_ShouldFocusFirstElement_Successfully()
        {
            // Arrange
            var containerRef = new ElementReference(Guid.NewGuid().ToString());
            
            _jsModuleMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("focusFirstElement", It.IsAny<object[]>()))
                .ReturnsAsync(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult));

            // Act
            await _service.FocusFirstElementAsync(containerRef);

            // Assert
            _jsModuleMock.Verify(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "focusFirstElement",
                It.Is<object[]>(args => args.Contains(containerRef))),
                Times.Once);
            
            _loggerMock.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Focus moved to first element")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region FocusLastElementAsync Tests

        [Fact]
        public async Task FocusLastElementAsync_ShouldFocusLastElement_Successfully()
        {
            // Arrange
            var containerRef = new ElementReference(Guid.NewGuid().ToString());
            
            _jsModuleMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("focusLastElement", It.IsAny<object[]>()))
                .ReturnsAsync(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult));

            // Act
            await _service.FocusLastElementAsync(containerRef);

            // Assert
            _jsModuleMock.Verify(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "focusLastElement",
                It.Is<object[]>(args => args.Contains(containerRef))),
                Times.Once);
            
            _loggerMock.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Focus moved to last element")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region IsFocusedAsync Tests

        [Fact]
        public async Task IsFocusedAsync_ShouldReturnTrue_WhenElementIsFocused()
        {
            // Arrange
            var elementRef = new ElementReference(Guid.NewGuid().ToString());
            
            _jsModuleMock
                .Setup(x => x.InvokeAsync<bool>("isFocused", It.IsAny<object[]>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.IsFocusedAsync(elementRef);

            // Assert
            result.Should().BeTrue();
            _jsModuleMock.Verify(x => x.InvokeAsync<bool>(
                "isFocused",
                It.Is<object[]>(args => args.Contains(elementRef))),
                Times.Once);
        }

        [Fact]
        public async Task IsFocusedAsync_ShouldReturnFalse_WhenElementIsNotFocused()
        {
            // Arrange
            var elementRef = new ElementReference(Guid.NewGuid().ToString());
            
            _jsModuleMock
                .Setup(x => x.InvokeAsync<bool>("isFocused", It.IsAny<object[]>()))
                .ReturnsAsync(false);

            // Act
            var result = await _service.IsFocusedAsync(elementRef);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task IsFocusedAsync_ShouldReturnFalse_WhenExceptionOccurs()
        {
            // Arrange
            var elementRef = new ElementReference(Guid.NewGuid().ToString());
            
            _jsModuleMock
                .Setup(x => x.InvokeAsync<bool>("isFocused", It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("Failed to check focus"));

            // Act
            var result = await _service.IsFocusedAsync(elementRef);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region GetActiveElementAsync Tests

        [Fact]
        public async Task GetActiveElementAsync_ShouldReturnElement_WhenExists()
        {
            // Arrange
            var expectedElement = new ElementReference(Guid.NewGuid().ToString());
            
            _jsModuleMock
                .Setup(x => x.InvokeAsync<ElementReference?>("getActiveElement", It.IsAny<object[]>()))
                .ReturnsAsync(expectedElement);

            // Act
            var result = await _service.GetActiveElementAsync();

            // Assert
            result.Should().Be(expectedElement);
        }

        [Fact]
        public async Task GetActiveElementAsync_ShouldReturnNull_WhenNoActiveElement()
        {
            // Arrange
            _jsModuleMock
                .Setup(x => x.InvokeAsync<ElementReference?>("getActiveElement", It.IsAny<object[]>()))
                .ReturnsAsync((ElementReference?)null);

            // Act
            var result = await _service.GetActiveElementAsync();

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetActiveElementAsync_ShouldReturnNull_WhenExceptionOccurs()
        {
            // Arrange
            _jsModuleMock
                .Setup(x => x.InvokeAsync<ElementReference?>("getActiveElement", It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("Failed to get active element"));

            // Act
            var result = await _service.GetActiveElementAsync();

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region DisposeAsync Tests

        [Fact]
        public async Task DisposeAsync_ShouldReleaseFocusTrap_WhenTrapped()
        {
            // Arrange
            var containerRef = new ElementReference(Guid.NewGuid().ToString());
            
            _jsModuleMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(It.IsAny<string>(), It.IsAny<object[]>()))
                .ReturnsAsync(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult));
            
            await _service.TrapFocusAsync(containerRef);

            // Act
            await _service.DisposeAsync();

            // Assert
            _jsModuleMock.Verify(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "releaseFocusTrap",
                It.IsAny<object[]>()),
                Times.Once);
        }

        [Fact]
        public async Task DisposeAsync_ShouldDisposeModule_WhenCreated()
        {
            // Arrange
            // Force module creation by calling a method
            _jsModuleMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("setFocus", It.IsAny<object[]>()))
                .ReturnsAsync(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult));
            
            await _service.SetFocusAsync(new ElementReference());

            // Act
            await _service.DisposeAsync();

            // Assert
            _jsModuleMock.Verify(x => x.DisposeAsync(), Times.Once);
        }

        [Fact]
        public async Task DisposeAsync_ShouldHandleExceptionsGracefully()
        {
            // Arrange
            _jsModuleMock
                .Setup(x => x.DisposeAsync())
                .ThrowsAsync(new JSException("Disposal failed"));
            
            // Force module creation
            _jsModuleMock
                .Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("setFocus", It.IsAny<object[]>()))
                .ReturnsAsync(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult));
            
            await _service.SetFocusAsync(new ElementReference());

            // Act
            var act = async () => await _service.DisposeAsync();

            // Assert
            await act.Should().NotThrowAsync();
            
            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error disposing FocusManagementService")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        public void Dispose()
        {
            _service?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}