using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using NoLock.Social.Core.Common.Extensions;

namespace NoLock.Social.Core.Performance;

/// <summary>
/// Service for monitoring application performance
/// </summary>
public interface IPerformanceMonitoringService
{
    /// <summary>
    /// Start tracking a performance operation
    /// </summary>
    PerformanceTimer StartOperation(string operationName);
    
    /// <summary>
    /// Record a completed performance metric
    /// </summary>
    void RecordMetric(PerformanceMetrics metrics);
    
    /// <summary>
    /// Get statistics for a specific operation
    /// </summary>
    PerformanceStatistics? GetStatistics(string operationName);
    
    /// <summary>
    /// Get all operation statistics
    /// </summary>
    IReadOnlyDictionary<string, PerformanceStatistics> GetAllStatistics();
    
    /// <summary>
    /// Set performance thresholds for an operation
    /// </summary>
    void SetThresholds(string operationName, PerformanceThresholds thresholds);
    
    /// <summary>
    /// Get recent performance alerts
    /// </summary>
    IReadOnlyList<PerformanceAlert> GetRecentAlerts(int count = 10);
    
    /// <summary>
    /// Clear all metrics data
    /// </summary>
    void ClearMetrics();
    
    /// <summary>
    /// Export metrics to JSON
    /// </summary>
    Task<string> ExportMetricsAsync();
    
    /// <summary>
    /// Get browser performance metrics
    /// </summary>
    Task<BrowserPerformanceMetrics?> GetBrowserMetricsAsync();
}

/// <summary>
/// Browser performance metrics from Performance API
/// </summary>
public class BrowserPerformanceMetrics
{
    public double NavigationStart { get; set; }
    public double DomContentLoaded { get; set; }
    public double LoadComplete { get; set; }
    public double FirstContentfulPaint { get; set; }
    public double FirstInputDelay { get; set; }
    public double CumulativeLayoutShift { get; set; }
    public long UsedJSHeapSize { get; set; }
    public long TotalJSHeapSize { get; set; }
}

/// <summary>
/// Implementation of performance monitoring service
/// </summary>
public class PerformanceMonitoringService : IPerformanceMonitoringService
{
    private readonly ILogger<PerformanceMonitoringService> _logger;
    private readonly IJSRuntime _jsRuntime;
    private readonly ConcurrentDictionary<string, List<PerformanceMetrics>> _metrics;
    private readonly ConcurrentDictionary<string, PerformanceThresholds> _thresholds;
    private readonly ConcurrentBag<PerformanceAlert> _alerts;
    private readonly int _maxMetricsPerOperation = 1000;
    private readonly int _maxAlerts = 100;
    
    public PerformanceMonitoringService(
        ILogger<PerformanceMonitoringService> logger,
        IJSRuntime jsRuntime)
    {
        _logger = logger;
        _jsRuntime = jsRuntime;
        _metrics = new ConcurrentDictionary<string, List<PerformanceMetrics>>();
        _thresholds = new ConcurrentDictionary<string, PerformanceThresholds>();
        _alerts = new ConcurrentBag<PerformanceAlert>();
    }
    
    public PerformanceTimer StartOperation(string operationName)
    {
        _logger.LogDebug("Starting performance tracking for operation: {OperationName}", operationName);
        return new PerformanceTimer(operationName, metrics => RecordMetric(metrics));
    }
    
