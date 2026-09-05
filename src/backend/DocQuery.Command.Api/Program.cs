using DocQuery.Command.Api;

var builder = WebApplication.CreateBuilder(args);

CommandApiHost.ConfigureServices(builder);

var app = builder.Build();

CommandApiHost.ConfigurePipeline(app);

app.Run();

/// <summary>Exposed so integration tests can bootstrap the host via WebApplicationFactory.</summary>
public partial class Program;
