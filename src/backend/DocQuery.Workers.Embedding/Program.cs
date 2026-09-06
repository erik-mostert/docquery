using DocQuery.Workers.Embedding;

var builder = Host.CreateApplicationBuilder(args);

EmbeddingHost.Configure(builder);

builder.Build().Run();
