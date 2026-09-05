using DocQuery.Application.Abstractions;
using DocQuery.Infrastructure.Resilience;
using Polly;
using Polly.Registry;

namespace DocQuery.Infrastructure.Storage;

/// <summary>
/// Decorates the raw <see cref="IBlobStore"/> with the blob-storage resilience pipeline. Each upload attempt rewinds
/// the stream so a retry resends the whole file; non-seekable streams are rejected because a partial retry would
/// silently store a truncated document. Downloads return a fresh stream per attempt, so they need no rewinding.
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

        var state = new UploadState(inner, blobName, content, contentType);

        return Pipeline.ExecuteAsync(
            static async (state, token) =>
            {
                state.Content.Position = 0;
                await state.Inner.UploadAsync(state.BlobName, state.Content, state.ContentType, token);
            },
            state,
            cancellationToken).AsTask();
    }

    public Task<Stream> DownloadAsync(string blobName, CancellationToken cancellationToken) =>
        Pipeline.ExecuteAsync(
            static async (state, token) => await state.Inner.DownloadAsync(state.BlobName, token),
            (Inner: inner, BlobName: blobName),
            cancellationToken).AsTask();

    private ResiliencePipeline Pipeline => pipelines.GetPipeline(ResiliencePipelineNames.BlobStorage);

    private sealed record UploadState(IBlobStore Inner, string BlobName, Stream Content, string ContentType);
}
