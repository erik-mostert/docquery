using DocQuery.Application.Abstractions;
using DocQuery.Infrastructure.Persistence.Outbox;
using DocQuery.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace DocQuery.Infrastructure.Persistence;

public static class PersistenceExtensions
{
    public const string DefaultConnectionName = "docquery";

    /// <summary>
    /// Registers the PostgreSQL <see cref="DocQueryDbContext"/> through the Aspire Npgsql integration (connection
    /// string, health check, tracing, retrying execution strategy) plus repositories, unit of work and outbox mappers.
    /// </summary>
    public static IHostApplicationBuilder AddPersistence(this IHostApplicationBuilder builder, string connectionName = DefaultConnectionName)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddNpgsqlDbContext<DocQueryDbContext>(
            connectionName,
            configureDbContextOptions: options => options.UseNpgsql(npgsql => npgsql.UseVector()));
        builder.Services.AddPersistenceServices();

        var options = builder.Configuration.GetSection(PersistenceOptions.SectionName).Get<PersistenceOptions>() ?? new PersistenceOptions();
        if (options.ApplyMigrationsOnStartup)
        {
            builder.Services.AddHostedService<MigrationRunner>();
        }

        return builder;
    }

    /// <summary>Everything except the DbContext registration, so tests can point the context at a container.</summary>
    public static IServiceCollection AddPersistenceServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IDocumentChunkRepository, DocumentChunkRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IIntegrationEventMapper, DocumentUploadedMapper>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IIntegrationEventMapper, DocumentChunkedMapper>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IIntegrationEventMapper, DocumentEmbeddedMapper>());

        return services;
    }
}
