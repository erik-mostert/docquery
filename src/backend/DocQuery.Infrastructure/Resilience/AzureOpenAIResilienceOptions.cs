namespace DocQuery.Infrastructure.Resilience;

/// <summary>Bound from <c>Resilience:AzureOpenAI</c>. Azure OpenAI throttles with 429; backoff with jitter is the main tool.</summary>
public sealed class AzureOpenAIResilienceOptions : ResiliencePipelineOptions
{
    public const string SectionName = "Resilience:AzureOpenAI";
}
