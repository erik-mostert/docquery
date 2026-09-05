var builder = DistributedApplication.CreateBuilder(args);

// Azure Blob Storage, run locally as Azurite. The "documents" container holds uploaded PDFs.
var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var documents = storage.AddBlobContainer("documents");

// PostgreSQL from the pgvector image so embeddings can be stored later without changing the server.
var postgres = builder.AddPostgres("postgres")
    .WithImage("pgvector/pgvector")
    .WithImageTag("pg17");
var database = postgres.AddDatabase("docquery");

var commandApi = builder.AddProject<Projects.DocQuery_Command_Api>("command-api")
    .WithReference(documents).WaitFor(documents)
    .WithReference(database).WaitFor(database);

builder.AddViteApp("web", "../clients/docquery.web")
    .WithReference(commandApi)
    .WithEnvironment("VITE_API_URL", commandApi.GetEndpoint("http"));

builder.Build().Run();
