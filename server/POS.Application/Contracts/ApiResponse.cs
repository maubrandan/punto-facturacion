using POS.Application.Common;

namespace POS.Application.Contracts;

/// <summary>
/// Envelope HTTP uniforme: mismo contrato en éxito y en error.
/// </summary>
public sealed class ApiResponse<T>
{
    public bool Success { get; init; }

    public T? Data { get; init; }

    public ApiErrorBody? Error { get; init; }

    public static ApiResponse<T> FromResult(Result<T> result) =>
        result.IsSuccess
            ? new ApiResponse<T> { Success = true, Data = result.Value }
            : new ApiResponse<T>
            {
                Success = false,
                Error = new ApiErrorBody
                {
                    Code = result.ErrorCode ?? "error",
                    Message = result.Error ?? string.Empty
                }
            };
}

public sealed class ApiErrorBody
{
    public required string Code { get; init; }

    public required string Message { get; init; }
}
