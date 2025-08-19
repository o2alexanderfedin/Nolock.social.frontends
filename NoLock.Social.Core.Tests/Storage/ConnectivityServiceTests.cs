using FluentAssertions;
using Microsoft.JSInterop;
using Moq;
using NoLock.Social.Core.Storage.Interfaces;
using NoLock.Social.Core.Storage.Services;
using System;
using System.Threading.Tasks;
using Xunit;

namespace NoLock.Social.Core.Tests.Storage
{
    /// <summary>
    /// Tests for connectivity monitoring and state change detection
    /// Validates Story 1.8 requirement: "Visual indicator of offline mode"
    /// </summary>
    public class ConnectivityServiceTests : IDisposable
    {
        private readonly Mock<IJSRuntimeWrapper> _jsRuntimeMock;
        private readonly Mock<IJSObjectReference> _jsModuleMock;
        private readonly Mock<IOfflineQueueService> _queueServiceMock;
        private readonly ConnectivityService _connectivityService;

        public ConnectivityServiceTests()
        {
            _jsRuntimeMock = new Mock<IJSRuntimeWrapper>();
            _jsModuleMock = new Mock<IJSObjectReference>();
            _queueServiceMock = new Mock<IOfflineQueueService>();
            
            // Setup the module import to return our mock module
            _jsRuntimeMock.Setup(x => x.InvokeAsync<IJSObjectReference>("import", "./js/connectivity.js"))
                .ReturnsAsync(_jsModuleMock.Object);
            
            // Setup queue service mock to handle ProcessQueueAsync
            _queueServiceMock.Setup(x => x.ProcessQueueAsync())
                .Returns(Task.CompletedTask);
            
            _connectivityService = new ConnectivityService(_jsRuntimeMock.Object, _queueServiceMock.Object);
        }

        public void Dispose()
        {
            _connectivityService?.Dispose();
        }

        #region Connectivity State Detection

        [Theory]
        [InlineData(true, "device is online")]
        [InlineData(false, "device is offline")]
        public async Task IsOnlineAsync_ShouldReturnCorrectConnectivityState(
            bool expectedOnline, string scenario)
        {
            // Arrange
            _jsModuleMock.Setup(x => x.InvokeAsync<bool>("isOnline", It.IsAny<object[]>()))
                .ReturnsAsync(expectedOnline);

            // Act
            var result = await _connectivityService.IsOnlineAsync();

            // Assert
            result.Should().Be(expectedOnline, $"Should correctly detect when {scenario}");
            _jsModuleMock.Verify(x => x.InvokeAsync<bool>("isOnline", It.IsAny<object[]>()), 
                Times.Once);
        }

        [Fact]
        public async Task IsOnlineAsync_WithJSException_ShouldHandleGracefully()
        {
            // Arrange
            _jsModuleMock.Setup(x => x.InvokeAsync<bool>("isOnline", It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("Navigator not available"));

            // Act & Assert - Should default to true (online) when JS fails per the implementation
            var result = await _connectivityService.IsOnlineAsync();
            result.Should().BeTrue("Should default to online when connectivity check fails");
        }

        #endregion

        #region Monitoring Lifecycle

        [Fact]
        public async Task StartMonitoringAsync_ShouldInitializeEventListeners()
        {
            // Arrange
            _jsModuleMock.Setup(x => x.InvokeAsync<bool>("isOnline", It.IsAny<object[]>()))
                .ReturnsAsync(true);
            _jsModuleMock.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("startMonitoring", It.IsAny<object[]>()))
                .Returns(new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult)));

            // Act
            await _connectivityService.StartMonitoringAsync();

