using System.Text.Json;
using Azure.Messaging.ServiceBus;
using DocQuery.Application.Exceptions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocQuery.Infrastructure.Messaging;

/// <summary>
/// Receives from one topic subscription with a <see cref="ServiceBusProcessor"/> and settles each message
/// explicitly (ADR 0015): handled or unknown subject → complete; permanent failure or unreadable body →
/// dead-letter with a reason; anything else → abandon, so Service Bus redelivers until MaxDeliveryCount.
/// </summary>
internal sealed class ServiceBusSubscriptionListener(
    ServiceBusClient client,
    MessageDispatcher dispatcher,
    IOptions<MessagingOptions> messaging,
    IOptions<SubscriptionOptions> subscription,
    ILogger<ServiceBusSubscriptionListener> logger) : IHostedService, IAsyncDisposable
{
    private const int MaxDeadLetterDescriptionLength = 4096;

    private ServiceBusProcessor? _processor;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var options = subscription.Value;
        var topic = messaging.Value.TopicName;

        _processor = client.CreateProcessor(topic, options.Name, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = options.MaxConcurrentCalls,
            PrefetchCount = options.PrefetchCount,
            MaxAutoLockRenewalDuration = options.MaxAutoLockRenewalDuration,
        });

        _processor.ProcessMessageAsync += OnMessageAsync;
        _processor.ProcessErrorAsync += OnErrorAsync;

        await _processor.StartProcessingAsync(cancellationToken);
        MessagingLog.ListenerStarted(logger, topic, options.Name, options.MaxConcurrentCalls);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor is not null)
        {
            await _processor.StopProcessingAsync(cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_processor is not null)
        {
            await _processor.DisposeAsync();
        }
    }

    private async Task OnMessageAsync(ProcessMessageEventArgs args)
    {
        var message = args.Message;
        var cancellationToken = args.CancellationToken;

        try
        {
            var handled = await dispatcher.DispatchAsync(message.Subject, message.Body, cancellationToken);
            if (handled)
            {
                MessagingLog.MessageHandled(logger, message.MessageId, message.Subject);
            }
            else
            {
                MessagingLog.UnknownSubject(logger, message.MessageId, message.Subject);
            }

            await args.CompleteMessageAsync(message, cancellationToken);
        }
        catch (PermanentProcessingException exception)
        {
            await DeadLetterAsync(args, nameof(PermanentProcessingException), exception.Message, cancellationToken);
        }
        catch (JsonException exception)
        {
            await DeadLetterAsync(args, nameof(JsonException), exception.Message, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down: leave the message; its lock expires and it is redelivered.
        }
        catch (Exception exception)
        {
            MessagingLog.TransientFailure(logger, exception, message.MessageId, message.Subject, message.DeliveryCount);
            await SettleAsync(() => args.AbandonMessageAsync(message, cancellationToken: cancellationToken), message.MessageId);
        }
    }

    private async Task DeadLetterAsync(ProcessMessageEventArgs args, string reason, string description, CancellationToken cancellationToken)
    {
        var message = args.Message;
        var truncated = description.Length <= MaxDeadLetterDescriptionLength ? description : description[..MaxDeadLetterDescriptionLength];

        MessagingLog.DeadLettered(logger, message.MessageId, message.Subject, reason, truncated);
        await SettleAsync(() => args.DeadLetterMessageAsync(message, reason, truncated, cancellationToken), message.MessageId);
    }

    private async Task SettleAsync(Func<Task> settle, string messageId)
    {
        try
        {
            await settle();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MessagingLog.SettlementFailed(logger, exception, messageId);
        }
    }

    private Task OnErrorAsync(ProcessErrorEventArgs args)
    {
        MessagingLog.ProcessorError(logger, args.Exception, args.ErrorSource.ToString(), args.EntityPath);
        return Task.CompletedTask;
    }
}
