namespace FinAI.Api.Common;

/// <summary>
/// Resultado de uma operação de serviço (padrão Result).
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public ErrorCode Error { get; }
    public string? Message { get; }

    protected Result(bool isSuccess, ErrorCode error, string? message)
    {
        IsSuccess = isSuccess;
        Error = error;
        Message = message;
    }

    public static Result Success() => new(true, ErrorCode.None, null);

    public static Result Failure(ErrorCode error, string? message = null) => new(false, error, message);

    public static Result<T> Success<T>(T value) => new(value, true, ErrorCode.None, null);

    public static Result<T> Failure<T>(ErrorCode error, string? message = null) => new(default, false, error, message);
}

/// <summary>
/// Resultado de uma operação de serviço com valor (padrão Result&lt;T&gt;).
/// </summary>
public class Result<T> : Result
{
    public T? Value { get; }

    internal Result(T? value, bool isSuccess, ErrorCode error, string? message)
        : base(isSuccess, error, message)
    {
        Value = value;
    }
}
