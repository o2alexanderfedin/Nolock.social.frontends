using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using NoLock.Social.Core.ImageProcessing.Interfaces;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Models;
using NoLock.Social.Core.OCR.Services;
using Xunit;

namespace NoLock.Social.Core.Tests.OCR.Services
{
    /// <summary>
    /// Unit tests for WakeLockService functionality.
    /// Tests wake lock acquisition, release, visibility monitoring, and error handling.
    /// </summary>
    public class WakeLockServiceTests : IDisposable
    {
        private readonly Mock<IJSRuntimeWrapper> _mockJSRuntime;
        private readonly Mock<ILogger<WakeLockService>> _mockLogger;
        private readonly WakeLockService _wakeLockService;

        public WakeLockServiceTests()
        {
            _mockJSRuntime = new Mock<IJSRuntimeWrapper>();
            _mockLogger = new Mock<ILogger<WakeLockService>>();
            _wakeLockService = new WakeLockService(_mockJSRuntime.Object, _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_InitializesCorrectly()
        {
            // Act & Assert
            Assert.False(_wakeLockService.IsWakeLockActive);
            Assert.True(_wakeLockService.IsPageVisible);
        }

        [Fact]
        public void Constructor_WithNullJSRuntime_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new WakeLockService(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new WakeLockService(_mockJSRuntime.Object, null!));
        }

        #endregion

        #region Wake Lock Acquisition Tests

        [Theory]
        [InlineData(true, true, "OCR Processing")]
        [InlineData(true, true, "Custom Reason")]
        [InlineData(true, true, null)]
        public async Task AcquireWakeLockAsync_WhenSupported_ReturnsSuccessResult(
            bool browserSupported, 
            bool jsResult, 
            string reason)
        {
            // Arrange
            _mockJSRuntime.Setup(js => js.InvokeAsync<bool>("wakeLockInterop.isWakeLockSupported", It.IsAny<object[]>()))
                .ReturnsAsync(browserSupported);
            _mockJSRuntime.Setup(js => js.InvokeAsync<bool>("wakeLockInterop.acquireWakeLock", It.IsAny<object[]>()))
                .ReturnsAsync(jsResult);

            // Act
            var result = await _wakeLockService.AcquireWakeLockAsync(reason);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(WakeLockOperationType.Acquire, result.OperationType);
            Assert.True(_wakeLockService.IsWakeLockActive);
        }

        [Fact]
        public async Task AcquireWakeLockAsync_WhenAlreadyActive_ReturnsSuccessWithoutJSCall()
        {
            // Arrange
            SetupSuccessfulAcquisition();
            await _wakeLockService.AcquireWakeLockAsync(); // First acquisition

            // Act
            var result = await _wakeLockService.AcquireWakeLockAsync("Second attempt");

            // Assert
            Assert.True(result.IsSuccess);
            Assert.True(_wakeLockService.IsWakeLockActive);
            // Verify JS was called only once (for the first acquisition)
            _mockJSRuntime.Verify(js => js.InvokeAsync<bool>("wakeLockInterop.acquireWakeLock", It.IsAny<object[]>()), 
                Times.Once);
        }

        [Fact]
        public async Task AcquireWakeLockAsync_WhenBrowserNotSupported_ReturnsFailureResult()
        {
            // Arrange
            _mockJSRuntime.Setup(js => js.InvokeAsync<bool>("wakeLockInterop.isWakeLockSupported", It.IsAny<object[]>()))
                .ReturnsAsync(false);

            // Act
            var result = await _wakeLockService.AcquireWakeLockAsync();

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(WakeLockOperationType.Acquire, result.OperationType);
            Assert.False(_wakeLockService.IsWakeLockActive);
            Assert.Contains("not supported", result.ErrorMessage);
        }

        [Fact]
        public async Task AcquireWakeLockAsync_WhenJSReturnsFalse_ReturnsFailureResult()
        {
            // Arrange
            _mockJSRuntime.Setup(js => js.InvokeAsync<bool>("wakeLockInterop.isWakeLockSupported", It.IsAny<object[]>()))
                .ReturnsAsync(true);
            _mockJSRuntime.Setup(js => js.InvokeAsync<bool>("wakeLockInterop.acquireWakeLock", It.IsAny<object[]>()))
                .ReturnsAsync(false);

            // Act
            var result = await _wakeLockService.AcquireWakeLockAsync();

            // Assert
            Assert.False(result.IsSuccess);
            Assert.False(_wakeLockService.IsWakeLockActive);
            Assert.Contains("Failed to acquire", result.ErrorMessage);
        }

        [Fact]
        public async Task AcquireWakeLockAsync_WhenJSExceptionThrown_ReturnsFailureResult()
        {
            // Arrange
            _mockJSRuntime.Setup(js => js.InvokeAsync<bool>("wakeLockInterop.isWakeLockSupported", It.IsAny<object[]>()))
                .ReturnsAsync(true);
            _mockJSRuntime.Setup(js => js.InvokeAsync<bool>("wakeLockInterop.acquireWakeLock", It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("JavaScript error"));

            // Act
            var result = await _wakeLockService.AcquireWakeLockAsync();

            // Assert
            Assert.False(result.IsSuccess);
            Assert.False(_wakeLockService.IsWakeLockActive);
            Assert.Contains("JavaScript error", result.ErrorMessage);
        }

        [Fact]
        public async Task AcquireWakeLockAsync_WhenGenericExceptionThrown_ReturnsFailureResult()
        {
            // Arrange
            _mockJSRuntime.Setup(js => js.InvokeAsync<bool>("wakeLockInterop.isWakeLockSupported", It.IsAny<object[]>()))
                .ThrowsAsync(new InvalidOperationException("Generic error"));

            // Act
            var result = await _wakeLockService.AcquireWakeLockAsync();

            // Assert
            Assert.False(result.IsSuccess);
            Assert.False(_wakeLockService.IsWakeLockActive);
            Assert.Contains("Unable to determine Wake Lock API support", result.ErrorMessage);
        }

        #endregion

        #region Wake Lock Release Tests

        [Fact]
        public async Task ReleaseWakeLockAsync_WhenActive_ReturnsSuccessResult()
        {
            // Arrange
            SetupSuccessfulAcquisition();
            await _wakeLockService.AcquireWakeLockAsync();
            
            _mockJSRuntime.Setup(js => js.InvokeAsync<bool>("wakeLockInterop.releaseWakeLock", It.IsAny<object[]>()))
                .ReturnsAsync(true);

            // Act
            var result = await _wakeLockService.ReleaseWakeLockAsync();

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(WakeLockOperationType.Release, result.OperationType);
            Assert.False(_wakeLockService.IsWakeLockActive);
        }

        [Fact]
        public async Task ReleaseWakeLockAsync_WhenNotActive_ReturnsSuccessWithoutJSCall()
        {
            // Act
            var result = await _wakeLockService.ReleaseWakeLockAsync();

            // Assert
            Assert.True(result.IsSuccess);
            Assert.False(_wakeLockService.IsWakeLockActive);
            _mockJSRuntime.Verify(js => js.InvokeAsync<bool>("wakeLockInterop.releaseWakeLock", It.IsAny<object[]>()), 
                Times.Never);
        }

        [Fact]
        public async Task ReleaseWakeLockAsync_WhenJSReturnsFalse_ReturnsFailureResult()
        {
            // Arrange
            SetupSuccessfulAcquisition();
            await _wakeLockService.AcquireWakeLockAsync();
            
            _mockJSRuntime.Setup(js => js.InvokeAsync<bool>("wakeLockInterop.releaseWakeLock", It.IsAny<object[]>()))
                .ReturnsAsync(false);

            // Act
            var result = await _wakeLockService.ReleaseWakeLockAsync();

            // Assert
            Assert.False(result.IsSuccess);
            Assert.True(_wakeLockService.IsWakeLockActive); // Should remain active on failure
            Assert.Contains("Failed to release", result.ErrorMessage);
        }

        [Fact]
        public async Task ReleaseWakeLockAsync_WhenJSExceptionThrown_ReturnsFailureResult()
        {
            // Arrange
            SetupSuccessfulAcquisition();
            await _wakeLockService.AcquireWakeLockAsync();
            
            _mockJSRuntime.Setup(js => js.InvokeAsync<bool>("wakeLockInterop.releaseWakeLock", It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("Release error"));

            // Act
            var result = await _wakeLockService.ReleaseWakeLockAsync();

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("JavaScript error", result.ErrorMessage);
        }

        #endregion

        #region Visibility Monitoring Tests

        [Fact]
        public async Task StartVisibilityMonitoringAsync_WhenNotActive_StartsSuccessfully()
        {
            // Arrange
            _mockJSRuntime.Setup(js => js.InvokeVoidAsync("wakeLockInterop.startVisibilityMonitoring", It.IsAny<object[]>()))
                .Returns(Task.CompletedTask);

            // Act
            await _wakeLockService.StartVisibilityMonitoringAsync();

            // Assert
            _mockJSRuntime.Verify(js => js.InvokeVoidAsync("wakeLockInterop.startVisibilityMonitoring", It.IsAny<object[]>()), 
                Times.Once);
        }

        [Fact]
        public async Task StartVisibilityMonitoringAsync_WhenAlreadyActive_DoesNotCallJSAgain()
        {
            // Arrange
            _mockJSRuntime.Setup(js => js.InvokeVoidAsync("wakeLockInterop.startVisibilityMonitoring", It.IsAny<object[]>()))
                .Returns(Task.CompletedTask);
            
            await _wakeLockService.StartVisibilityMonitoringAsync(); // First call

            // Act
            await _wakeLockService.StartVisibilityMonitoringAsync(); // Second call

            // Assert
            _mockJSRuntime.Verify(js => js.InvokeVoidAsync("wakeLockInterop.startVisibilityMonitoring", It.IsAny<object[]>()), 
                Times.Once);
        }

        [Fact]
        public async Task StartVisibilityMonitoringAsync_WhenJSExceptionThrown_ThrowsInvalidOperationException()
        {
            // Arrange
            _mockJSRuntime.Setup(js => js.InvokeVoidAsync("wakeLockInterop.startVisibilityMonitoring", It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("Monitoring error"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _wakeLockService.StartVisibilityMonitoringAsync());
            
            Assert.Contains("JavaScript error", exception.Message);
        }

        [Fact]
        public async Task StopVisibilityMonitoringAsync_WhenActive_StopsSuccessfully()
        {
            // Arrange
            _mockJSRuntime.Setup(js => js.InvokeVoidAsync("wakeLockInterop.startVisibilityMonitoring", It.IsAny<object[]>()))
                .Returns(Task.CompletedTask);
            _mockJSRuntime.Setup(js => js.InvokeVoidAsync("wakeLockInterop.stopVisibilityMonitoring", It.IsAny<object[]>()))
                .Returns(Task.CompletedTask);
            
            await _wakeLockService.StartVisibilityMonitoringAsync();

            // Act
            await _wakeLockService.StopVisibilityMonitoringAsync();

            // Assert
            _mockJSRuntime.Verify(js => js.InvokeVoidAsync("wakeLockInterop.stopVisibilityMonitoring", It.IsAny<object[]>()), 
                Times.Once);
        }

        [Fact]
        public async Task StopVisibilityMonitoringAsync_WhenNotActive_DoesNotCallJS()
        {
            // Act
            await _wakeLockService.StopVisibilityMonitoringAsync();

            // Assert
            _mockJSRuntime.Verify(js => js.InvokeVoidAsync("wakeLockInterop.stopVisibilityMonitoring", It.IsAny<object[]>()), 
                Times.Never);
        }

        #endregion

        #region Event Tests

        [Fact]
        public async Task AcquireWakeLockAsync_RaisesWakeLockStatusChangedEvent()
        {
            // Arrange
            SetupSuccessfulAcquisition();
            WakeLockStatusChangedEventArgs? eventArgs = null;
            _wakeLockService.WakeLockStatusChanged += (sender, args) => eventArgs = args;

            // Act
            await _wakeLockService.AcquireWakeLockAsync("Test reason");

            // Assert
            Assert.NotNull(eventArgs);
            Assert.True(eventArgs.IsActive);
            Assert.False(eventArgs.PreviousState);
            Assert.Equal(WakeLockOperationType.Acquire, eventArgs.OperationType);
            Assert.Equal("Test reason", eventArgs.Reason);
            Assert.True(eventArgs.OperationResult.IsSuccess);
        }

        [Fact]
        public async Task ReleaseWakeLockAsync_RaisesWakeLockStatusChangedEvent()
        {
            // Arrange
            SetupSuccessfulAcquisition();
            await _wakeLockService.AcquireWakeLockAsync();
            
            _mockJSRuntime.Setup(js => js.InvokeAsync<bool>("wakeLockInterop.releaseWakeLock", It.IsAny<object[]>()))
                .ReturnsAsync(true);

            WakeLockStatusChangedEventArgs? eventArgs = null;
            _wakeLockService.WakeLockStatusChanged += (sender, args) => eventArgs = args;

            // Act
            await _wakeLockService.ReleaseWakeLockAsync();

            // Assert
            Assert.NotNull(eventArgs);
            Assert.False(eventArgs.IsActive);
            Assert.True(eventArgs.PreviousState);
            Assert.Equal(WakeLockOperationType.Release, eventArgs.OperationType);
            Assert.True(eventArgs.OperationResult.IsSuccess);
        }

        [Fact]
        public void OnVisibilityChanged_RaisesVisibilityChangedEvent()
        {
            // Arrange
            VisibilityChangedEventArgs? eventArgs = null;
            _wakeLockService.VisibilityChanged += (sender, args) => eventArgs = args;

            // Act
            _wakeLockService.OnVisibilityChanged(false, "hidden");

            // Assert
            Assert.NotNull(eventArgs);
            Assert.False(eventArgs.IsVisible);
            Assert.True(eventArgs.PreviousState);
            Assert.Equal("hidden", eventArgs.VisibilityState);
            Assert.False(_wakeLockService.IsPageVisible);
        }

        [Fact]
        public async Task OnVisibilityChanged_WhenHiddenAndWakeLockActive_AutoReleasesWakeLock()
        {
            // Arrange
            SetupSuccessfulAcquisition();
            await _wakeLockService.AcquireWakeLockAsync();
            
            _mockJSRuntime.Setup(js => js.InvokeAsync<bool>("wakeLockInterop.releaseWakeLock", It.IsAny<object[]>()))
                .ReturnsAsync(true);

            // Act
            _wakeLockService.OnVisibilityChanged(false, "hidden");

            // Give time for the auto-release task to complete
            await Task.Delay(100);

            // Assert
            Assert.False(_wakeLockService.IsPageVisible);
            // Note: Auto-release happens in background task, so we verify the JS call was made
            _mockJSRuntime.Verify(js => js.InvokeAsync<bool>("wakeLockInterop.releaseWakeLock", It.IsAny<object[]>()), 
                Times.Once);
        }

        #endregion

        #region Disposal Tests

        [Fact]
        public async Task Dispose_WhenWakeLockActive_ReleasesWakeLock()
        {
            // Arrange
            SetupSuccessfulAcquisition();
            await _wakeLockService.AcquireWakeLockAsync();
            
            _mockJSRuntime.Setup(js => js.InvokeAsync<bool>("wakeLockInterop.releaseWakeLock", It.IsAny<object[]>()))
                .ReturnsAsync(true);

            // Act
            _wakeLockService.Dispose();

            // Give time for disposal tasks to complete
            await Task.Delay(500);

            // Assert
            _mockJSRuntime.Verify(js => js.InvokeAsync<bool>("wakeLockInterop.releaseWakeLock", It.IsAny<object[]>()), 
                Times.Once);
        }

        [Fact]
        public async Task Dispose_WhenVisibilityMonitoringActive_StopsMonitoring()
        {
            // Arrange
            _mockJSRuntime.Setup(js => js.InvokeVoidAsync("wakeLockInterop.startVisibilityMonitoring", It.IsAny<object[]>()))
                .Returns(Task.CompletedTask);
            _mockJSRuntime.Setup(js => js.InvokeVoidAsync("wakeLockInterop.stopVisibilityMonitoring", It.IsAny<object[]>()))
                .Returns(Task.CompletedTask);
            
            await _wakeLockService.StartVisibilityMonitoringAsync();

            // Act
            _wakeLockService.Dispose();

            // Give time for disposal tasks to complete
            await Task.Delay(500);

            // Assert
            _mockJSRuntime.Verify(js => js.InvokeVoidAsync("wakeLockInterop.stopVisibilityMonitoring", It.IsAny<object[]>()), 
                Times.Once);
        }

        [Fact]
        public async Task MethodsAfterDispose_ThrowObjectDisposedException()
        {
            // Arrange
            _wakeLockService.Dispose();

            // Act & Assert
            await Assert.ThrowsAsync<ObjectDisposedException>(() => _wakeLockService.AcquireWakeLockAsync());
            await Assert.ThrowsAsync<ObjectDisposedException>(() => _wakeLockService.ReleaseWakeLockAsync());
            await Assert.ThrowsAsync<ObjectDisposedException>(() => _wakeLockService.StartVisibilityMonitoringAsync());
            await Assert.ThrowsAsync<ObjectDisposedException>(() => _wakeLockService.StopVisibilityMonitoringAsync());
        }

        #endregion

        #region Thread Safety Tests

        [Fact]
        public async Task ConcurrentWakeLockOperations_HandledSafely()
        {
            // Arrange
            SetupSuccessfulAcquisition();
            _mockJSRuntime.Setup(js => js.InvokeAsync<bool>("wakeLockInterop.releaseWakeLock", It.IsAny<object[]>()))
                .ReturnsAsync(true);

            // Act - Run multiple acquire and release operations concurrently
            var tasks = new[]
            {
                _wakeLockService.AcquireWakeLockAsync("Task 1"),
                _wakeLockService.AcquireWakeLockAsync("Task 2"),
                _wakeLockService.ReleaseWakeLockAsync(),
                _wakeLockService.AcquireWakeLockAsync("Task 3")
            };

            var results = await Task.WhenAll(tasks);

            // Assert - All operations should complete without throwing
            Assert.All(results, result => Assert.NotNull(result));
        }

        [Fact]
        public void PropertyAccess_IsThreadSafe()
        {
            // Act & Assert - Multiple threads accessing properties should not throw
            var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
            {
                var isActive = _wakeLockService.IsWakeLockActive;
                var isVisible = _wakeLockService.IsPageVisible;
                return (isActive, isVisible);
            }));

            // Should complete without exceptions - xUnit tests fail if exceptions are thrown
            Task.WaitAll(tasks.ToArray());
        }

        #endregion

        #region Helper Methods

        private void SetupSuccessfulAcquisition()
        {
            _mockJSRuntime.Setup(js => js.InvokeAsync<bool>("wakeLockInterop.isWakeLockSupported", It.IsAny<object[]>()))
                .ReturnsAsync(true);
            _mockJSRuntime.Setup(js => js.InvokeAsync<bool>("wakeLockInterop.acquireWakeLock", It.IsAny<object[]>()))
                .ReturnsAsync(true);
        }

        public void Dispose()
        {
            _wakeLockService?.Dispose();
        }

        #endregion
    }
}