    public void RecordMetric(PerformanceMetrics metrics)
    {
        try
        {
            // Add to metrics collection
            var list = _metrics.AddOrUpdate(
                metrics.OperationName,
                new List<PerformanceMetrics> { metrics },
                (key, existing) =>
                {
                    existing.Add(metrics);
                    
                    // Limit the number of stored metrics per operation
                    if (existing.Count > _maxMetricsPerOperation)
                    {
                        existing.RemoveRange(0, existing.Count - _maxMetricsPerOperation);
                    }
                    
                    return existing;
                });
            
            // Check thresholds
            if (_thresholds.TryGetValue(metrics.OperationName, out var threshold))
            {
                if (threshold.IsViolated(metrics))
                {
                    var alert = new PerformanceAlert
                    {
                        Timestamp = DateTime.UtcNow,
                        OperationName = metrics.OperationName,
                        Severity = DetermineSeverity(metrics, threshold),
                        Message = GenerateAlertMessage(metrics, threshold),
                        Metrics = metrics
                    };
                    
                    AddAlert(alert);
                    _logger.LogWarning("Performance threshold violated for {OperationName}: {Message}",
                        metrics.OperationName, alert.Message);
                }
            }
            
            _logger.LogDebug("Recorded metric for {OperationName}: Duration={Duration}ms, Memory={Memory}KB, Success={Success}",
                metrics.OperationName,
                metrics.Duration.TotalMilliseconds,
                metrics.MemoryUsedBytes / 1024,
                metrics.Success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording performance metric for {OperationName}", metrics.OperationName);
        }
    }
    
    public PerformanceStatistics? GetStatistics(string operationName)
    {
        if (!_metrics.TryGetValue(operationName, out var metricsList) || metricsList.Count == 0)
            return null;
        
        lock (metricsList)
        {
            var durations = metricsList.Select(m => m.Duration).OrderBy(d => d).ToList();
            var successCount = metricsList.Count(m => m.Success);
            
            return new PerformanceStatistics
            {
                OperationName = operationName,
                TotalExecutions = metricsList.Count,
                SuccessfulExecutions = successCount,
                FailedExecutions = metricsList.Count - successCount,
                MinDuration = durations.FirstOrDefault(TimeSpan.Zero),
                MaxDuration = durations.LastOrDefault(TimeSpan.Zero),
                AverageDuration = TimeSpan.FromMilliseconds(durations.Average(d => d.TotalMilliseconds)),
                MedianDuration = GetPercentile(durations, 50),
                P95Duration = GetPercentile(durations, 95),
                P99Duration = GetPercentile(durations, 99),
                AverageMemoryUsedBytes = (long)metricsList.Average(m => m.MemoryUsedBytes),
                FirstExecution = metricsList.FirstOrDefault()?.StartTime ?? DateTime.UtcNow,
                LastExecution = metricsList.LastOrDefault()?.StartTime ?? DateTime.UtcNow
            };
        }
    }
    
    public IReadOnlyDictionary<string, PerformanceStatistics> GetAllStatistics()
    {
        var result = new Dictionary<string, PerformanceStatistics>();
        
        foreach (var kvp in _metrics)
        {
            var stats = GetStatistics(kvp.Key);
            if (stats != null)
            {
                result[kvp.Key] = stats;
            }
        }
        
        return result;
    }
    
    public void SetThresholds(string operationName, PerformanceThresholds thresholds)
    {
        _thresholds[operationName] = thresholds;
        _logger.LogInformation("Set performance thresholds for {OperationName}", operationName);
        
        // Check current statistics against new thresholds
        var stats = GetStatistics(operationName);
        if (stats != null && thresholds.IsViolated(stats))
        {
            var alert = new PerformanceAlert
            {
                Timestamp = DateTime.UtcNow,
                OperationName = operationName,
                Severity = AlertSeverity.Warning,
                Message = $"Current statistics violate newly set thresholds",
                Statistics = stats
            };
            
            AddAlert(alert);
        }
    }
    
    public IReadOnlyList<PerformanceAlert> GetRecentAlerts(int count = 10)
    {
        return _alerts
            .OrderByDescending(a => a.Timestamp)
            .Take(count)
            .ToList();
    }
    
    public void ClearMetrics()
    {
        _metrics.Clear();
        _alerts.Clear();
        _logger.LogInformation("Performance metrics cleared");
    }
    
    public async Task<string> ExportMetricsAsync()
    {
        var result = await _logger.ExecuteWithLogging(
            () => Task.FromResult(SerializeMetricsToJson()),
            "Error exporting performance metrics");
            
        if (result.IsSuccess)
        {
            return result.Value;
        }
        
        throw result.Exception;
    }
    
    private string SerializeMetricsToJson()
    {
        var export = new
        {
            ExportTime = DateTime.UtcNow,
            Statistics = GetAllStatistics(),
            RecentAlerts = GetRecentAlerts(50),
            Thresholds = _thresholds.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };
        
        return System.Text.Json.JsonSerializer.Serialize(export, new System.Text.Json.JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
    }
    
    public async Task<BrowserPerformanceMetrics?> GetBrowserMetricsAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<BrowserPerformanceMetrics>("eval", @"
                (function() {
                    const metrics = {};
                    
                    // Navigation timing
                    if (window.performance && window.performance.timing) {
                        const timing = window.performance.timing;
                        metrics.navigationStart = timing.navigationStart;
                        metrics.domContentLoaded = timing.domContentLoadedEventEnd - timing.navigationStart;
                        metrics.loadComplete = timing.loadEventEnd - timing.navigationStart;
                    }
                    
                    // Paint timing
                    if (window.performance && window.performance.getEntriesByType) {
                        const paintEntries = performance.getEntriesByType('paint');
                        const fcp = paintEntries.find(entry => entry.name === 'first-contentful-paint');
                        if (fcp) {
                            metrics.firstContentfulPaint = fcp.startTime;
                        }
                    }
                    
                    // Memory usage (Chrome only)
                    if (window.performance && window.performance.memory) {
                        metrics.usedJSHeapSize = performance.memory.usedJSHeapSize;
                        metrics.totalJSHeapSize = performance.memory.totalJSHeapSize;
                    }
                    
                    // Web Vitals (simplified)
                    metrics.firstInputDelay = 0; // Would need PerformanceObserver
                    metrics.cumulativeLayoutShift = 0; // Would need PerformanceObserver
                    
                    return metrics;
                })();
            ");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting browser performance metrics");
            return null;
        }
    }
    
