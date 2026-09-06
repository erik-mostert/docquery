using DocQuery.Infrastructure.Resilience;
using Microsoft.Extensions.AI;
using Polly.Registry;

namespace DocQuery.Infrastructure.AI;

/// <summary>
/// Applies the Azure OpenAI resilience pipeline (retry with backoff and jitter, circuit breaker, timeout) around
/// chat completions (ADR 0007). Streaming is not wrapped: a retry cannot replay a partially consumed stream.
/// </summary>
internal sealed class ResilientChatClient(IChatClient inner, ResiliencePipelineProvider<string> pipelines) : DelegatingChatClient(inner)
{
    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        // Materialise once: a retried attempt must send the same messages, not re-enumerate a lazy sequence.
        var request = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();
        var pipeline = pipelines.GetPipeline(ResiliencePipelineNames.AzureOpenAI);

        return pipeline.ExecuteAsync(
            static async (state, token) => await state.Inner.GetResponseAsync(state.Messages, state.Options, token),
            (Inner: InnerClient, Messages: request, Options: options),
            cancellationToken).AsTask();
    }
}
