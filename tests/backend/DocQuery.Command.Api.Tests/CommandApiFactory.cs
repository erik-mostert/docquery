using DocQuery.Application.Abstractions;
using DocQuery.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DocQuery.Command.Api.Tests;

/// <summary>
/// Hosts the Command API in-process with the shared fakes replacing every outbound dependency. The upload rate
/// limit is raised so unrelated tests never trip it; individual tests lower it via <see cref="WebApplicationFactory{T}.WithWebHostBuilder"/>.
/// </summary>
public sealed class CommandApiFactory : WebApplicationFactory<Program>
{
    public static readonly DateTimeOffset Now = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    public FakeBlobStore BlobStore { get; } = new();

    public InMemoryDocumentRepository Documents { get; } = new();

    public FakeUnitOfWork UnitOfWork { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RateLimiting:Uploads:PermitLimit"] = "1000",
        }));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IBlobStore>();
            services.AddSingleton<IBlobStore>(BlobStore);

            services.RemoveAll<IDocumentRepository>();
            services.AddSingleton<IDocumentRepository>(Documents);

            services.RemoveAll<IUnitOfWork>();
            services.AddSingleton<IUnitOfWork>(UnitOfWork);

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        });
    }
}
