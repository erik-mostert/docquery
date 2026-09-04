namespace DocQuery.Application.Abstractions;

/// <summary>Handles one command type. Endpoints depend on this, never on a concrete handler.</summary>
public interface ICommandHandler<in TCommand, TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
