namespace DocQuery.Infrastructure.Resilience;

/// <summary>Bound from <c>Resilience:ServiceBus</c>. Defaults suit Azure Service Bus throttling behaviour.</summary>
public sealed class ServiceBusResilienceOptions : ResiliencePipelineOptions
{
    public const string SectionName = "Resilience:ServiceBus";
}
