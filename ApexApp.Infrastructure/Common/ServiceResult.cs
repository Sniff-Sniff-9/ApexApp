namespace ApexApp.Infrastructure.Common;

/// <summary>Result of an operation that does not return a value.</summary>
public sealed class ServiceResult
{
    private ServiceResult(int statusCode, string? error)
    {
        StatusCode = statusCode;
        Error = error;
    }

    public int StatusCode { get; }

    public string? Error { get; }

    public bool IsSuccess => StatusCode is >= 200 and < 300;

    public static ServiceResult Ok(int statusCode = 200) => new(statusCode, null);

    public static ServiceResult Fail(int statusCode, string error) => new(statusCode, error);
}

public sealed class ServiceResult<T>
{
    private ServiceResult(T? value, int statusCode, string? error)
    {
        Value = value;
        StatusCode = statusCode;
        Error = error;
    }

    public T? Value { get; }

    public int StatusCode { get; }

    public string? Error { get; }

    public bool IsSuccess => StatusCode is >= 200 and < 300;

    public static ServiceResult<T> Ok(T value) => new(value, 200, null);

    public static ServiceResult<T> Fail(int statusCode, string error) => new(default, statusCode, error);
}
