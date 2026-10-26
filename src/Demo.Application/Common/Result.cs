namespace Demo.Application.Common;

/// <summary>Either a value or an expected <see cref="Common.Error"/>. Exceptions stay reserved for the unexpected (ADR-0006).</summary>
public sealed class Result<T>
{
    private readonly T? _value;

    public bool IsSuccess { get; }

    public Error? Error { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"A failed result has no value (error '{Error!.Code}').");

    private Result(T value)
    {
        _value = value;
        IsSuccess = true;
    }

    private Result(Error error)
    {
        Error = error;
    }

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new Result<T>(error);
    }

    public static implicit operator Result<T>(Error error) => Failure(error);
}
