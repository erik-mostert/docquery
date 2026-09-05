var builder = DistributedApplication.CreateBuilder(args);

// Azure Blob Storage, run locally as Azurite. The "documents" container holds uploaded PDFs.
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(azurite => azurite.WithDataVolume().WithLifetime(ContainerLifetime.Persistent));
var documents = storage.AddBlobContainer("documents");

// PostgreSQL from the pgvector image so embeddings can be stored later without changing the server.
// Data volumes keep uploads and rows across AppHost sessions; persistent lifetime keeps the containers running
// between sessions so startup is fast. Reset with `docker volume rm` / `docker rm` on the docquery containers.
var postgres = builder.AddPostgres("postgres")
    .WithImage("pgvector/pgvector")
    .WithImageTag("pg17")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);
var database = postgres.AddDatabase("docquery");

var commandApi = builder.AddProject<Projects.DocQuery_Command_Api>("command-api")
    .WithReference(documents).WaitFor(documents)
    .WithReference(database).WaitFor(database);

builder.AddViteApp("web", "../clients/docquery.web")
    .WithReference(commandApi)
    .WithEnvironment("VITE_API_URL", commandApi.GetEndpoint("http"));

builder.Build().Run();
