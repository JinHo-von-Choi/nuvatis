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

/**
 * <bind> 태그. name에 value 표현식의 결과를 바인딩한다.
 * SG에서는 로컬 변수 선언 코드로 변환된다.
 */
public sealed record BindNode(string Name, string Value) : ParsedSqlNode;

public sealed record ParameterNode(string Name, bool IsStringSubstitution) : ParsedSqlNode;

public sealed record MixedNode(ImmutableArray<ParsedSqlNode> Children) : ParsedSqlNode;
