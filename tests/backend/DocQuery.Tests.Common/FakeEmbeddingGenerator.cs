using DocQuery.Domain.Documents;
using Microsoft.Extensions.AI;

namespace DocQuery.Tests.Common;

/// <summary>
/// Deterministic embeddings derived from the input text, with the requested dimensionality. Records every batch;
/// can be scripted to fail or to return the wrong number of vectors.
/// </summary>
public sealed class FakeEmbeddingGenerator(int dimensions = DocumentChunk.EmbeddingDimensions) : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly Queue<Exception> _failures = new();

    public List<IReadOnlyList<string>> Batches { get; } = [];

    /// <summary>When set, only this many vectors are returned regardless of the input count.</summary>
    public int? ReturnCountOverride { get; set; }

    public void FailNextCallWith(Exception exception) => _failures.Enqueue(exception);

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);

        var inputs = values.ToList();
        Batches.Add(inputs);

        if (_failures.TryDequeue(out var failure))
        {
            throw failure;
        }

        var count = ReturnCountOverride ?? inputs.Count;
        var embeddings = inputs.Take(count).Select(text => new Embedding<float>(Vector(text)));

        return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(embeddings));
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType?.IsInstanceOfType(this) == true ? this : null;

    public void Dispose()
    {
    }

    /// <summary>Same text, same vector; different texts, different vectors. Good enough to check plumbing.</summary>
    public float[] Vector(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var vector = new float[dimensions];
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = ((text[i % text.Length] + i) % 97) / 97f;
        }

        return vector;
    }
}
