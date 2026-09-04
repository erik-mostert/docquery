var builder = DistributedApplication.CreateBuilder(args);

var commandApi = builder.AddProject<Projects.DocQuery_Command_Api>("command-api");

builder.AddViteApp("web", "../clients/docquery.web")
    .WithReference(commandApi)
    .WithEnvironment("VITE_API_URL", commandApi.GetEndpoint("http"));

builder.Build().Run();
