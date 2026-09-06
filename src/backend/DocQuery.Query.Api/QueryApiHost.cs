using DocQuery.Api.Common.Cors;
using DocQuery.Api.Common.Endpoints;
using DocQuery.Api.Common.ExceptionHandling;
using DocQuery.Api.Common.RateLimiting;
using DocQuery.Application;
using DocQuery.Application.Documents.Querying;
using DocQuery.Infrastructure.AI;
using DocQuery.Infrastructure.Configuration;
using DocQuery.Infrastructure.Persistence;
using DocQuery.Query.Api.ExceptionHandling;
using DocQuery.Query.Api.RateLimiting;

namespace DocQuery.Query.Api;

/// <summary>Composition of the Query API, kept out of Program.cs so tests can build the same host without starting it.</summary>
public static class QueryApiHost
{
    public static WebApplicationBuilder ConfigureServices(WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Supplies ConnectionStrings:openai from the vault when a vault URI is configured; otherwise a direct
        // ConnectionStrings:openai value (user secret locally) is used. Host-injected connection strings win.
        builder.AddKeyVaultSecretsIfConfigured();

        builder.AddServiceDefaults();
        builder.Services.AddOpenApi();

        builder.Services.AddQuerying();
        builder.Services.AddOptions<QueryOptions>()
            .Bind(builder.Configuration.GetSection(QueryOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.AddAzureOpenAIEmbeddings();
        builder.AddAzureOpenAIChat();
        builder.AddPersistence();

        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<DomainExceptionHandler>();
        builder.Services.AddExceptionHandler<BrokenCircuitExceptionHandler>();
        builder.Services.AddExceptionHandler<AiServiceExceptionHandler>();
        builder.Services.AddClientRateLimitPolicy<QueryRateLimitOptions>(builder.Configuration, QueryRateLimitOptions.PolicyName, QueryRateLimitOptions.SectionName);
        builder.Services.AddApiCors(builder.Configuration);
        builder.Services.AddCommonEndpoints();
        builder.Services.AddEndpoints(typeof(QueryApiHost).Assembly);

        return builder;
    }

    public static WebApplication ConfigurePipeline(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseExceptionHandler();
        app.UseCors();
        app.UseRateLimiter();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.MapEndpoints();

        return app;
    }
}
