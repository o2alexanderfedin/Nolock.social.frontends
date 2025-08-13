using System.Diagnostics;

namespace NoLock.Social.Core.Performance;

/// <summary>
/// Represents performance metrics for an operation
/// </summary>
public class PerformanceMetrics
{
    private TimeSpan? _duration;
    
    public string OperationName { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    
    public TimeSpan Duration 
    { 
        get => _duration ?? (EndTime - StartTime);
        set => _duration = value;
    }
    
    public long MemoryUsedBytes { get; set; }
    public Dictionary<string, object> CustomMetrics { get; set; } = new();
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Aggregated performance statistics
/// </summary>
public class PerformanceStatistics
{
    public string OperationName { get; set; } = "";
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public double SuccessRate => TotalExecutions > 0 ? (double)SuccessfulExecutions / TotalExecutions * 100 : 0;
    public TimeSpan MinDuration { get; set; }
    public TimeSpan MaxDuration { get; set; }
    public TimeSpan AverageDuration { get; set; }
    public TimeSpan MedianDuration { get; set; }
    public TimeSpan P95Duration { get; set; } // 95th percentile
    public TimeSpan P99Duration { get; set; } // 99th percentile
    public long AverageMemoryUsedBytes { get; set; }
    public DateTime FirstExecution { get; set; }
    public DateTime LastExecution { get; set; }
}

/// <summary>
/// Performance threshold configuration
/// </summary>
public class PerformanceThresholds
{
    public TimeSpan? MaxDuration { get; set; }
    public long? MaxMemoryBytes { get; set; }
    public double? MinSuccessRate { get; set; }
    
    public bool IsViolated(PerformanceMetrics metrics)
    {
        if (MaxDuration.HasValue && metrics.Duration > MaxDuration.Value)
            return true;
            
        if (MaxMemoryBytes.HasValue && metrics.MemoryUsedBytes > MaxMemoryBytes.Value)
            return true;
            
        return false;
    }
    
    public bool IsViolated(PerformanceStatistics stats)
    {
        if (MaxDuration.HasValue && stats.AverageDuration > MaxDuration.Value)
            return true;
            
        if (MaxMemoryBytes.HasValue && stats.AverageMemoryUsedBytes > MaxMemoryBytes.Value)
            return true;
            
        if (MinSuccessRate.HasValue && stats.SuccessRate < MinSuccessRate.Value)
            return true;
            
        return false;
    }
}

/// <summary>
/// Performance alert raised when thresholds are violated
/// </summary>
public class PerformanceAlert
{
    public DateTime Timestamp { get; set; }
    public string OperationName { get; set; } = "";
    public AlertSeverity Severity { get; set; }
    public string Message { get; set; } = "";
    public PerformanceMetrics? Metrics { get; set; }
    public PerformanceStatistics? Statistics { get; set; }
}

public enum AlertSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Disposable performance timer for measuring operation duration
/// </summary>
public class PerformanceTimer : IDisposable
{
    private readonly string _operationName;
    private readonly Action<PerformanceMetrics> _onComplete;
    private readonly Stopwatch _stopwatch;
    private readonly DateTime _startTime;
    private readonly long _startMemory;
    private bool _disposed;
    private bool _success = true;
    private string? _errorMessage;
    private readonly Dictionary<string, object> _customMetrics = new();
    
    public PerformanceTimer(string operationName, Action<PerformanceMetrics> onComplete)
    {
        _operationName = operationName;
        _onComplete = onComplete;
        _startTime = DateTime.UtcNow;
        _stopwatch = Stopwatch.StartNew();
        _startMemory = GC.GetTotalMemory(false);
    }
    
    public void AddMetric(string key, object value)
    {
        _customMetrics[key] = value;
    }
    
    public void MarkAsFailure(string errorMessage)
    {
        _success = false;
        _errorMessage = errorMessage;
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _stopwatch.Stop();
        var endMemory = GC.GetTotalMemory(false);
        
        var metrics = new PerformanceMetrics
        {
            OperationName = _operationName,
            StartTime = _startTime,
            EndTime = DateTime.UtcNow,
            MemoryUsedBytes = Math.Max(0, endMemory - _startMemory),
            Success = _success,
            ErrorMessage = _errorMessage,
            CustomMetrics = _customMetrics
        };
        
        _onComplete(metrics);
        _disposed = true;
    }
}