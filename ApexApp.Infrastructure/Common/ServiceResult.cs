namespace ApexApp.Infrastructure.Common;

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
