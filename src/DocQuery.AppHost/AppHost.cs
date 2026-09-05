var builder = DistributedApplication.CreateBuilder(args);

// Azure Blob Storage, run locally as Azurite. The "documents" container holds uploaded PDFs. The blob port is
// pinned to 10000 so Azure Storage Explorer's built-in "Emulator - Default Ports" connection works unchanged.
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(azurite => azurite
        .WithDataVolume()
        .WithLifetime(ContainerLifetime.Persistent)
        .WithBlobPort(10000));
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

// Azure Service Bus, run locally as the emulator (which brings its own SQL Edge sidecar). One topic carries every
// document event; each worker owns a subscription (ADR 0014).
var serviceBus = builder.AddAzureServiceBus("servicebus")
    .RunAsEmulator(emulator => emulator.WithLifetime(ContainerLifetime.Persistent));
var documentEvents = serviceBus.AddServiceBusTopic("document-events");
documentEvents.AddServiceBusSubscription("chunking");

var commandApi = builder.AddProject<Projects.DocQuery_Command_Api>("command-api")
    .WithReference(documents).WaitFor(documents)
    .WithReference(database).WaitFor(database);

// The relay waits for the API because the API applies migrations at startup.
builder.AddProject<Projects.DocQuery_Workers_OutboxRelay>("outbox-relay")
    .WithReference(database).WaitFor(database)
    .WithReference(serviceBus).WaitFor(serviceBus)
    .WaitFor(commandApi);

builder.AddViteApp("web", "../clients/docquery.web")
    .WithReference(commandApi)
    .WithEnvironment("VITE_API_URL", commandApi.GetEndpoint("http"));

builder.Build().Run();
