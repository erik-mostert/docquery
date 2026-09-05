using DocQuery.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DocQuery.Infrastructure.Pdf;

public static class PdfTextExtractionExtensions
{
    public static IServiceCollection AddPdfTextExtraction(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IPdfTextExtractor, PdfPigTextExtractor>();

        return services;
    }
}
