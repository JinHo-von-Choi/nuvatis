#nullable enable
using System.Collections.Immutable;

namespace NuVatis.Generators.Models;

public abstract record ParsedSqlNode;

public sealed record TextNode(string Text) : ParsedSqlNode;

public sealed record IfNode(string Test, ImmutableArray<ParsedSqlNode> Children) : ParsedSqlNode;

public sealed record ChooseNode(
    ImmutableArray<WhenClause> Whens,
    ImmutableArray<ParsedSqlNode>? Otherwise
) : ParsedSqlNode;

/** WhenClause는 ParsedSqlNode가 아닌 독립 record. choose/when 전용. */
public sealed record WhenClause(string Test, ImmutableArray<ParsedSqlNode> Children);

public sealed record WhereNode(ImmutableArray<ParsedSqlNode> Children) : ParsedSqlNode;

public sealed record SetNode(ImmutableArray<ParsedSqlNode> Children) : ParsedSqlNode;

public sealed record ForEachNode(
    string Collection,
    string Item,
    string? Open,
    string? Close,
    string? Separator,
    ImmutableArray<ParsedSqlNode> Children
) : ParsedSqlNode;

public sealed record IncludeNode(string RefId) : ParsedSqlNode;

public sealed record ParameterNode(string Name, bool IsStringSubstitution) : ParsedSqlNode;

public sealed record MixedNode(ImmutableArray<ParsedSqlNode> Children) : ParsedSqlNode;
