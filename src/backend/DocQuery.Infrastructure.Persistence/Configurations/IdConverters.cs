using DocQuery.Domain.Documents;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DocQuery.Infrastructure.Persistence.Configurations;

internal sealed class DocumentIdConverter() : ValueConverter<DocumentId, Guid>(id => id.Value, value => new DocumentId(value));

internal sealed class DocumentChunkIdConverter() : ValueConverter<DocumentChunkId, Guid>(id => id.Value, value => new DocumentChunkId(value));
