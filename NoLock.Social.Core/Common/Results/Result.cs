namespace NoLock.Social.Core.Common.Results;

/// <summary>
/// Represents the result of an operation that can either succeed or fail
/// </summary>
/// <typeparam name="T">The type of the success value</typeparam>
public readonly struct Result<T>
{
    private readonly T? _value;
    private readonly Exception? _exception;
    private readonly bool _isSuccess;

    private Result(T value)
    {
        _value = value;
        _exception = null;
        _isSuccess = true;
    }

    private Result(Exception exception)
    {
        _value = default;
        _exception = exception;
        _isSuccess = false;
    }

    public bool IsSuccess => _isSuccess;
    public bool IsFailure => !_isSuccess;

    public T Value => _isSuccess 
        ? _value! 
        : throw new InvalidOperationException("Cannot access value of failed result");

    public Exception Exception => !_isSuccess 
        ? _exception! 
        : throw new InvalidOperationException("Cannot access exception of successful result");

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Exception exception) => new(exception);

    /// <summary>
    /// Executes action if result is successful
    /// </summary>
    public Result<T> OnSuccess(Action<T> action)
    {
        if (_isSuccess)
            action(_value!);
        return this;
    }

    /// <summary>
    /// Executes action if result is failure
    /// </summary>
    public Result<T> OnFailure(Action<Exception> action)
    {
        if (!_isSuccess)
            action(_exception!);
        return this;
    }

    /// <summary>
    /// Maps success value to new type
    /// </summary>
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        return _isSuccess 
            ? Result<TNew>.Success(mapper(_value!))
            : Result<TNew>.Failure(_exception!);
    }

    /// <summary>
    /// Matches success or failure and returns a value
    /// </summary>
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<Exception, TResult> onFailure)
    {
        return _isSuccess ? onSuccess(_value!) : onFailure(_exception!);
    }
}

/// <summary>
/// Non-generic Result for operations that don't return a value
/// </summary>
public readonly struct Result
{
    private readonly Exception? _exception;
    private readonly bool _isSuccess;

    private Result(bool isSuccess, Exception? exception = null)
    {
        _isSuccess = isSuccess;
        _exception = exception;
    }

    public bool IsSuccess => _isSuccess;
    public bool IsFailure => !_isSuccess;

    public Exception Exception => !_isSuccess 
        ? _exception! 
        : throw new InvalidOperationException("Cannot access exception of successful result");

    public static Result Success() => new(true);
    public static Result Failure(Exception exception) => new(false, exception);

    /// <summary>
    /// Executes action if result is successful
    /// </summary>
    public Result OnSuccess(Action action)
    {
        if (_isSuccess)
            action();
        return this;
    }

    /// <summary>
    /// Executes action if result is failure
    /// </summary>
    public Result OnFailure(Action<Exception> action)
    {
        if (!_isSuccess)
            action(_exception!);
        return this;
    }

    /// <summary>
    /// Matches success or failure and returns a value
    /// </summary>
    public TResult Match<TResult>(Func<TResult> onSuccess, Func<Exception, TResult> onFailure)
    {
        return _isSuccess ? onSuccess() : onFailure(_exception!);
    }
}