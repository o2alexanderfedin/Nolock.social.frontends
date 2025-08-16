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
        private readonly Mock<IJSRuntime> _jsRuntimeMock;
        private readonly ConnectivityService _connectivityService;

        public ConnectivityServiceTests()
        {
            _jsRuntimeMock = new Mock<IJSRuntime>();
            _connectivityService = new ConnectivityService(_jsRuntimeMock.Object);
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
            _jsRuntimeMock.Setup(x => x.InvokeAsync<bool>("connectivity.isOnline", It.IsAny<object[]>()))
                .ReturnsAsync(expectedOnline);

            // Act
            var result = await _connectivityService.IsOnlineAsync();

            // Assert
            result.Should().Be(expectedOnline, $"Should correctly detect when {scenario}");
            _jsRuntimeMock.Verify(x => x.InvokeAsync<bool>("connectivity.isOnline", It.IsAny<object[]>()), 
                Times.Once);
        }

        [Fact]
        public async Task IsOnlineAsync_WithJSException_ShouldHandleGracefully()
        {
            // Arrange
            _jsRuntimeMock.Setup(x => x.InvokeAsync<bool>("connectivity.isOnline", It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("Navigator not available"));

            // Act & Assert - Should default to offline when JS fails
            var result = await _connectivityService.IsOnlineAsync();
            result.Should().BeFalse("Should default to offline when connectivity check fails");
        }

        #endregion

        #region Monitoring Lifecycle

        [Fact]
        public async Task StartMonitoringAsync_ShouldInitializeEventListeners()
        {
            // Arrange
            _jsRuntimeMock.Setup(x => x.InvokeAsync<object>("connectivity.startMonitoring", It.IsAny<object[]>()))
                .Returns(ValueTask.FromResult<object>(true));

            // Act
            await _connectivityService.StartMonitoringAsync();

            // Assert
            _jsRuntimeMock.Verify(x => x.InvokeAsync<object>("connectivity.startMonitoring", 
                It.IsAny<object[]>()), Times.Once);
        }

        [Fact]
        public async Task StopMonitoringAsync_ShouldCleanupEventListeners()
        {
            // Arrange
            _jsRuntimeMock.Setup(x => x.InvokeAsync<object>("connectivity.stopMonitoring", It.IsAny<object[]>()))
                .Returns(ValueTask.FromResult<object>(true));

            // Act
            await _connectivityService.StopMonitoringAsync();

            // Assert
            _jsRuntimeMock.Verify(x => x.InvokeAsync<object>("connectivity.stopMonitoring", 
                It.IsAny<object[]>()), Times.Once);
        }

        [Fact]
        public async Task StartMonitoring_ThenStop_ShouldWorkCorrectly()
        {
            // Arrange
            _jsRuntimeMock.Setup(x => x.InvokeAsync<object>(It.IsAny<string>(), It.IsAny<object[]>()))
                .Returns(ValueTask.FromResult<object>(true));

            // Act
            await _connectivityService.StartMonitoringAsync();
            await _connectivityService.StopMonitoringAsync();

            // Assert
            _jsRuntimeMock.Verify(x => x.InvokeAsync<object>("connectivity.startMonitoring", 
                It.IsAny<object[]>()), Times.Once);
            _jsRuntimeMock.Verify(x => x.InvokeAsync<object>("connectivity.stopMonitoring", 
                It.IsAny<object[]>()), Times.Once);
        }

        #endregion

        #region Event Handling

        [Fact]
        public void OnOnlineEvent_WhenRaised_ShouldTriggerHandlers()
        {
            // Arrange
            var eventFired = false;
            ConnectivityEventArgs? receivedArgs = null;

            _connectivityService.OnOnline += (sender, args) =>
            {
                eventFired = true;
                receivedArgs = args;
            };

            // Act - Simulate online event from JavaScript
            var eventArgs = new ConnectivityEventArgs 
            { 
                IsOnline = true, 
                Timestamp = DateTime.UtcNow,
                PreviousState = false
            };
            
            // Simulate the event being raised
            var onOnlineEvent = typeof(ConnectivityService)
                .GetEvent("OnOnline", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            
            var raiseMethod = typeof(ConnectivityService)
                .GetMethod("RaiseOnOnline", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (raiseMethod != null)
            {
                raiseMethod.Invoke(_connectivityService, new object[] { eventArgs });
            }

            // Assert
            eventFired.Should().BeTrue("OnOnline event should be raised");
            receivedArgs?.IsOnline.Should().BeTrue("Event args should indicate online state");
        }

        [Fact]
        public void OnOfflineEvent_WhenRaised_ShouldTriggerHandlers()
        {
            // Arrange
            var eventFired = false;
            ConnectivityEventArgs? receivedArgs = null;

            _connectivityService.OnOffline += (sender, args) =>
            {
                eventFired = true;
                receivedArgs = args;
            };

            // Act - Simulate offline event
            var eventArgs = new ConnectivityEventArgs 
            { 
                IsOnline = false, 
                Timestamp = DateTime.UtcNow,
                PreviousState = true
            };

            // Simulate the event being raised
            var raiseMethod = typeof(ConnectivityService)
                .GetMethod("RaiseOnOffline", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (raiseMethod != null)
            {
                raiseMethod.Invoke(_connectivityService, new object[] { eventArgs });
            }

            // Assert
            eventFired.Should().BeTrue("OnOffline event should be raised");
            receivedArgs?.IsOnline.Should().BeFalse("Event args should indicate offline state");
        }

        [Theory]
        [InlineData(true, false, "going offline")]
        [InlineData(false, true, "coming online")]
        [InlineData(true, true, "staying online")]
        [InlineData(false, false, "staying offline")]
        public void ConnectivityEvents_ShouldIncludeStateTransitionInfo(
            bool previousState, bool currentState, string scenario)
        {
            // Arrange
            ConnectivityEventArgs? receivedArgs = null;
            var eventType = currentState ? "OnOnline" : "OnOffline";

            if (currentState)
            {
                _connectivityService.OnOnline += (sender, args) => receivedArgs = args;
            }
            else
            {
                _connectivityService.OnOffline += (sender, args) => receivedArgs = args;
            }

            // Act
            var eventArgs = new ConnectivityEventArgs 
            { 
                IsOnline = currentState, 
                Timestamp = DateTime.UtcNow,
                PreviousState = previousState
            };

            var raiseMethodName = currentState ? "RaiseOnOnline" : "RaiseOnOffline";
            var raiseMethod = typeof(ConnectivityService)
                .GetMethod(raiseMethodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (raiseMethod != null)
            {
                raiseMethod.Invoke(_connectivityService, new object[] { eventArgs });
            }

            // Assert
            receivedArgs.Should().NotBeNull($"Event should be triggered for {scenario}");
            receivedArgs!.IsOnline.Should().Be(currentState);
            receivedArgs.PreviousState.Should().Be(previousState);
            receivedArgs.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        #endregion

        #region Error Handling and Edge Cases

        [Fact]
        public async Task StartMonitoring_WithJSException_ShouldHandleGracefully()
        {
            // Arrange
            _jsRuntimeMock.Setup(x => x.InvokeAsync<object>("connectivity.startMonitoring", It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("Browser not supported"));

            // Act & Assert
            await _connectivityService.Invoking(s => s.StartMonitoringAsync())
                .Should().NotThrowAsync("Should handle JS exceptions gracefully");
        }

        [Fact]
        public async Task StopMonitoring_WithJSException_ShouldHandleGracefully()
        {
            // Arrange
            _jsRuntimeMock.Setup(x => x.InvokeAsync<object>("connectivity.stopMonitoring", It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("Event listeners not found"));

            // Act & Assert
            await _connectivityService.Invoking(s => s.StopMonitoringAsync())
                .Should().NotThrowAsync("Should handle cleanup errors gracefully");
        }

        [Fact]
        public async Task MultipleStartCalls_ShouldNotCauseIssues()
        {
            // Arrange
            _jsRuntimeMock.Setup(x => x.InvokeAsync<object>("connectivity.startMonitoring", It.IsAny<object[]>()))
                .Returns(ValueTask.FromResult<object>(true));

            // Act - Call start multiple times
            await _connectivityService.StartMonitoringAsync();
            await _connectivityService.StartMonitoringAsync();
            await _connectivityService.StartMonitoringAsync();

            // Assert - Should handle multiple calls gracefully
            _jsRuntimeMock.Verify(x => x.InvokeAsync<object>("connectivity.startMonitoring", 
                It.IsAny<object[]>()), Times.AtLeast(1));
        }

        [Fact]
        public async Task StopWithoutStart_ShouldHandleGracefully()
        {
            // Arrange
            _jsRuntimeMock.Setup(x => x.InvokeAsync<object>("connectivity.stopMonitoring", It.IsAny<object[]>()))
                .Returns(ValueTask.FromResult<object>(true));

            // Act & Assert
            await _connectivityService.Invoking(s => s.StopMonitoringAsync())
                .Should().NotThrowAsync("Should handle stop without start gracefully");
        }

        #endregion

        #region Service Lifecycle

        [Fact]
        public void ServiceDisposal_ShouldCleanupResources()
        {
            // Arrange
            _jsRuntimeMock.Setup(x => x.InvokeAsync<object>("connectivity.stopMonitoring", It.IsAny<object[]>()))
                .Returns(ValueTask.FromResult<object>(true));

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
            _jsRuntimeMock.Setup(x => x.InvokeAsync<bool>("connectivity.isOnline", It.IsAny<object[]>()))
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
            _jsRuntimeMock.Setup(x => x.InvokeAsync<bool>("connectivity.isOnline", It.IsAny<object[]>()))
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
            _jsRuntimeMock.Verify(x => x.InvokeAsync<bool>("connectivity.isOnline", It.IsAny<object[]>()), 
                Times.Exactly(tasks.Length));
        }

        #endregion
    }
}