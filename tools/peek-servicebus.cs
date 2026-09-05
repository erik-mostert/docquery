// Peeks messages waiting on a Service Bus topic subscription without consuming them.
// The Service Bus emulator has no UI or management API, so this is the way to see what the outbox relay published.
//
//   dotnet run tools/peek-servicebus.cs -- "<connection string from the servicebus resource in the Aspire dashboard>"
//   dotnet run tools/peek-servicebus.cs -- "<connection string>" document-events chunking 100
//
#:package Azure.Messaging.ServiceBus@7.20.2
#:property ManagePackageVersionsCentrally=false

using Azure.Messaging.ServiceBus;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: dotnet run tools/peek-servicebus.cs -- <connection-string> [topic=document-events] [subscription=chunking] [max=50]");
    return 1;
}

var connectionString = args[0];
var topic = args.Length > 1 ? args[1] : "document-events";
var subscription = args.Length > 2 ? args[2] : "chunking";
var max = args.Length > 3 ? int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture) : 50;

await using var client = new ServiceBusClient(connectionString);
await using var receiver = client.CreateReceiver(topic, subscription);

var messages = await receiver.PeekMessagesAsync(max);
Console.WriteLine($"{messages.Count} message(s) on {topic}/{subscription}");

foreach (var message in messages)
{
    Console.WriteLine();
    Console.WriteLine($"{message.EnqueuedTime:u}  seq={message.SequenceNumber}  id={message.MessageId}");
    Console.WriteLine($"  subject      : {message.Subject}");
    Console.WriteLine($"  content-type : {message.ContentType}");
    foreach (var (key, value) in message.ApplicationProperties)
    {
        Console.WriteLine($"  {key,-13}: {value}");
    }

    Console.WriteLine($"  body         : {message.Body}");
}

return 0;
