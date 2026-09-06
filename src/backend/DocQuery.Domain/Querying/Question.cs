using DocQuery.Domain.Common;

namespace DocQuery.Domain.Querying;

/// <summary>A natural-language question asked over the document corpus: non-empty, trimmed, bounded in length.</summary>
public sealed record Question
{
    /// <summary>Upper bound on question length; long inputs cost embedding tokens and rarely improve retrieval.</summary>
    public const int MaxLength = 2000;

    private Question(string text)
    {
        Text = text;
    }

    public string Text { get; }

    public static Question Create(string? text)
    {
        var trimmed = text?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            throw new DomainException("A question must not be empty.");
        }

        if (trimmed.Length > MaxLength)
        {
            throw new DomainException(FormattableString.Invariant(
                $"A question must be at most {MaxLength} characters; got {trimmed.Length}."));
        }

        return new Question(trimmed);
    }
}
