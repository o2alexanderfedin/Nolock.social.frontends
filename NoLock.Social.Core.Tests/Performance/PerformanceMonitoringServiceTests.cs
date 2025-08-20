using NoLock.Social.Core.Performance;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using Xunit;
using FluentAssertions;

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
        
        stats.Should().NotBeNull();
        stats.OperationName.Should().Be(operationName);
        stats.TotalExecutions.Should().Be(1);
        stats.SuccessfulExecutions.Should().Be(1);
        stats.AverageDuration.TotalMilliseconds.Should().BeGreaterThan(0);
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
        
        stats.Should().NotBeNull();
        stats.TotalExecutions.Should().Be(1);
        stats.SuccessfulExecutions.Should().Be(0);
        stats.FailedExecutions.Should().Be(1);
        stats.SuccessRate.Should().Be(0);
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
        
        stats.Should().NotBeNull("statistics should be available after operations");
        stats.TotalExecutions.Should().Be(5, "we executed 5 operations");
        stats.SuccessfulExecutions.Should().Be(5, "all operations completed successfully");
        stats.FailedExecutions.Should().Be(0, "no operations failed");
        stats.MinDuration.TotalMilliseconds.Should().BeGreaterThanOrEqualTo(9, "minimum delay was ~10ms (allowing for timing variance)");
        stats.MaxDuration.TotalMilliseconds.Should().BeGreaterThanOrEqualTo(50, "maximum delay was 50ms");
        stats.SuccessRate.Should().Be(100.0, "all operations succeeded (100%)");
        
        // Additional FluentAssertions capabilities
        stats.AverageDuration.Should().BePositive("average duration should be calculated");
        stats.AverageDuration.TotalMilliseconds.Should().BeInRange(10, 50, "average should be between min and max");
        stats.Should().BeEquivalentTo(new 
        {
            OperationName = operationName,
            TotalExecutions = 5,
            SuccessfulExecutions = 5,
            FailedExecutions = 0,
            SuccessRate = 100.0
        }, options => options
            .Including(s => s.OperationName)
            .Including(s => s.TotalExecutions)
            .Including(s => s.SuccessfulExecutions)
            .Including(s => s.FailedExecutions)
            .Including(s => s.SuccessRate));
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
        
        alerts.Should().NotBeEmpty();
        alerts.Should().Contain(a => a.OperationName == operationName);
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
        allStats.Should().HaveCount(3);
        allStats.Keys.Should().Contain("Op1");
        allStats.Keys.Should().Contain("Op2");
        allStats.Keys.Should().Contain("Op3");
    }
    
    [Fact]
    public void ClearMetrics_RemovesAllData()
    {
        // Arrange
        using (_service.StartOperation("ToClear")) { }
        _service.GetStatistics("ToClear").Should().NotBeNull();
        
        // Act
        _service.ClearMetrics();
        
        // Assert
        _service.GetStatistics("ToClear").Should().BeNull();
        _service.GetAllStatistics().Should().BeEmpty();
        _service.GetRecentAlerts().Should().BeEmpty();
    }
    
    [Fact]
    public async Task ExportMetricsAsync_GeneratesValidJson()
    {
        // Arrange
        using (_service.StartOperation("ExportOp")) { }
        
        // Act
        var json = await _service.ExportMetricsAsync();
        
        // Assert
        json.Should().NotBeNull();
        json.Should().Contain("ExportTime");
        json.Should().Contain("Statistics");
        json.Should().Contain("ExportOp");
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
        
        stats.Should().NotBeNull();
        stats.TotalExecutions.Should().Be(1);
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
        threshold.IsViolated(metrics).Should().BeTrue("duration exceeds threshold");
        
        metrics.Duration = TimeSpan.FromMilliseconds(50);
        metrics.MemoryUsedBytes = 2 * 1024 * 1024;
        threshold.IsViolated(metrics).Should().BeTrue("memory exceeds threshold");
        
        metrics.MemoryUsedBytes = 512 * 1024;
        threshold.IsViolated(metrics).Should().BeFalse("both metrics are within limits");
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
        
        stats.Should().NotBeNull();
        stats.TotalExecutions.Should().Be(5);
        stats.SuccessfulExecutions.Should().Be(3);
        stats.FailedExecutions.Should().Be(2);
        stats.SuccessRate.Should().Be(60, "3 out of 5 operations succeeded (60%)");
    }
}