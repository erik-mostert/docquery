using DocQuery.Application.Abstractions;
using DocQuery.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DocQuery.Command.Api.Tests;

/// <summary>
/// Hosts the Command API in-process with the shared fakes replacing every outbound dependency. Connection strings
/// are syntactically valid placeholders so the Aspire integrations register, but nothing connects: startup jobs
/// (migrations, container creation) are switched off and dependency health checks removed. The upload rate limit
/// is raised so unrelated tests never trip it; individual tests lower it via <see cref="WebApplicationFactory{T}.WithWebHostBuilder"/>.
/// </summary>
public sealed class CommandApiFactory : WebApplicationFactory<Program>
{
    public static readonly DateTimeOffset Now = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    // Endpoint form: the client is built with a URI and a credential, so nothing is contacted at startup.
    private const string PlaceholderBlobConnection = "Endpoint=http://127.0.0.1:10000/devstoreaccount1;ContainerName=documents";

    private const string PlaceholderDatabaseConnection = "Host=localhost;Database=docquery;Username=postgres;Password=placeholder";

    public FakeBlobStore BlobStore { get; } = new();

    public InMemoryDocumentRepository Documents { get; } = new();

    public FakeUnitOfWork UnitOfWork { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting lands in host configuration before Program.cs runs. ConfigureAppConfiguration would be applied
        // only at Build(), too late for values the Aspire integrations read while registering services.
        builder.UseSetting("ConnectionStrings:documents", PlaceholderBlobConnection);
        builder.UseSetting("ConnectionStrings:docquery", PlaceholderDatabaseConnection);
        builder.UseSetting("BlobStorage:CreateContainerOnStartup", "false");
        builder.UseSetting("Persistence:ApplyMigrationsOnStartup", "false");
        builder.UseSetting("RateLimiting:Uploads:PermitLimit", "1000");

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

            // Keep only the liveness self-check; dependency checks would try to reach the placeholders.
            services.PostConfigure<HealthCheckServiceOptions>(options =>
            {
                var self = options.Registrations.Where(registration => registration.Name == "self").ToList();
                options.Registrations.Clear();
                foreach (var registration in self)
                {
                    options.Registrations.Add(registration);
                }
            });
        });
    }
}
