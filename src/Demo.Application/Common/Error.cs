namespace Demo.Application.Common;

public enum ErrorType
{
    Validation,
    NotFound,
    Unavailable
}

/// <summary>An expected business failure. <see cref="Code"/> is stable and machine-readable; <see cref="Message"/> is for humans.</summary>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Unavailable(string code, string message) => new(code, message, ErrorType.Unavailable);
}
