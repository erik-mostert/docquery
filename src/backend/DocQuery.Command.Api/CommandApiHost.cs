using DocQuery.Api.Common.Cors;
using DocQuery.Api.Common.Endpoints;
using DocQuery.Api.Common.ExceptionHandling;
using DocQuery.Api.Common.RateLimiting;
using DocQuery.Application;
using DocQuery.Command.Api.Options;
using DocQuery.Command.Api.RateLimiting;
using DocQuery.Infrastructure;
using DocQuery.Infrastructure.Configuration;
using DocQuery.Infrastructure.Persistence;

namespace DocQuery.Command.Api;

/// <summary>Composition of the Command API, kept out of Program.cs so tests can build the same host without starting it.</summary>
public static class CommandApiHost
{
    public static WebApplicationBuilder ConfigureServices(WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // In Kubernetes the PostgreSQL connection string comes from Key Vault through workload identity (ADR 0021);
        // locally the AppHost injects it and no vault URI is configured.
        builder.AddKeyVaultSecretsIfConfigured();

        builder.AddServiceDefaults();
        builder.Services.AddOpenApi();

        builder.Services.AddApplication();
        builder.AddInfrastructure();
        builder.AddPersistence();

        builder.Services.AddUploadOptions(builder.Configuration);
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<DomainExceptionHandler>();
        builder.Services.AddExceptionHandler<BrokenCircuitExceptionHandler>();
        builder.Services.AddClientRateLimitPolicy<UploadRateLimitOptions>(builder.Configuration, UploadRateLimitOptions.PolicyName, UploadRateLimitOptions.SectionName);
        builder.Services.AddApiCors(builder.Configuration);
        builder.Services.AddCommonEndpoints();
        builder.Services.AddEndpoints(typeof(CommandApiHost).Assembly);

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
