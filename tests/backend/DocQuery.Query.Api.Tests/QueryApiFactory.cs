using DocQuery.Application.Abstractions;
using DocQuery.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DocQuery.Query.Api.Tests;

/// <summary>
/// Hosts the Query API in-process with fakes replacing the embedding model, the chat model and the vector search.
/// Connection strings are syntactically valid placeholders so the integrations register, but nothing connects:
/// migrations are off and dependency health checks removed. The query rate limit is raised so unrelated tests
/// never trip it; individual tests lower it via <see cref="WebApplicationFactory{T}.WithWebHostBuilder"/>.
/// </summary>
public sealed class QueryApiFactory : WebApplicationFactory<Program>
{
    private const string PlaceholderDatabaseConnection = "Host=localhost;Database=docquery;Username=postgres;Password=placeholder";

    private const string PlaceholderOpenAIConnection = "Endpoint=https://placeholder.openai.azure.com/;Key=placeholder";

    /// <summary>Settings the host needs at registration time; shared with tests that compose the host themselves.</summary>
    public static IReadOnlyDictionary<string, string?> PlaceholderSettings { get; } = new Dictionary<string, string?>
    {
        ["ConnectionStrings:docquery"] = PlaceholderDatabaseConnection,
        ["ConnectionStrings:openai"] = PlaceholderOpenAIConnection,
        ["Persistence:ApplyMigrationsOnStartup"] = "false",
        ["RateLimiting:Queries:PermitLimit"] = "1000",
    };

    public FakeEmbeddingGenerator Embeddings { get; } = new();

    public FakeChatClient Chat { get; } = new();

    public FakeChunkSearch Search { get; } = new();

    public InMemoryDocumentRepository Documents { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting lands in host configuration before Program.cs runs. ConfigureAppConfiguration would be applied
        // only at Build(), too late for values the integrations read while registering services.
        foreach (var (key, value) in PlaceholderSettings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmbeddingGenerator<string, Embedding<float>>>();
            services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(Embeddings);

            services.RemoveAll<IChatClient>();
            services.AddSingleton<IChatClient>(Chat);

            services.RemoveAll<IChunkSearch>();
            services.AddSingleton<IChunkSearch>(Search);

            services.RemoveAll<IDocumentRepository>();
            services.AddSingleton<IDocumentRepository>(Documents);

            // Keep only the liveness self-check; dependency checks would try to reach the placeholders.
            services.PostConfigure<HealthCheckServiceOptions>(options =>
            {
                var self = options.Registrations.Where(registration => registration.Name == "self").ToList();
                options.Registrations.Clear();
                foreach (var registration in self)
                {
                    options.Registrations.Add(registration);
                }
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Embeddings.Dispose();
            Chat.Dispose();
        }

        base.Dispose(disposing);
    }
}
