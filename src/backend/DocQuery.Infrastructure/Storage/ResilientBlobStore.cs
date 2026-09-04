using DocQuery.Application.Abstractions;
using DocQuery.Infrastructure.Resilience;
using Polly;
using Polly.Registry;

namespace DocQuery.Infrastructure.Storage;

/// <summary>
/// Decorates the raw <see cref="IBlobStore"/> with the blob-storage resilience pipeline. Each attempt rewinds the
/// stream so a retry resends the whole file; non-seekable streams are rejected because a partial retry would
/// silently store a truncated document.
/// </summary>
internal sealed class ResilientBlobStore(
    IBlobStore inner,
    ResiliencePipelineProvider<string> pipelines) : IBlobStore
{
    public Task UploadAsync(string blobName, Stream content, string contentType, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!content.CanSeek)
        {
            throw new ArgumentException("The content stream must be seekable so failed uploads can be retried.", nameof(content));
        }

        var pipeline = pipelines.GetPipeline(ResiliencePipelineNames.BlobStorage);
        var state = new UploadState(inner, blobName, content, contentType);

        return pipeline.ExecuteAsync(
            static async (state, token) =>
            {
                state.Content.Position = 0;
                await state.Inner.UploadAsync(state.BlobName, state.Content, state.ContentType, token);
            },
            state,
            cancellationToken).AsTask();
    }

    private sealed record UploadState(IBlobStore Inner, string BlobName, Stream Content, string ContentType);
}
