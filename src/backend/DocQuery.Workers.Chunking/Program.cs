using DocQuery.Workers.Chunking;

var builder = Host.CreateApplicationBuilder(args);

ChunkingHost.Configure(builder);

builder.Build().Run();
