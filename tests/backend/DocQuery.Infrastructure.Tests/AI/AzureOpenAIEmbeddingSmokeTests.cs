using DocQuery.Domain.Documents;
using DocQuery.Infrastructure.AI;
using DocQuery.Tests.Common;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DocQuery.Infrastructure.Tests.AI;

/// <summary>
/// Opt-in check against a real Azure OpenAI deployment. Set DOCQUERY_OPENAI_CONNECTION to
/// "Endpoint=https://…;Key=…" (and optionally DOCQUERY_OPENAI_EMBEDDING_DEPLOYMENT) to run it.
/// </summary>
public sealed class AzureOpenAIEmbeddingSmokeTests
{
    private const string ConnectionVariable = "DOCQUERY_OPENAI_CONNECTION";

    [EnvironmentFact(ConnectionVariable)]
    public async Task Deployment_returns_vectors_of_the_expected_dimensions()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:openai"] = Environment.GetEnvironmentVariable(ConnectionVariable),
            ["AzureOpenAI:EmbeddingDeployment"] = Environment.GetEnvironmentVariable("DOCQUERY_OPENAI_EMBEDDING_DEPLOYMENT") ?? "text-embedding-3-small",
        });
        builder.AddAzureOpenAIEmbeddings();
        using var host = builder.Build();
        var generator = host.Services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

        var result = await generator.GenerateAsync(["The quick brown fox.", "Jumps over the lazy dog."]);

        Assert.Equal(2, result.Count);
        Assert.All(result, embedding => Assert.Equal(DocumentChunk.EmbeddingDimensions, embedding.Vector.Length));
    }
}
