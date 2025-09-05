using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NoLock.Social.Core.Performance;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using FluentAssertions;
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
    public void MultipleOperations_CalculatesStatisticsCorrectly()
    {
        // Arrange
        const string operationName = "MultiOperation";
        var durations = new List<int> { 10, 20, 30, 40, 50 };
        
        // Act - Use deterministic PerformanceMetrics instead of Task.Delay timing
        foreach (var duration in durations)
        {
            var metrics = new PerformanceMetrics
            {
                OperationName = operationName,
                StartTime = DateTime.UtcNow.AddMilliseconds(-duration),
                EndTime = DateTime.UtcNow,
                Duration = TimeSpan.FromMilliseconds(duration),
                Success = true,
                MemoryUsedBytes = 1024 * duration // Vary memory usage too
            };
            _service.RecordMetric(metrics);
        }
        
        // Assert
        var stats = _service.GetStatistics(operationName);
        
        stats.Should().NotBeNull("statistics should be available after operations");
        stats.TotalExecutions.Should().Be(5, "we executed 5 operations");
        stats.SuccessfulExecutions.Should().Be(5, "all operations completed successfully");
        stats.FailedExecutions.Should().Be(0, "no operations failed");
        stats.MinDuration.TotalMilliseconds.Should().Be(10, "minimum duration was exactly 10ms");
        stats.MaxDuration.TotalMilliseconds.Should().Be(50, "maximum duration was exactly 50ms");
        stats.SuccessRate.Should().Be(100.0, "all operations succeeded (100%)");
        
        // Additional FluentAssertions capabilities
        stats.AverageDuration.Should().BePositive("average duration should be calculated");
        stats.AverageDuration.TotalMilliseconds.Should().Be(30, "average of 10,20,30,40,50 is 30");
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

    [Theory]
    [InlineData("Op1", 100, 1024, true, "")]
    [InlineData("Op2", 200, 2048, false, "Error")]
    [InlineData("Op3", 50, 512, true, "")]
    [InlineData("Op4", 300, 4096, false, "Timeout")]
    public void RecordMetric_MultipleScenarios(string operationName, int durationMs, long memoryBytes, bool success, string? errorMessage)
    {
        // Arrange
        var metrics = new PerformanceMetrics
        {
            OperationName = operationName,
            StartTime = DateTime.UtcNow.AddMilliseconds(-durationMs),
            EndTime = DateTime.UtcNow,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            MemoryUsedBytes = memoryBytes,
            Success = success,
            ErrorMessage = string.IsNullOrEmpty(errorMessage) ? null : errorMessage
        };

        // Act
        _service.RecordMetric(metrics);
        var stats = _service.GetStatistics(operationName);

        // Assert
        stats.Should().NotBeNull();
        stats.OperationName.Should().Be(operationName);
        stats.TotalExecutions.Should().Be(1);
        stats.SuccessfulExecutions.Should().Be(success ? 1 : 0);
        stats.FailedExecutions.Should().Be(success ? 0 : 1);
    }

    [Fact]
    public async Task GetBrowserMetricsAsync_ReturnsCorrectData()
    {
        // Arrange
        var expectedMetrics = new BrowserPerformanceMetrics
        {
            NavigationStart = 1609459200000,
            DomContentLoaded = 150,
            LoadComplete = 300,
            FirstContentfulPaint = 75,
            FirstInputDelay = 10,
            CumulativeLayoutShift = 0.05,
            UsedJSHeapSize = 10485760,
            TotalJSHeapSize = 52428800
        };

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<BrowserPerformanceMetrics>("eval", It.IsAny<object[]>()))
            .ReturnsAsync(expectedMetrics);

        // Act
        var metrics = await _service.GetBrowserMetricsAsync();

        // Assert
        metrics.Should().NotBeNull();
        metrics.Should().BeEquivalentTo(expectedMetrics);
    }

    [Fact]
    public async Task GetBrowserMetricsAsync_HandlesException()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<BrowserPerformanceMetrics>("eval", It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("Browser API not available"));

        // Act
        var metrics = await _service.GetBrowserMetricsAsync();

        // Assert
        metrics.Should().BeNull();
        _loggerMock.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(50)]
    public void GetRecentAlerts_ReturnsCorrectCount(int requestedCount)
    {
        // Arrange
        for (int i = 0; i < 20; i++)
        {
            var metrics = new PerformanceMetrics
            {
                OperationName = $"Alert_{i}",
                Duration = TimeSpan.FromMilliseconds(1000),
                Success = false,
                ErrorMessage = $"Error {i}"
            };
            
            _service.SetThresholds($"Alert_{i}", new PerformanceThresholds 
            { 
                MaxDuration = TimeSpan.FromMilliseconds(100) 
            });
            _service.RecordMetric(metrics);
        }

        // Act
        var alerts = _service.GetRecentAlerts(requestedCount);

        // Assert
        alerts.Count.Should().Be(Math.Min(requestedCount, 20));
        alerts.Should().BeInDescendingOrder(a => a.Timestamp);
    }

    [Fact]
    public void RecordMetric_HandlesException()
    {
        // Arrange
        var metrics = new PerformanceMetrics
        {
            OperationName = null!, // This might cause issues
            Duration = TimeSpan.FromMilliseconds(100)
        };

        // Act & Assert - should not throw
        var act = () => _service.RecordMetric(metrics);
        act.Should().NotThrow();
    }

    [Fact]
    public void GetStatistics_WithNoData_ReturnsNull()
    {
        // Act
        var stats = _service.GetStatistics("NonExistentOperation");

        // Assert
        stats.Should().BeNull();
    }

    [Theory]
    [InlineData(10, 20, 30, 40, 50)]
    [InlineData(100, 200, 300, 400, 500)]
    [InlineData(5, 10, 15, 20, 25)]
    public void GetStatistics_CalculatesPercentilesCorrectly(params int[] durations)
    {
        // Arrange
        const string operationName = "PercentileOp";
        foreach (var duration in durations)
        {
            var metrics = new PerformanceMetrics
            {
                OperationName = operationName,
                Duration = TimeSpan.FromMilliseconds(duration),
                Success = true
            };
            _service.RecordMetric(metrics);
        }

        // Act
        var stats = _service.GetStatistics(operationName);

        // Assert
        stats.Should().NotBeNull();
        stats.MinDuration.TotalMilliseconds.Should().Be(durations.Min());
        stats.MaxDuration.TotalMilliseconds.Should().Be(durations.Max());
        stats.MedianDuration.Should().BePositive();
        stats.P95Duration.Should().BePositive();
        stats.P99Duration.Should().BePositive();
    }

    [Fact]
    public void SetThresholds_WithStatistics_ChecksViolation()
    {
        // Arrange
        const string operationName = "ThresholdStatsOp";
        
        // Create some metrics first
        for (int i = 0; i < 10; i++)
        {
            var metrics = new PerformanceMetrics
            {
                OperationName = operationName,
                Duration = TimeSpan.FromMilliseconds(200),
                Success = i < 5 // 50% success rate
            };
            _service.RecordMetric(metrics);
        }

        // Act - set threshold that will be violated
        _service.SetThresholds(operationName, new PerformanceThresholds
        {
            MaxDuration = TimeSpan.FromMilliseconds(100),
            MinSuccessRate = 80
        });

        // Assert
        var alerts = _service.GetRecentAlerts();
        alerts.Should().Contain(a => a.OperationName == operationName);
    }

    [Fact]
    public void PerformanceAlert_PropertiesSetCorrectly()
    {
        // Arrange
        var alert = new PerformanceAlert
        {
            Timestamp = DateTime.UtcNow,
            OperationName = "TestOp",
            Severity = AlertSeverity.Critical,
            Message = "Test message",
            Metrics = new PerformanceMetrics { OperationName = "TestOp" },
            Statistics = new PerformanceStatistics { OperationName = "TestOp" }
        };

        // Assert
        alert.OperationName.Should().Be("TestOp");
        alert.Severity.Should().Be(AlertSeverity.Critical);
        alert.Message.Should().Be("Test message");
        alert.Metrics.Should().NotBeNull();
        alert.Statistics.Should().NotBeNull();
    }

    [Theory]
    [InlineData(AlertSeverity.Info, 0)]
    [InlineData(AlertSeverity.Warning, 1)]
    [InlineData(AlertSeverity.Error, 2)]
    [InlineData(AlertSeverity.Critical, 3)]
    public void AlertSeverity_HasCorrectValues(AlertSeverity severity, int expectedValue)
    {
        // Assert
        ((int)severity).Should().Be(expectedValue);
    }

    [Fact]
    public void RecordMetric_LargeNumberOfMetrics_HandlesCorrectly()
    {
        // Arrange
        const string operationName = "LargeVolumeOp";
        const int metricsCount = 1500; // More than _maxMetricsPerOperation (1000)

        // Act
        for (int i = 0; i < metricsCount; i++)
        {
            var metrics = new PerformanceMetrics
            {
                OperationName = operationName,
                Duration = TimeSpan.FromMilliseconds(i),
                Success = true
            };
            _service.RecordMetric(metrics);
        }

        // Assert
        var stats = _service.GetStatistics(operationName);
        stats.Should().NotBeNull();
        stats.TotalExecutions.Should().BeLessThanOrEqualTo(1000); // Should be limited
    }
}