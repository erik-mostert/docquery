using System.ClientModel;
using System.Net;
using System.Net.Http.Json;
using DocQuery.Query.Api.Endpoints.Queries;
using Microsoft.AspNetCore.Mvc;

namespace DocQuery.Query.Api.Tests.Queries;

public sealed class AiServiceFailureTests(QueryApiFactory factory) : IClassFixture<QueryApiFactory>
{
    private static readonly Uri QueriesUri = new("/queries", UriKind.Relative);

    [Fact]
    public async Task Azure_openai_failure_is_a_bad_gateway_with_problem_details_and_retry_after()
    {
        factory.Embeddings.FailNextCallWith(new ClientResultException("HTTP 404 (DeploymentNotFound)"));
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(QueriesUri, new AskQuestionRequest("question"));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.Contains("Retry-After"));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("AI service error", problem.Title);
        Assert.DoesNotContain("DeploymentNotFound", problem.Detail, StringComparison.Ordinal);
    }
}
