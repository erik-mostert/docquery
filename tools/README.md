# Tools

Small single-file .NET 10 programs for poking at the running system. Run them with `dotnet run <file> -- <args>`;
no project file is needed. They are not part of the solution build and are not deployed.

| Tool | Purpose |
|---|---|
| [peek-servicebus.cs](peek-servicebus.cs) | Lists the messages waiting on a Service Bus topic subscription without consuming them. Use it to see what the outbox relay published, since the Service Bus emulator has no UI or management API. |

## peek-servicebus.cs

```bash
dotnet run tools/peek-servicebus.cs -- "<connection string>" [topic] [subscription] [max]
```

| Argument | Default | Notes |
|---|---|---|
| connection string | required | Shown on the `servicebus` resource in the Aspire dashboard. For the local emulator: `Endpoint=sb://localhost:<port>;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;` where `<port>` is the host port mapped to 5672 (`docker ps`). |
| topic | `document-events` | |
| subscription | `chunking` | |
| max | `50` | Maximum messages to show. |

Prints enqueue time, sequence number, `MessageId` (the outbox row id), `Subject` (contract type name),
content type, application properties and the JSON body. Peeking leaves the messages in place for the real
consumer.

## Conventions

- One file per tool, named `verb-noun.cs`, with a comment block at the top stating purpose and usage.
- Declare NuGet dependencies with `#:package Name@Version` and add `#:property ManagePackageVersionsCentrally=false`,
  because the repository uses central package management and file-based programs cannot use `Directory.Packages.props`.
- Read-only by default. A tool that mutates state (for example, dead-lettering or purging messages) must say so in
  its top comment and require an explicit flag.
- Add a row to the table above when adding a tool.
