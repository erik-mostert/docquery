using Xunit;

namespace DocQuery.Tests.Common;

/// <summary>A fact that runs only when the named environment variable is set; used for opt-in tests against live services.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class EnvironmentFactAttribute : FactAttribute
{
    public EnvironmentFactAttribute(string variableName)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(variableName)))
        {
            Skip = FormattableString.Invariant($"Set {variableName} to run this test.");
        }
    }
}
