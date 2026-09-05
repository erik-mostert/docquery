using DocQuery.Application;
using DocQuery.Application.Documents.Chunking;
using DocQuery.Infrastructure;
using DocQuery.Infrastructure.Messaging;
using DocQuery.Infrastructure.Pdf;
using DocQuery.Infrastructure.Persistence;
using DocQuery.Infrastructure.Text;

namespace DocQuery.Workers.Chunking;

/// <summary>Composition of the chunking worker, kept out of Program.cs so tests can build the same host.</summary>
public static class ChunkingHost
{
    public static HostApplicationBuilder Configure(HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddServiceDefaults();

        builder.Services.AddChunking();
        builder.Services.AddPdfTextExtraction();
        builder.Services.AddTokenizer();
        builder.Services.AddOptions<ChunkingOptions>()
            .Bind(builder.Configuration.GetSection(ChunkingOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => options.OverlapTokens < options.MaxTokens, "Chunking: OverlapTokens must be smaller than MaxTokens.")
            .ValidateOnStart();

        builder.AddInfrastructure();
        builder.AddPersistence();
        builder.AddServiceBusConsumer();

        return builder;
    }
}
