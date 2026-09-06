using DocQuery.Application.Abstractions;
using DocQuery.Domain.Common;
using DocQuery.Domain.Documents;
using DocQuery.Domain.Querying;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace DocQuery.Application.Documents.Querying;

/// <summary>
/// Retrieval-augmented answering: embed the question with the same model as the chunks, fetch the nearest chunks
/// by cosine distance, and ask the chat model to answer from those passages only. With no embedded chunk to
/// ground an answer the chat model is not called at all (ADR 0019).
/// </summary>
internal sealed class AskQuestionHandler(
    IEmbeddingGenerator<string, Embedding<float>> embeddings,
    IChunkSearch search,
    IChatClient chat,
    IOptions<QueryOptions> options) : IQueryHandler<AskQuestionQuery, AskQuestionResult>
{
    public async Task<AskQuestionResult> HandleAsync(AskQuestionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var settings = options.Value;
        var question = Question.Create(query.Question);
        var topK = ResolveTopK(query.TopK, settings);

        var queryEmbedding = await EmbedAsync(question, cancellationToken);
        var passages = await search.SearchAsync(queryEmbedding, query.DocumentId, topK, cancellationToken);
        if (passages.Count == 0)
        {
            return new AskQuestionResult(AskQuestionResult.NoPassagesAnswer, []);
        }

        var response = await chat.GetResponseAsync(
            RagPrompt.Build(question.Text, passages),
            new ChatOptions { MaxOutputTokens = settings.MaxAnswerTokens },
            cancellationToken);

        var citations = passages
            .Select((passage, index) => new Citation(
                index + 1,
                passage.DocumentId,
                passage.FileName,
                passage.PageNumber,
                passage.Position,
                Excerpt(passage.Text, settings.ExcerptLength),
                1 - passage.Distance))
            .ToList();

        return new AskQuestionResult(response.Text.Trim(), citations);
    }

    private static int ResolveTopK(int? requested, QueryOptions settings)
    {
        if (requested is null)
        {
            return settings.DefaultTopK;
        }

        if (requested < 1 || requested > settings.MaxTopK)
        {
            throw new DomainException(FormattableString.Invariant($"TopK must be between 1 and {settings.MaxTopK}."));
        }

        return requested.Value;
    }

    private async Task<ReadOnlyMemory<float>> EmbedAsync(Question question, CancellationToken cancellationToken)
    {
        var generated = await embeddings.GenerateAsync([question.Text], cancellationToken: cancellationToken);
        var vector = generated.Single().Vector;

        if (vector.Length != DocumentChunk.EmbeddingDimensions)
        {
            throw new InvalidOperationException(FormattableString.Invariant(
                $"The embedding model returned {vector.Length} dimensions; chunks are stored with {DocumentChunk.EmbeddingDimensions}."));
        }

        return vector;
    }

    private static string Excerpt(string text, int length) =>
        text.Length <= length ? text : string.Concat(text.AsSpan(0, length).TrimEnd(), "…");
}
