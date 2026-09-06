namespace DocQuery.Application.Abstractions;

/// <summary>Handles one query type on the read side. Endpoints depend on this, never on a concrete handler (ADR 0009).</summary>
public interface IQueryHandler<in TQuery, TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
