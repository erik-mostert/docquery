using DocQuery.Query.Api;

var builder = WebApplication.CreateBuilder(args);

QueryApiHost.ConfigureServices(builder);

var app = builder.Build();

QueryApiHost.ConfigurePipeline(app);

app.Run();

/// <summary>Exposed so integration tests can bootstrap the host via WebApplicationFactory.</summary>
public partial class Program;
