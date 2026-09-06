using DocQuery.Infrastructure.AI;
using DocQuery.Tests.Common;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DocQuery.Infrastructure.Tests.AI;

/// <summary>
/// Opt-in check against a real Azure OpenAI chat deployment. Set DOCQUERY_OPENAI_CONNECTION to
/// "Endpoint=https://…;Key=…" (and optionally DOCQUERY_OPENAI_CHAT_DEPLOYMENT) to run it.
/// </summary>
public sealed class AzureOpenAIChatSmokeTests
{
    private const string ConnectionVariable = "DOCQUERY_OPENAI_CONNECTION";

    [EnvironmentFact(ConnectionVariable)]
    public async Task Deployment_answers_a_prompt()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:openai"] = Environment.GetEnvironmentVariable(ConnectionVariable),
            ["AzureOpenAI:ChatDeployment"] = Environment.GetEnvironmentVariable("DOCQUERY_OPENAI_CHAT_DEPLOYMENT") ?? "gpt-5-mini",
        });
        builder.AddAzureOpenAIChat();
        using var host = builder.Build();
        var chat = host.Services.GetRequiredService<IChatClient>();

        var response = await chat.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Reply with the single word: pong")],
            new ChatOptions { MaxOutputTokens = 200 });

        Assert.Contains("pong", response.Text, StringComparison.OrdinalIgnoreCase);
    }
}
