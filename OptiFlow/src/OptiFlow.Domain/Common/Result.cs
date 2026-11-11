namespace OptiFlow.Domain.Common;

public static class ErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string PermissionDenied = "PERMISSION_DENIED";
    public const string TransientFailure = "TRANSIENT_FAILURE";
    public const string Duplicate = "DUPLICATE";
    public const string NotFound = "NOT_FOUND";
    public const string NotApproved = "NOT_APPROVED";
    public const string StaleConflict = "STALE_CONFLICT";
    public const string InvalidState = "INVALID_STATE";
    public const string General = "GENERAL";
}

public sealed record Result<T>
{
    public T? Value { get; }
    public string? Error { get; }
    public string? ErrorCode { get; }
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    private Result(T value)
    {
        Value = value;
        IsSuccess = true;
    }

    private Result(string error, string errorCode)
    {
        Error = error;
        ErrorCode = errorCode;
        IsSuccess = false;
    }

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(string error, string errorCode = ErrorCodes.General) =>
        new(error, errorCode);

    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<string, string, TResult> onFailure) =>
        IsSuccess
            ? onSuccess(Value!)
            : onFailure(Error!, ErrorCode!);
}

public sealed record Result
{
    public string? Error { get; }
    public string? ErrorCode { get; }
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    private Result()
    {
        IsSuccess = true;
    }

    private Result(string error, string errorCode)
    {
        Error = error;
        ErrorCode = errorCode;
        IsSuccess = false;
    }

    public static Result Success() => new();

    public static Result Failure(string error, string errorCode = ErrorCodes.General) =>
        new(error, errorCode);

    public TResult Match<TResult>(
        Func<TResult> onSuccess,
        Func<string, string, TResult> onFailure) =>
        IsSuccess
            ? onSuccess()
            : onFailure(Error!, ErrorCode!);
}
