using DocQuery.Application.Abstractions;

namespace DocQuery.Tests.Common;

/// <summary>One token per whitespace-separated word, with exact character offsets. Makes chunk sizes easy to reason about.</summary>
public sealed class FakeTokenizer : ITokenizer
{
    public int CountTokens(string text) => EncodeToTokens(text).Count;

    public IReadOnlyList<TokenSpan> EncodeToTokens(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var spans = new List<TokenSpan>();
        var index = 0;
        while (index < text.Length)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            if (index >= text.Length)
            {
                break;
            }

            var start = index;
            while (index < text.Length && !char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            spans.Add(new TokenSpan(spans.Count, start, index));
        }

        return spans;
    }
}
