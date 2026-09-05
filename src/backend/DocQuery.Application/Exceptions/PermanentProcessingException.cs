namespace DocQuery.Application.Exceptions;

/// <summary>
/// A message cannot be processed and retrying will not help (missing data, corrupt input, violated rule). Consumers
/// dead-letter the message instead of redelivering it. The message text is recorded as the document's failure reason.
/// </summary>
public sealed class PermanentProcessingException : Exception
{
    public PermanentProcessingException(string message)
        : base(message)
    {
    }

    public PermanentProcessingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
