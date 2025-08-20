using Microsoft.Extensions.Logging;

namespace NoLock.Social.Core.Common.Utilities;

/// <summary>
/// Centralized exception handling utility to eliminate try-catch-log duplication
/// </summary>
public static class ExceptionHandler
{
    /// <summary>
    /// Executes an async operation with standardized exception handling and logging
    /// </summary>
    public static async Task<T> ExecuteAsync<T>(
        ILogger logger,
        Func<Task<T>> operation,
        string operationName,
        bool rethrow = true)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{OperationName} failed", operationName);
            if (rethrow)
                throw;
            return default!;
        }
    }

    /// <summary>
    /// Executes a sync operation with standardized exception handling and logging
    /// </summary>
    public static T Execute<T>(
        ILogger logger,
        Func<T> operation,
        string operationName,
        bool rethrow = true)
    {
        try
        {
            return operation();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{OperationName} failed", operationName);
            if (rethrow)
                throw;
            return default!;
        }
    }

    /// <summary>
    /// Executes an async void operation with standardized exception handling and logging
    /// </summary>
    public static async Task ExecuteAsync(
        ILogger logger,
        Func<Task> operation,
        string operationName,
        bool rethrow = true)
    {
        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{OperationName} failed", operationName);
            if (rethrow)
                throw;
        }
    }

    /// <summary>
    /// Executes a void operation with standardized exception handling and logging
    /// </summary>
    public static void Execute(
        ILogger logger,
        Action operation,
        string operationName,
        bool rethrow = true)
    {
        try
        {
            operation();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{OperationName} failed", operationName);
            if (rethrow)
                throw;
        }
    }
}