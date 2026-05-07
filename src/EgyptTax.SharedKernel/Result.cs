namespace EgyptTax.SharedKernel;

public readonly record struct Error(string Code, string Message);

// CA1000: Static factories on generic types are intentional here — the
// discriminated-union pattern (Result<T>.Success / .Failure) is the standard
// shape for this primitive across .NET ecosystems (FluentResults, ErrorOr,
// OneOf). Implicit conversions from T and Error reduce the need to call them
// directly in most code, but the explicit form remains useful for clarity.
#pragma warning disable CA1000
public readonly struct Result<T>
{
    private readonly T? _value;
    private readonly Error _error;

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot read Value on a failed Result.");

    public Error Error => IsFailure
        ? _error
        : throw new InvalidOperationException("Cannot read Error on a successful Result.");

    private Result(T value)
    {
        _value = value;
        _error = default;
        IsSuccess = true;
    }

    private Result(Error error)
    {
        _value = default;
        _error = error;
        IsSuccess = false;
    }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);
}
#pragma warning restore CA1000
