#nullable enable

namespace NuVatis.Generators.Models;

public sealed record ParsedStatement(
    string Id,
    string StatementType,
    string? ResultMapId,
    string? ResultType,
    string? ParameterType,
    ParsedSqlNode RootNode,
    int? Timeout = null
);
