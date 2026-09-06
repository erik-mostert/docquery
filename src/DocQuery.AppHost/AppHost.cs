using Aspire.Hosting.Azure;
using DocQuery.Contracts.Documents;
using DocQuery.Contracts.Messaging;
using Microsoft.Extensions.Configuration;

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
// document event; each worker owns a subscription that only receives its own message type through a correlation
// filter on Subject (ADR 0014, ADR 0018). The emulator reads this configuration when its container is created, so
// after changing it remove the persistent servicebus containers (see README).
var serviceBus = builder.AddAzureServiceBus("servicebus")
    .RunAsEmulator(emulator => emulator.WithLifetime(ContainerLifetime.Persistent));
var documentEvents = serviceBus.AddServiceBusTopic("document-events");
documentEvents.AddServiceBusSubscription("chunking").WithProperties(subscription => ReceiveOnly(subscription, MessageSubject.Of<DocumentUploaded>()));
documentEvents.AddServiceBusSubscription("embedding").WithProperties(subscription => ReceiveOnly(subscription, MessageSubject.Of<DocumentChunked>()));

// Declared first so the APIs can allow its origin (Aspire picks the Vite port). Its API URLs are attached below.
var web = builder.AddViteApp("web", "../clients/docquery.web");

var commandApi = builder.AddProject<Projects.DocQuery_Command_Api>("command-api")
    .WithReference(documents).WaitFor(documents)
    .WithReference(database).WaitFor(database)
    .WithEnvironment("Cors__AllowedOrigins__0", web.GetEndpoint("http"));

// The relay waits for the API because the API applies migrations at startup.
builder.AddProject<Projects.DocQuery_Workers_OutboxRelay>("outbox-relay")
    .WithReference(database).WaitFor(database)
    .WithReference(serviceBus).WaitFor(serviceBus)
    .WaitFor(commandApi);

// Azure OpenAI has no emulator. The embedding worker reads ConnectionStrings:openai either from Key Vault (when
// ConnectionStrings:keyvault, the vault URI, is set as an AppHost user secret; see infra/README.md) or directly from
// a ConnectionStrings:openai user secret. Both are optional here so the rest of the stack runs without Azure; the
// worker itself fails fast at startup when neither is provided.
var keyVault = builder.Configuration.GetConnectionString("keyvault") is not null ? builder.AddConnectionString("keyvault") : null;
var openAi = builder.Configuration.GetConnectionString("openai") is not null ? builder.AddConnectionString("openai") : null;

// Consumes DocumentUploaded from the "chunking" subscription; reads blobs, writes chunks and outbox rows.
// Resource names are one namespace, so the project cannot also be called "chunking".
builder.AddProject<Projects.DocQuery_Workers_Chunking>("chunking-worker")
    .WithReference(documents).WaitFor(documents)
    .WithReference(database).WaitFor(database)
    .WithReference(serviceBus).WaitFor(serviceBus)
    .WaitFor(commandApi);

// Consumes DocumentChunked from the "embedding" subscription; calls Azure OpenAI, writes vectors and outbox rows.
var embeddingWorker = builder.AddProject<Projects.DocQuery_Workers_Embedding>("embedding-worker")
    .WithReference(database).WaitFor(database)
    .WithReference(serviceBus).WaitFor(serviceBus)
    .WaitFor(commandApi);

// Read side (ADR 0002): embeds the question, searches pgvector and asks the chat model for a grounded answer.
// Waits for the command API because that applies the migrations. Same Azure OpenAI wiring as the embedding worker.
var queryApi = builder.AddProject<Projects.DocQuery_Query_Api>("query-api")
    .WithReference(database).WaitFor(database)
    .WithEnvironment("Cors__AllowedOrigins__0", web.GetEndpoint("http"))
    .WaitFor(commandApi);

foreach (var azureConsumer in new[] { embeddingWorker, queryApi })
{
    if (keyVault is not null)
    {
        azureConsumer.WithReference(keyVault);
    }

    if (openAi is not null)
    {
        azureConsumer.WithReference(openAi);
    }
}

web.WithReference(commandApi)
    .WithReference(queryApi)
    .WithEnvironment("VITE_API_URL", commandApi.GetEndpoint("http"))
    .WithEnvironment("VITE_QUERY_API_URL", queryApi.GetEndpoint("http"));

builder.Build().Run();

// Five delivery attempts before a message is dead-lettered; permanent failures are dead-lettered immediately.
// With an explicit rule the emulator creates no catch-all rule, so only messages with this Subject are delivered.
static void ReceiveOnly(AzureServiceBusSubscriptionResource subscription, string subject)
{
    subscription.MaxDeliveryCount = 5;
    subscription.Rules.Add(new AzureServiceBusRule("subject")
    {
        FilterType = AzureServiceBusFilterType.CorrelationFilter,
        CorrelationFilter = new AzureServiceBusCorrelationFilter { Subject = subject },
    });
}
