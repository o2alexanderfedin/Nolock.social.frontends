using Microsoft.Extensions.Logging;
using NoLock.Social.Core.Common.Results;

namespace NoLock.Social.Core.Common.Extensions;

/// <summary>
/// Extension methods for Result types to eliminate try-catch-log patterns
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Executes an operation and wraps it in a Result, with automatic logging
    /// </summary>
    public static async Task<Result<T>> ExecuteWithLogging<T>(
        this ILogger logger,
        Func<Task<T>> operation,
        string operationName)
    {
        try
        {
            var result = await operation();
            return Result<T>.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{OperationName} failed", operationName);
            return Result<T>.Failure(ex);
        }
    }

    /// <summary>
    /// Executes an operation and wraps it in a Result, with automatic logging
    /// </summary>
    public static Result<T> ExecuteWithLogging<T>(
        this ILogger logger,
        Func<T> operation,
        string operationName)
    {
        try
        {
            var result = operation();
            return Result<T>.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{OperationName} failed", operationName);
            return Result<T>.Failure(ex);
        }
    }

    /// <summary>
    /// Executes an operation and wraps it in a Result, with automatic logging
    /// </summary>
    public static async Task<Result> ExecuteWithLogging(
        this ILogger logger,
        Func<Task> operation,
        string operationName)
    {
        try
        {
            await operation();
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{OperationName} failed", operationName);
            return Result.Failure(ex);
        }
    }

    /// <summary>
    /// Executes an operation and wraps it in a Result, with automatic logging
    /// </summary>
    public static Result ExecuteWithLogging(
        this ILogger logger,
        Action operation,
        string operationName)
    {
        try
        {
            operation();
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{OperationName} failed", operationName);
            return Result.Failure(ex);
        }
    }

    /// <summary>
    /// Logs and throws exception if Result is failure
    /// </summary>
    public static T ThrowIfFailure<T>(this Result<T> result)
    {
        if (result.IsFailure)
            throw result.Exception;
        return result.Value;
    }

    /// <summary>
    /// Logs and throws exception if Result is failure
    /// </summary>
    public static void ThrowIfFailure(this Result result)
    {
        if (result.IsFailure)
            throw result.Exception;
    }
}