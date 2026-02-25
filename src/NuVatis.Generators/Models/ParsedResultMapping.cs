#nullable enable

namespace NuVatis.Generators.Models;

public sealed record ParsedResultMapping(
    string Column,
    string Property,
    string? TypeHandler,
    bool IsId
);
