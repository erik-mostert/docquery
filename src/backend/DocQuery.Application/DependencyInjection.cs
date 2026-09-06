using DocQuery.Application.Abstractions;
using DocQuery.Application.Documents.Chunking;
using DocQuery.Application.Documents.Embedding;
using DocQuery.Application.Documents.Listing;
using DocQuery.Application.Documents.Querying;
using DocQuery.Application.Documents.Upload;
using DocQuery.Application.Messaging;
using DocQuery.Contracts.Documents;
using DocQuery.Domain.Documents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DocQuery.Application;

public static class DependencyInjection
{
    /// <summary>Command handlers for the write API. Infrastructure registers the abstractions they depend on.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ICommandHandler<UploadDocumentCommand, DocumentId>, UploadDocumentHandler>();

        return services;
    }

    /// <summary>
    /// Message handlers for the chunking worker. Kept separate from <see cref="AddApplication"/> because they depend
    /// on services (PDF extraction, tokenizer, chunk repository) that only the chunking host provides.
    /// </summary>
    public static IServiceCollection AddChunking(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.AddIntegrationMessageHandler<DocumentUploaded, DocumentUploadedHandler>();

        return services;
    }

    /// <summary>
    /// Query handlers for the read API; depends on an <c>IEmbeddingGenerator</c>, an <c>IChatClient</c> and an
    /// <c>IChunkSearch</c> the host provides. <see cref="QueryOptions"/> is bound by the host.
    /// </summary>
    public static IServiceCollection AddQuerying(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IQueryHandler<AskQuestionQuery, AskQuestionResult>, AskQuestionHandler>();
        services.AddScoped<IQueryHandler<ListDocumentsQuery, IReadOnlyList<DocumentSummary>>, ListDocumentsHandler>();
        services.AddScoped<IQueryHandler<GetDocumentQuery, DocumentSummary?>, GetDocumentHandler>();

        return services;
    }

    /// <summary>Message handlers for the embedding worker; depends on an <c>IEmbeddingGenerator</c> the host provides.</summary>
    public static IServiceCollection AddEmbedding(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.AddIntegrationMessageHandler<DocumentChunked, DocumentChunkedHandler>();

        return services;
    }
}
