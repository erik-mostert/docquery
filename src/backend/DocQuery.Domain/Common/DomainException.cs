namespace DocQuery.Domain.Common;

/// <summary>Thrown when an operation would violate a domain invariant. Maps to a 400 at the API boundary.</summary>
public sealed class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
