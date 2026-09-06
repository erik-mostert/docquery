using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace DocQuery.Infrastructure.Configuration;

public static class KeyVaultConfigurationExtensions
{
    public const string DefaultConnectionName = "keyvault";

    /// <summary>
    /// When <c>ConnectionStrings:{connectionName}</c> holds a vault URI, adds the vault's secrets as configuration
    /// ("ConnectionStrings--openai" becomes ConnectionStrings:openai) through the current identity. The vault sits
    /// below the environment variables, so connection strings the host injects (Aspire's emulators locally) keep
    /// precedence over the Azure ones stored in the vault. Without a vault URI nothing is added.
    /// </summary>
    public static IHostApplicationBuilder AddKeyVaultSecretsIfConfigured(
        this IHostApplicationBuilder builder,
        string connectionName = DefaultConnectionName)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.Configuration.GetConnectionString(connectionName) is null)
        {
            return builder;
        }

        builder.Configuration.AddBelowEnvironmentVariables(() => builder.Configuration.AddAzureKeyVaultSecrets(connectionName));
        return builder;
    }
}