    private TimeSpan GetPercentile(List<TimeSpan> sortedDurations, int percentile)
    {
        if (sortedDurations.Count == 0)
            return TimeSpan.Zero;
        
        var index = (int)Math.Ceiling(sortedDurations.Count * (percentile / 100.0)) - 1;
        index = Math.Max(0, Math.Min(index, sortedDurations.Count - 1));
        return sortedDurations[index];
    }
    
    private AlertSeverity DetermineSeverity(PerformanceMetrics metrics, PerformanceThresholds threshold)
    {
        if (!metrics.Success)
            return AlertSeverity.Error;
        
        var durationRatio = threshold.MaxDuration.HasValue 
            ? metrics.Duration.TotalMilliseconds / threshold.MaxDuration.Value.TotalMilliseconds 
            : 0;
            
        if (durationRatio > 2) return AlertSeverity.Critical;
        if (durationRatio > 1.5) return AlertSeverity.Error;
        if (durationRatio > 1) return AlertSeverity.Warning;
        
        return AlertSeverity.Info;
    }
    
    private string GenerateAlertMessage(PerformanceMetrics metrics, PerformanceThresholds threshold)
    {
        var messages = new List<string>();
        
        if (!metrics.Success)
        {
            messages.Add($"Operation failed: {metrics.ErrorMessage}");
        }
        
        if (threshold.MaxDuration.HasValue && metrics.Duration > threshold.MaxDuration.Value)
        {
            messages.Add($"Duration {metrics.Duration.TotalMilliseconds:F2}ms exceeds threshold {threshold.MaxDuration.Value.TotalMilliseconds:F2}ms");
        }
        
        if (threshold.MaxMemoryBytes.HasValue && metrics.MemoryUsedBytes > threshold.MaxMemoryBytes.Value)
        {
            messages.Add($"Memory usage {metrics.MemoryUsedBytes / 1024}KB exceeds threshold {threshold.MaxMemoryBytes.Value / 1024}KB");
        }
        
        return string.Join("; ", messages);
    }
    
    private void AddAlert(PerformanceAlert alert)
    {
        _alerts.Add(alert);
        
        // Limit the number of alerts
        while (_alerts.Count > _maxAlerts)
        {
            _alerts.TryTake(out _);
        }
    }
}