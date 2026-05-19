namespace POS.Application.Common;

/// <summary>
/// Resultado tipado de casos de uso (éxito o error con código y mensaje).
/// </summary>
public readonly record struct Result<T>
{
    public bool IsSuccess { get; init; }

    public T? Value { get; init; }

    public string? Error { get; init; }

    public string? ErrorCode { get; init; }

    public static Result<T> Ok(T value) => new() { IsSuccess = true, Value = value };

    public static Result<T> Failure(string errorCode, string message) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        Error = message
    };
}
