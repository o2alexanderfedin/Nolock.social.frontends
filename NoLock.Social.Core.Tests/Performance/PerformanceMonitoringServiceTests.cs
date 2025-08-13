using NoLock.Social.Core.Performance;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using Xunit;

namespace NoLock.Social.Core.Tests.Performance;

public class PerformanceMonitoringServiceTests
{
    private readonly Mock<ILogger<PerformanceMonitoringService>> _loggerMock;
    private readonly Mock<IJSRuntime> _jsRuntimeMock;
    private readonly PerformanceMonitoringService _service;
    
    public PerformanceMonitoringServiceTests()
    {
        _loggerMock = new Mock<ILogger<PerformanceMonitoringService>>();
        _jsRuntimeMock = new Mock<IJSRuntime>();
        _service = new PerformanceMonitoringService(_loggerMock.Object, _jsRuntimeMock.Object);
    }
    
    [Fact]
    public async Task StartOperation_RecordsMetrics_Successfully()
    {
        // Arrange
        const string operationName = "TestOperation";
        
        // Act
        using (var timer = _service.StartOperation(operationName))
        {
            await Task.Delay(10); // Simulate some work
            timer.AddMetric("TestMetric", 42);
        }
        
        // Assert
        var stats = _service.GetStatistics(operationName);
        Assert.NotNull(stats);
        Assert.Equal(operationName, stats.OperationName);
        Assert.Equal(1, stats.TotalExecutions);
        Assert.Equal(1, stats.SuccessfulExecutions);
        Assert.True(stats.AverageDuration.TotalMilliseconds > 0);
    }
    
    [Fact]
    public void RecordMetric_WithFailure_TracksFailureCorrectly()
    {
        // Arrange
        const string operationName = "FailingOperation";
        
        // Act
        using (var timer = _service.StartOperation(operationName))
        {
            timer.MarkAsFailure("Test failure");
        }
        
        // Assert
        var stats = _service.GetStatistics(operationName);
        Assert.NotNull(stats);
        Assert.Equal(1, stats.TotalExecutions);
        Assert.Equal(0, stats.SuccessfulExecutions);
        Assert.Equal(1, stats.FailedExecutions);
        Assert.Equal(0, stats.SuccessRate);
    }
    
    [Fact]
    public async Task MultipleOperations_CalculatesStatisticsCorrectly()
    {
        // Arrange
        const string operationName = "MultiOperation";
        var durations = new List<int> { 10, 20, 30, 40, 50 };
        
        // Act
        foreach (var duration in durations)
        {
            using (var timer = _service.StartOperation(operationName))
            {
                await Task.Delay(duration);
            }
        }
        
        // Assert
        var stats = _service.GetStatistics(operationName);
        Assert.NotNull(stats);
        Assert.Equal(5, stats.TotalExecutions);
        Assert.Equal(5, stats.SuccessfulExecutions);
        Assert.True(stats.MinDuration.TotalMilliseconds >= 10);
        Assert.True(stats.MaxDuration.TotalMilliseconds >= 50);
    }
    
    [Fact]
    public void SetThresholds_ViolationTriggersAlert()
    {
        // Arrange
        const string operationName = "ThresholdOperation";
        var threshold = new PerformanceThresholds
        {
            MaxDuration = TimeSpan.FromMilliseconds(1) // Very low threshold
        };
        
        _service.SetThresholds(operationName, threshold);
        
        // Act
        using (var timer = _service.StartOperation(operationName))
        {
            Thread.Sleep(10); // Ensure we exceed the threshold
        }
        
        // Assert
        var alerts = _service.GetRecentAlerts();
        Assert.NotEmpty(alerts);
        Assert.Contains(alerts, a => a.OperationName == operationName);
    }
    
    [Fact]
    public void GetAllStatistics_ReturnsAllOperationStats()
    {
        // Arrange & Act
        using (_service.StartOperation("Op1")) { }
        using (_service.StartOperation("Op2")) { }
        using (_service.StartOperation("Op3")) { }
        
        var allStats = _service.GetAllStatistics();
        
        // Assert
        Assert.Equal(3, allStats.Count);
        Assert.Contains("Op1", allStats.Keys);
        Assert.Contains("Op2", allStats.Keys);
        Assert.Contains("Op3", allStats.Keys);
    }
    
    [Fact]
    public void ClearMetrics_RemovesAllData()
    {
        // Arrange
        using (_service.StartOperation("ToClear")) { }
        Assert.NotNull(_service.GetStatistics("ToClear"));
        
        // Act
        _service.ClearMetrics();
        
        // Assert
        Assert.Null(_service.GetStatistics("ToClear"));
        Assert.Empty(_service.GetAllStatistics());
        Assert.Empty(_service.GetRecentAlerts());
    }
    
    [Fact]
    public async Task ExportMetricsAsync_GeneratesValidJson()
    {
        // Arrange
        using (_service.StartOperation("ExportOp")) { }
        
        // Act
        var json = await _service.ExportMetricsAsync();
        
        // Assert
        Assert.NotNull(json);
        Assert.Contains("ExportTime", json);
        Assert.Contains("Statistics", json);
        Assert.Contains("ExportOp", json);
    }
    
    [Fact]
    public void PerformanceTimer_CustomMetrics_AreRecorded()
    {
        // Arrange & Act
        using (var timer = _service.StartOperation("CustomMetricOp"))
        {
            timer.AddMetric("CustomKey1", "CustomValue1");
            timer.AddMetric("CustomKey2", 123);
        }
        
        // Assert - metrics are recorded (we can't directly access them, but operation completes successfully)
        var stats = _service.GetStatistics("CustomMetricOp");
        Assert.NotNull(stats);
        Assert.Equal(1, stats.TotalExecutions);
    }
    
    [Fact]
    public void PerformanceThresholds_IsViolated_ChecksCorrectly()
    {
        // Arrange
        var threshold = new PerformanceThresholds
        {
            MaxDuration = TimeSpan.FromMilliseconds(100),
            MaxMemoryBytes = 1024 * 1024
        };
        
        var metrics = new PerformanceMetrics
        {
            Duration = TimeSpan.FromMilliseconds(150),
            MemoryUsedBytes = 512 * 1024
        };
        
        // Act & Assert
        Assert.True(threshold.IsViolated(metrics)); // Duration exceeds
        
        metrics.Duration = TimeSpan.FromMilliseconds(50);
        metrics.MemoryUsedBytes = 2 * 1024 * 1024;
        Assert.True(threshold.IsViolated(metrics)); // Memory exceeds
        
        metrics.MemoryUsedBytes = 512 * 1024;
        Assert.False(threshold.IsViolated(metrics)); // Both within limits
    }
    
    [Fact]
    public void PerformanceStatistics_SuccessRate_CalculatedCorrectly()
    {
        // Arrange
        const string operationName = "SuccessRateOp";
        
        // Act - 3 successes, 2 failures
        for (var i = 0; i < 3; i++)
        {
            using (_service.StartOperation(operationName)) { }
        }
        
        for (var i = 0; i < 2; i++)
        {
            using (var timer = _service.StartOperation(operationName))
            {
                timer.MarkAsFailure("Test failure");
            }
        }
        
        // Assert
        var stats = _service.GetStatistics(operationName);
        Assert.NotNull(stats);
        Assert.Equal(5, stats.TotalExecutions);
        Assert.Equal(3, stats.SuccessfulExecutions);
        Assert.Equal(2, stats.FailedExecutions);
        Assert.Equal(60, stats.SuccessRate); // 3/5 * 100 = 60%
    }
}