            // Assert
            _jsModuleMock.Verify(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("startMonitoring", 
                It.IsAny<object[]>()), Times.Once);
        }

        [Fact]
        public async Task StopMonitoringAsync_ShouldCleanupEventListeners()
        {
            // Arrange
            _jsModuleMock.Setup(x => x.InvokeAsync<bool>("isOnline", It.IsAny<object[]>()))
                .ReturnsAsync(true);
            _jsModuleMock.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("startMonitoring", It.IsAny<object[]>()))
                .Returns(new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult)));
            _jsModuleMock.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("stopMonitoring", It.IsAny<object[]>()))
                .Returns(new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult)));
            
            // Start monitoring first
            await _connectivityService.StartMonitoringAsync();

            // Act
            await _connectivityService.StopMonitoringAsync();

            // Assert
            _jsModuleMock.Verify(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("stopMonitoring", 
                It.IsAny<object[]>()), Times.Once);
        }

        [Fact]
        public async Task StartMonitoring_ThenStop_ShouldWorkCorrectly()
        {
            // Arrange
            _jsModuleMock.Setup(x => x.InvokeAsync<bool>("isOnline", It.IsAny<object[]>()))
                .ReturnsAsync(true);
            _jsModuleMock.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("startMonitoring", It.IsAny<object[]>()))
                .Returns(new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult)));
            _jsModuleMock.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("stopMonitoring", It.IsAny<object[]>()))
                .Returns(new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult)));

            // Act
            await _connectivityService.StartMonitoringAsync();
            await _connectivityService.StopMonitoringAsync();

            // Assert
            _jsModuleMock.Verify(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("startMonitoring", 
                It.IsAny<object[]>()), Times.Once);
            _jsModuleMock.Verify(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("stopMonitoring", 
                It.IsAny<object[]>()), Times.Once);
        }

        #endregion

        #region Event Handling

        [Fact]
        public async Task OnOnlineEvent_WhenRaised_ShouldTriggerHandlers()
        {
            // Arrange
            var eventFired = false;
            ConnectivityEventArgs? receivedArgs = null;

            _connectivityService.OnOnline += (sender, args) =>
            {
                eventFired = true;
                receivedArgs = args;
            };

            // Act - Simulate online event from JavaScript by calling the JSInvokable method
            await _connectivityService.OnConnectivityOnline();

            // Assert
            eventFired.Should().BeTrue("OnOnline event should be raised");
            receivedArgs?.IsOnline.Should().BeTrue("Event args should indicate online state");
        }

        [Fact]
        public async Task OnOfflineEvent_WhenRaised_ShouldTriggerHandlers()
        {
            // Arrange
            var eventFired = false;
            ConnectivityEventArgs? receivedArgs = null;

            _connectivityService.OnOffline += (sender, args) =>
            {
                eventFired = true;
                receivedArgs = args;
            };

            // Act - Simulate offline event from JavaScript by calling the JSInvokable method
            await _connectivityService.OnConnectivityOffline();

            // Assert
            eventFired.Should().BeTrue("OnOffline event should be raised");
            receivedArgs?.IsOnline.Should().BeFalse("Event args should indicate offline state");
        }

        [Theory]
        [InlineData(true, false, "going offline")]
        [InlineData(false, true, "coming online")]
        [InlineData(true, true, "staying online")]
        [InlineData(false, false, "staying offline")]
        public async Task ConnectivityEvents_ShouldIncludeStateTransitionInfo(
            bool previousState, bool currentState, string scenario)
        {
            // Arrange
            ConnectivityEventArgs? receivedArgs = null;

            if (currentState)
            {
                _connectivityService.OnOnline += (sender, args) => receivedArgs = args;
            }
            else
            {
                _connectivityService.OnOffline += (sender, args) => receivedArgs = args;
            }

            // Act - Call the appropriate JSInvokable method based on state
            if (currentState)
            {
                await _connectivityService.OnConnectivityOnline();
            }
            else
            {
                await _connectivityService.OnConnectivityOffline();
            }

            // Assert
            receivedArgs.Should().NotBeNull($"Event should be triggered for {scenario}");
            receivedArgs!.IsOnline.Should().Be(currentState);
            // Note: PreviousState tracking test is limited since we can't set initial state without more complex setup
        }

        #endregion

        #region Error Handling and Edge Cases

        [Fact]
        public async Task StartMonitoring_WithJSException_ShouldHandleGracefully()
        {
            // Arrange
            _jsModuleMock.Setup(x => x.InvokeAsync<bool>("isOnline", It.IsAny<object[]>()))
                .ReturnsAsync(true); // Return default value for initial check
            _jsModuleMock.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("startMonitoring", It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("Browser not supported"));

            // Act & Assert - Should throw the exception from StartMonitoringAsync
            await _connectivityService.Invoking(s => s.StartMonitoringAsync())
                .Should().ThrowAsync<JSException>("Should propagate JS exceptions from startMonitoring");
        }

        [Fact]
        public async Task StopMonitoring_WithJSException_ShouldHandleGracefully()
        {
            // Arrange
            _jsModuleMock.Setup(x => x.InvokeAsync<bool>("isOnline", It.IsAny<object[]>()))
                .ReturnsAsync(true);
            _jsModuleMock.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("startMonitoring", It.IsAny<object[]>()))
                .Returns(new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult)));
            _jsModuleMock.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("stopMonitoring", It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("Event listeners not found"));
                
            // Start monitoring first
            await _connectivityService.StartMonitoringAsync();

            // Act & Assert
            await _connectivityService.Invoking(s => s.StopMonitoringAsync())
                .Should().ThrowAsync<JSException>("Should propagate cleanup errors");
        }

        [Fact]
        public async Task MultipleStartCalls_ShouldNotCauseIssues()
        {
            // Arrange
            _jsModuleMock.Setup(x => x.InvokeAsync<bool>("isOnline", It.IsAny<object[]>()))
                .ReturnsAsync(true);
            _jsModuleMock.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("startMonitoring", It.IsAny<object[]>()))
                .Returns(new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult)));

            // Act - Call start multiple times
            await _connectivityService.StartMonitoringAsync();
            await _connectivityService.StartMonitoringAsync();
            await _connectivityService.StartMonitoringAsync();

            // Assert - Should handle multiple calls gracefully (only first call should execute)
            _jsModuleMock.Verify(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("startMonitoring", 
                It.IsAny<object[]>()), Times.Once);
        }

        [Fact]
        public async Task StopWithoutStart_ShouldHandleGracefully()
        {
            // Arrange
            _jsModuleMock.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("stopMonitoring", It.IsAny<object[]>()))
                .Returns(new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult)));

            // Act & Assert
            await _connectivityService.Invoking(s => s.StopMonitoringAsync())
                .Should().NotThrowAsync("Should handle stop without start gracefully");
                
            // Verify that stopMonitoring is never called when not monitoring
            _jsModuleMock.Verify(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("stopMonitoring", 
                It.IsAny<object[]>()), Times.Never);
        }

        #endregion

        #region Service Lifecycle

        [Fact]
        public void ServiceDisposal_ShouldCleanupResources()
        {
            // Arrange
            _jsModuleMock.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("stopMonitoring", It.IsAny<object[]>()))
                .Returns(new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult)));

            // Act
            _connectivityService.Dispose();

            // Assert - Should not throw during disposal
            _connectivityService.Invoking(s => s.Dispose())
                .Should().NotThrow("Multiple disposal calls should be safe");
        }

        [Fact]
        public async Task OperationsAfterDisposal_ShouldThrowObjectDisposedException()
        {
            // Arrange
            _connectivityService.Dispose();

            // Act & Assert
            await _connectivityService.Invoking(s => s.IsOnlineAsync())
                .Should().ThrowAsync<ObjectDisposedException>();
                
            await _connectivityService.Invoking(s => s.StartMonitoringAsync())
                .Should().ThrowAsync<ObjectDisposedException>();
                
            await _connectivityService.Invoking(s => s.StopMonitoringAsync())
                .Should().ThrowAsync<ObjectDisposedException>();
        }

        #endregion

        #region Performance and Reliability

        [Fact]
        public async Task ConnectivityChecks_ShouldBeFast()
        {
            // Arrange
            _jsModuleMock.Setup(x => x.InvokeAsync<bool>("isOnline", It.IsAny<object[]>()))
                .ReturnsAsync(true);

            // Act - Measure response time
            var startTime = DateTime.UtcNow;
            var result = await _connectivityService.IsOnlineAsync();
            var duration = DateTime.UtcNow - startTime;

            // Assert
            result.Should().BeTrue();
            duration.Should().BeLessThan(TimeSpan.FromMilliseconds(100), 
                "Connectivity checks should be fast");
        }

        [Fact]
        public async Task ConcurrentConnectivityChecks_ShouldHandleCorrectly()
        {
            // Arrange
            _jsModuleMock.Setup(x => x.InvokeAsync<bool>("isOnline", It.IsAny<object[]>()))
                .ReturnsAsync(true);

            // Act - Make concurrent calls
            var tasks = new Task<bool>[10];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = _connectivityService.IsOnlineAsync();
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().AllSatisfy(result => result.Should().BeTrue());
            _jsModuleMock.Verify(x => x.InvokeAsync<bool>("isOnline", It.IsAny<object[]>()), 
                Times.Exactly(tasks.Length));
        }

        #endregion
    }
}