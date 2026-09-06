namespace DocQuery.Infrastructure.AI;

/// <summary>
/// The <c>Endpoint=https://…;Key=…</c> format used for <c>ConnectionStrings:openai</c> (the Aspire convention).
/// A missing key means "authenticate with the current identity" (managed identity in Azure, <c>az login</c> locally).
/// </summary>
public sealed record AzureOpenAIConnectionString(Uri Endpoint, string? Key)
{
    public static AzureOpenAIConnectionString Parse(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        Uri? endpoint = null;
        string? key = null;

        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = part.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
            {
                throw new FormatException(FormattableString.Invariant($"Azure OpenAI connection string segment '{part}' is not key=value."));
            }

            var name = part[..separator].Trim();
            var value = part[(separator + 1)..].Trim();

            if (name.Equals("Endpoint", StringComparison.OrdinalIgnoreCase))
            {
                if (!Uri.TryCreate(value, UriKind.Absolute, out endpoint) || endpoint.Scheme != Uri.UriSchemeHttps)
                {
                    throw new FormatException("Azure OpenAI Endpoint must be an absolute https URL.");
                }
            }
            else if (name.Equals("Key", StringComparison.OrdinalIgnoreCase))
            {
                key = value.Length > 0 ? value : null;
            }
            else
            {
                throw new FormatException(FormattableString.Invariant($"Azure OpenAI connection string has an unknown segment '{name}'."));
            }
        }

        return endpoint is null
            ? throw new FormatException("Azure OpenAI connection string must contain Endpoint=https://….")
            : new AzureOpenAIConnectionString(endpoint, key);
    }
}
