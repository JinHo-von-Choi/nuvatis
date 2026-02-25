#nullable enable

namespace NuVatis.Generators.Models;

public sealed record ParsedAssociation(
    string Property,
    string? ResultMapId,
    string? ColumnPrefix,
    string? Select,
    string? Column
);
