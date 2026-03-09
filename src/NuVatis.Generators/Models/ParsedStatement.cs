#nullable enable

namespace NuVatis.Generators.Models;

public sealed record ParsedStatement(
    string Id,
    string StatementType,
    string? ResultMapId,
    string? ResultType,
    string? ParameterType,
    ParsedSqlNode RootNode,
    int? Timeout              = null,
    ParsedSelectKey? SelectKey = null
);

public record ParsedSelectKey(
    string  KeyProperty,
    string  Sql,
    string  Order,
    string? ResultType = null
);
