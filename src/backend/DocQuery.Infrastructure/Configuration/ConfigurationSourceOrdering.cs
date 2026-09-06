using Microsoft.Extensions.Configuration;

namespace DocQuery.Infrastructure.Configuration;

/// <summary>
/// Places late-added configuration sources (Key Vault) below the environment-variable sources, so values the host
/// environment sets explicitly (Aspire locally, Kubernetes later) win and the added source only fills the gaps.
/// </summary>
public static class ConfigurationSourceOrdering
{
    // The source type is internal to Microsoft.Extensions.Configuration.EnvironmentVariables, so it is matched by name.
    private const string EnvironmentVariablesSourceTypeName =
        "Microsoft.Extensions.Configuration.EnvironmentVariables.EnvironmentVariablesConfigurationSource";

    /// <summary>
    /// Runs <paramref name="add"/>, then moves every source it appended to just before the first environment-variable
    /// source. Sources keep their relative order. Without an environment-variable source nothing moves.
    /// </summary>
    public static void AddBelowEnvironmentVariables(this IConfigurationBuilder configuration, Action add)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(add);

        var sources = configuration.Sources;
        var countBefore = sources.Count;
        add();

        var added = sources.Skip(countBefore).ToList();
        var target = FirstEnvironmentVariablesIndex(sources, countBefore);
        if (added.Count == 0 || target < 0)
        {
            return;
        }

        for (var index = sources.Count - 1; index >= countBefore; index--)
        {
            sources.RemoveAt(index);
        }

        for (var offset = 0; offset < added.Count; offset++)
        {
            sources.Insert(target + offset, added[offset]);
        }
    }

    private static int FirstEnvironmentVariablesIndex(IList<IConfigurationSource> sources, int count)
    {
        for (var index = 0; index < count; index++)
        {
            if (IsEnvironmentVariablesSource(sources[index]))
            {
                return index;
            }
        }

        return -1;
    }

    public static bool IsEnvironmentVariablesSource(IConfigurationSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.GetType().FullName == EnvironmentVariablesSourceTypeName;
    }
}
