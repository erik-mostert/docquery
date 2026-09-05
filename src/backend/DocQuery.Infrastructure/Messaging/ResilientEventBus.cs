using DocQuery.Application.Abstractions;
using DocQuery.Application.Messaging;
using DocQuery.Infrastructure.Resilience;
using Polly.Registry;

namespace DocQuery.Infrastructure.Messaging;

/// <summary>Decorates the raw <see cref="IEventBus"/> with the service-bus resilience pipeline (ADR 0007).</summary>
internal sealed class ResilientEventBus(
    IEventBus inner,
    ResiliencePipelineProvider<string> pipelines) : IEventBus
{
    public Task PublishAsync(OutboundMessage message, CancellationToken cancellationToken)
    {
        var pipeline = pipelines.GetPipeline(ResiliencePipelineNames.ServiceBus);

        return pipeline.ExecuteAsync(
            static async (state, token) => await state.Inner.PublishAsync(state.Message, token),
            (Inner: inner, Message: message),
            cancellationToken).AsTask();
    }
}
