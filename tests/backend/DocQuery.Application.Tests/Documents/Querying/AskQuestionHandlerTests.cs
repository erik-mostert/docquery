using DocQuery.Application.Abstractions;
using DocQuery.Application.Documents.Querying;
using DocQuery.Domain.Common;
using DocQuery.Domain.Documents;
using DocQuery.Tests.Common;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DocQuery.Application.Tests.Documents.Querying;

public sealed class AskQuestionHandlerTests : IDisposable
{
    private readonly FakeEmbeddingGenerator _embeddings = new();
    private readonly FakeChunkSearch _search = new();
    private readonly FakeChatClient _chat = new();

    [Fact]
    public async Task Embeds_the_question_searches_and_answers_with_numbered_citations()
    {
        var documentId = DocumentId.Create();
        _search.Matches.Add(new ChunkMatch(documentId, "handbook.pdf", 3, 7, "Refunds are issued within 14 days.", 0.10));
        _search.Matches.Add(new ChunkMatch(documentId, "handbook.pdf", 4, 8, "Refunds go to the original payment method.", 0.25));
        _chat.Answer = "Within 14 days [1], to the original payment method [2].";
        var handler = BuildHandler();

        var result = await handler.HandleAsync(new AskQuestionQuery("How long do refunds take?"), CancellationToken.None);

        var embedded = Assert.Single(_embeddings.Batches);
        Assert.Equal(["How long do refunds take?"], embedded);
        var call = Assert.Single(_search.Calls);
        Assert.Equal(_embeddings.Vector("How long do refunds take?"), call.Embedding.ToArray());
        Assert.Null(call.DocumentId);
        Assert.Equal(5, call.Limit);

        Assert.Equal(_chat.Answer, result.Answer);
        Assert.Equal([1, 2], result.Citations.Select(citation => citation.Number));
        Assert.Equal([0.90, 0.75], result.Citations.Select(citation => Math.Round(citation.Score, 2)));
        Assert.Equal([3, 4], result.Citations.Select(citation => citation.PageNumber));
        Assert.All(result.Citations, citation => Assert.Equal(documentId, citation.DocumentId));
    }

    [Fact]
    public async Task Prompt_contains_the_passages_and_the_question_and_caps_the_answer_length()
    {
        _search.Matches.Add(Match("Refunds are issued within 14 days."));
        var handler = BuildHandler(maxAnswerTokens: 123);

        await handler.HandleAsync(new AskQuestionQuery("  How long do refunds take? "), CancellationToken.None);

        var request = Assert.Single(_chat.Requests);
        Assert.Equal(ChatRole.System, request[0].Role);
        Assert.Contains("Refunds are issued within 14 days.", request[1].Text, StringComparison.Ordinal);
        Assert.EndsWith("Question: How long do refunds take?", request[1].Text, StringComparison.Ordinal);
        Assert.Equal(123, Assert.Single(_chat.Options)!.MaxOutputTokens);
    }

    [Fact]
    public async Task Scopes_the_search_to_the_requested_document_and_top_k()
    {
        var documentId = DocumentId.Create();
        _search.Matches.Add(Match("passage", documentId));
        var handler = BuildHandler();

        await handler.HandleAsync(new AskQuestionQuery("question", documentId, TopK: 3), CancellationToken.None);

        var call = Assert.Single(_search.Calls);
        Assert.Equal(documentId, call.DocumentId);
        Assert.Equal(3, call.Limit);
    }

    [Fact]
    public async Task Without_matches_the_chat_model_is_not_called()
    {
        var handler = BuildHandler();

        var result = await handler.HandleAsync(new AskQuestionQuery("question"), CancellationToken.None);

        Assert.Empty(_chat.Requests);
        Assert.Empty(result.Citations);
        Assert.Equal(AskQuestionResult.NoPassagesAnswer, result.Answer);
    }

    [Fact]
    public async Task Excerpts_are_cut_to_the_configured_length()
    {
        _search.Matches.Add(Match(new string('x', 400)));
        var handler = BuildHandler(excerptLength: 100);

        var result = await handler.HandleAsync(new AskQuestionQuery("question"), CancellationToken.None);

        var excerpt = Assert.Single(result.Citations).Excerpt;
        Assert.Equal(101, excerpt.Length);
        Assert.EndsWith("…", excerpt, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public async Task Top_k_outside_the_allowed_range_is_rejected_before_any_work(int topK)
    {
        var handler = BuildHandler();

        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(new AskQuestionQuery("question", TopK: topK), CancellationToken.None));

        Assert.Empty(_embeddings.Batches);
    }

    [Fact]
    public async Task Empty_question_is_rejected_before_any_work()
    {
        var handler = BuildHandler();

        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(new AskQuestionQuery("   "), CancellationToken.None));

        Assert.Empty(_embeddings.Batches);
    }

    [Fact]
    public async Task Wrong_embedding_size_is_a_configuration_error()
    {
        using var generator = new FakeEmbeddingGenerator(dimensions: 3);
        var handler = BuildHandler(generator);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(new AskQuestionQuery("question"), CancellationToken.None));

        Assert.Empty(_search.Calls);
    }

    [Fact]
    public async Task Chat_failures_propagate()
    {
        _search.Matches.Add(Match("passage"));
        _chat.FailNextCallWith(new TimeoutException("slow"));
        var handler = BuildHandler();

        await Assert.ThrowsAsync<TimeoutException>(() => handler.HandleAsync(new AskQuestionQuery("question"), CancellationToken.None));
    }

    public void Dispose()
    {
        _embeddings.Dispose();
        _chat.Dispose();
    }

    private static ChunkMatch Match(string text, DocumentId? documentId = null) =>
        new(documentId ?? DocumentId.Create(), "doc.pdf", 1, 0, text, 0.2);

    private IQueryHandler<AskQuestionQuery, AskQuestionResult> BuildHandler(int excerptLength = 300, int maxAnswerTokens = 800) =>
        BuildHandler(_embeddings, excerptLength, maxAnswerTokens);

    private IQueryHandler<AskQuestionQuery, AskQuestionResult> BuildHandler(FakeEmbeddingGenerator embeddings, int excerptLength = 300, int maxAnswerTokens = 800)
    {
        var services = new ServiceCollection();
        services.AddQuerying();
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(embeddings);
        services.AddSingleton<IChunkSearch>(_search);
        services.AddSingleton<IChatClient>(_chat);
        services.AddSingleton(Options.Create(new QueryOptions
        {
            DefaultTopK = 5,
            MaxTopK = 20,
            ExcerptLength = excerptLength,
            MaxAnswerTokens = maxAnswerTokens,
        }));

        return services.BuildServiceProvider().GetRequiredService<IQueryHandler<AskQuestionQuery, AskQuestionResult>>();
    }
}
