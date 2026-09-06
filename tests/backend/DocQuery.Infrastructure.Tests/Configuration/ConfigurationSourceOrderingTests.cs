using DocQuery.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;

namespace DocQuery.Infrastructure.Tests.Configuration;

public sealed class ConfigurationSourceOrderingTests : IDisposable
{
    private const string Prefix = "DOCQUERY_ORDERING_TEST_";

    public ConfigurationSourceOrderingTests()
    {
        Environment.SetEnvironmentVariable(Prefix + "ConnectionStrings__servicebus", "from-environment");
    }

    public void Dispose() => Environment.SetEnvironmentVariable(Prefix + "ConnectionStrings__servicebus", null);

    [Fact]
    public void Environment_variables_override_values_from_the_added_source()
    {
        var configuration = new ConfigurationManager();
        configuration.AddEnvironmentVariables(Prefix);

        configuration.AddBelowEnvironmentVariables(() => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:servicebus"] = "from-vault",
            ["ConnectionStrings:openai"] = "from-vault",
        }));

        Assert.Equal("from-environment", configuration.GetConnectionString("servicebus"));
        Assert.Equal("from-vault", configuration.GetConnectionString("openai"));
    }

    [Fact]
    public void Added_sources_are_placed_before_the_first_environment_variable_source_and_keep_their_order()
    {
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["key"] = "settings" });
        configuration.AddEnvironmentVariables("DOTNET_");
        configuration.AddEnvironmentVariables();
        configuration.AddCommandLine([]);

        configuration.AddBelowEnvironmentVariables(() =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["key"] = "added-1" });
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["key"] = "added-2" });
        });

        var sources = configuration.Sources.ToList();
        var firstEnvironment = sources.FindIndex(ConfigurationSourceOrdering.IsEnvironmentVariablesSource);
        var lastAdded = sources.FindLastIndex(source => source is MemoryConfigurationSource);
        Assert.True(lastAdded < firstEnvironment, $"added sources end at {lastAdded}; first environment source is at {firstEnvironment}");
        Assert.Equal("added-2", configuration["key"]);
    }

    [Fact]
    public void Recognises_the_environment_variable_source_by_type()
    {
        var configuration = new ConfigurationManager();
        configuration.AddEnvironmentVariables();
        configuration.AddInMemoryCollection();

        Assert.Equal(1, configuration.Sources.Count(ConfigurationSourceOrdering.IsEnvironmentVariablesSource));
        Assert.DoesNotContain(configuration.Sources, source => source is MemoryConfigurationSource && ConfigurationSourceOrdering.IsEnvironmentVariablesSource(source));
    }

    [Fact]
    public void Without_an_environment_variable_source_the_added_source_stays_last()
    {
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["key"] = "first" });

        configuration.AddBelowEnvironmentVariables(() => configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["key"] = "added" }));

        Assert.Equal("added", configuration["key"]);
    }

    [Fact]
    public void Adding_nothing_leaves_the_sources_untouched()
    {
        var configuration = new ConfigurationManager();
        configuration.AddEnvironmentVariables(Prefix);
        var before = configuration.Sources.ToList();

        configuration.AddBelowEnvironmentVariables(() => { });

        Assert.Equal(before, configuration.Sources);
    }
}
