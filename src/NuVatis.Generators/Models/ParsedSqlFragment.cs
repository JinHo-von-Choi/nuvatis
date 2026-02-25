#nullable enable

namespace NuVatis.Generators.Models;

public sealed record ParsedSqlFragment(
    string Id,
    ParsedSqlNode RootNode
);
