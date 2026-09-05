namespace DocQuery.Application.Abstractions;

/// <summary>Counts and locates tokens the way the embedding model will, so chunk sizes are measured in the right unit.</summary>
public interface ITokenizer
{
    int CountTokens(string text);

    /// <summary>Tokens with their character offsets into <paramref name="text"/>, in order.</summary>
    IReadOnlyList<TokenSpan> EncodeToTokens(string text);
}

/// <summary>A token and the half-open character range [<paramref name="Start"/>, <paramref name="End"/>) it covers.</summary>
public readonly record struct TokenSpan(int Id, int Start, int End);
