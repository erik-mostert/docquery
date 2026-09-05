using DocQuery.Workers.OutboxRelay;

var builder = Host.CreateApplicationBuilder(args);

OutboxRelayHost.Configure(builder);

builder.Build().Run();
