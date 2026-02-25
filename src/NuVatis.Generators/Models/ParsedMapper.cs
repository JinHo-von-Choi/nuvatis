#nullable enable
using System.Collections.Immutable;

namespace NuVatis.Generators.Models;

public sealed record ParsedMapper(
    string Namespace,
    ImmutableArray<ParsedResultMap> ResultMaps,
    ImmutableArray<ParsedStatement> Statements,
    ImmutableArray<ParsedSqlFragment> SqlFragments
);
