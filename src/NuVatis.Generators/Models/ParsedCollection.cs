#nullable enable

namespace NuVatis.Generators.Models;

public sealed record ParsedCollection(
    string Property,
    string? ResultMapId,
    string? OfType,
    string? ColumnPrefix,
    string? Select,
    string? Column
);
