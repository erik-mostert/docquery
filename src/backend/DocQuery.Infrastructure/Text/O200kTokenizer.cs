using DocQuery.Application.Abstractions;
using Microsoft.ML.Tokenizers;

namespace DocQuery.Infrastructure.Text;

/// <summary>
/// The o200k_base tokenizer used by current Azure OpenAI embedding and chat models, so chunk sizes are measured in
/// the model's own tokens. Loading the vocabulary takes about a second; register as a singleton.
/// </summary>
internal sealed class O200kTokenizer : ITokenizer
{
    private const string EncodingName = "o200k_base";

    private readonly TiktokenTokenizer _tokenizer = TiktokenTokenizer.CreateForEncoding(EncodingName);

    public int CountTokens(string text) => _tokenizer.CountTokens(text);

    public IReadOnlyList<TokenSpan> EncodeToTokens(string text)
    {
        var tokens = _tokenizer.EncodeToTokens(text, out _);

        return tokens
            .Select(token => new TokenSpan(token.Id, token.Offset.Start.Value, token.Offset.End.Value))
            .ToArray();
    }
}
