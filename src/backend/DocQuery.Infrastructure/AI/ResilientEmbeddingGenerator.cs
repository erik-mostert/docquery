using DocQuery.Infrastructure.Resilience;
using Microsoft.Extensions.AI;
using Polly.Registry;

namespace DocQuery.Infrastructure.AI;

/// <summary>Applies the Azure OpenAI resilience pipeline (retry with backoff and jitter, circuit breaker, timeout) around embedding calls (ADR 0007).</summary>
internal sealed class ResilientEmbeddingGenerator(
    IEmbeddingGenerator<string, Embedding<float>> inner,
    ResiliencePipelineProvider<string> pipelines) : DelegatingEmbeddingGenerator<string, Embedding<float>>(inner)
{
    public override Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);

        // Materialise once: a retried attempt must send the same inputs, not re-enumerate a lazy sequence.
        var inputs = values as IReadOnlyList<string> ?? values.ToList();
        var pipeline = pipelines.GetPipeline(ResiliencePipelineNames.AzureOpenAI);

        return pipeline.ExecuteAsync(
            static async (state, token) => await state.Inner.GenerateAsync(state.Inputs, state.Options, token),
            (Inner: InnerGenerator, Inputs: inputs, Options: options),
            cancellationToken).AsTask();
    }
}
