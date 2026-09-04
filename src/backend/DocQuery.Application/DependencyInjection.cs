using DocQuery.Application.Abstractions;
using DocQuery.Application.Documents.Upload;
using DocQuery.Domain.Documents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DocQuery.Application;

public static class DependencyInjection
{
    /// <summary>Registers command handlers. Infrastructure registers the abstractions they depend on.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ICommandHandler<UploadDocumentCommand, DocumentId>, UploadDocumentHandler>();

        return services;
    }
}
