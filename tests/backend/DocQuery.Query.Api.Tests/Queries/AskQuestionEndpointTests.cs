using System.Net;
using System.Net.Http.Json;
using DocQuery.Application.Documents.Querying;
using DocQuery.Domain.Documents;
using DocQuery.Query.Api.Endpoints.Queries;
using Microsoft.AspNetCore.Mvc;

namespace DocQuery.Query.Api.Tests.Queries;

public sealed class AskQuestionEndpointTests(QueryApiFactory factory) : IClassFixture<QueryApiFactory>
{
    private static readonly Uri QueriesUri = new("/queries", UriKind.Relative);

    [Fact]
    public async Task Post_question_returns_answer_with_citations()
    {
        var documentId = DocumentId.Create();
        factory.Search.Matches.Clear();
        factory.Search.Matches.Add(new ChunkMatch(documentId, "handbook.pdf", 3, 7, "Refunds are issued within 14 days.", 0.1));
        factory.Chat.Answer = "Within 14 days [1].";
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(QueriesUri, new AskQuestionRequest("How long do refunds take?"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AskQuestionResponse>();
        Assert.NotNull(body);
        Assert.Equal("Within 14 days [1].", body.Answer);
        var citation = Assert.Single(body.Citations);
        Assert.Equal(1, citation.Number);
        Assert.Equal(documentId.Value, citation.DocumentId);
        Assert.Equal("handbook.pdf", citation.FileName);
        Assert.Equal(3, citation.PageNumber);
        Assert.Equal(7, citation.Position);
        Assert.Equal("Refunds are issued within 14 days.", citation.Excerpt);
        Assert.Equal(0.9, citation.Score, precision: 6);
    }

    [Fact]
    public async Task Document_id_and_top_k_reach_the_search()
    {
        var documentId = DocumentId.Create();
        factory.Search.Calls.Clear();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(QueriesUri, new AskQuestionRequest("question", documentId.Value, TopK: 3));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var call = Assert.Single(factory.Search.Calls);
        Assert.Equal(documentId, call.DocumentId);
        Assert.Equal(3, call.Limit);
    }

    [Fact]
    public async Task Without_matches_the_answer_says_so_and_has_no_citations()
    {
        factory.Search.Matches.Clear();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(QueriesUri, new AskQuestionRequest("question"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AskQuestionResponse>();
        Assert.NotNull(body);
        Assert.Equal(AskQuestionResult.NoPassagesAnswer, body.Answer);
        Assert.Empty(body.Citations);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Empty_question_is_a_bad_request_with_problem_details(string? question)
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(QueriesUri, new AskQuestionRequest(question));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("empty", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Top_k_beyond_the_maximum_is_a_bad_request()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(QueriesUri, new AskQuestionRequest("question", TopK: 999));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("TopK", problem.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_body_is_a_bad_request()
    {
        using var client = factory.CreateClient();
        using var content = new StringContent("null", System.Text.Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(QueriesUri, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
