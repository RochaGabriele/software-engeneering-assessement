namespace Demo.Domain.Common;

/// <summary>Raised when a domain invariant would be violated.</summary>
public sealed class DomainException(string message) : Exception(message);
