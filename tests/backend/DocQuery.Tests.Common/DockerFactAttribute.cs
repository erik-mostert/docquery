using Xunit;

namespace DocQuery.Tests.Common;

/// <summary>A fact that runs only when a Docker engine is reachable; otherwise it is reported as skipped.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        if (!DockerAvailability.IsAvailable)
        {
            Skip = DockerAvailability.SkipReason;
        }
    }
}
