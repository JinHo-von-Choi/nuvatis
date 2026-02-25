#nullable enable
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using NuVatis.Generators.Models;

namespace NuVatis.Generators.Parsing;

/**
 * ParsedMapper 내 모든 IncludeNode를 해당 SqlFragment 내용으로 치환한다.
 * 빌드타임 전처리 단계로, SG 파이프라인에서 XML 파싱 직후 호출.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public static class IncludeResolver {

    public static ParsedMapper ResolveIncludes(ParsedMapper mapper) {
        var fragments = mapper.SqlFragments
            .ToDictionary(f => f.Id, f => f.RootNode);

        var resolvedStatements = mapper.Statements
            .Select(s => s with {
                RootNode = ResolveNode(s.RootNode, fragments, new HashSet<string>())
            })
            .ToImmutableArray();

        return mapper with { Statements = resolvedStatements };
    }

    private static ParsedSqlNode ResolveNode(
        ParsedSqlNode node,
        Dictionary<string, ParsedSqlNode> fragments,
        HashSet<string> visited) {

        return node switch {
            TextNode        => node,
            ParameterNode   => node,
            IncludeNode inc => ResolveInclude(inc, fragments, visited),
            IfNode n        => n with { Children = ResolveChildren(n.Children, fragments, visited) },
            WhereNode n     => n with { Children = ResolveChildren(n.Children, fragments, visited) },
            SetNode n       => n with { Children = ResolveChildren(n.Children, fragments, visited) },
            ForEachNode n   => n with { Children = ResolveChildren(n.Children, fragments, visited) },
            MixedNode n     => n with { Children = ResolveChildren(n.Children, fragments, visited) },
            ChooseNode n    => ResolveChooseNode(n, fragments, visited),
            _               => node
        };
    }

    private static ChooseNode ResolveChooseNode(
        ChooseNode node,
        Dictionary<string, ParsedSqlNode> fragments,
        HashSet<string> visited) {

        var whens = node.Whens
            .Select(w => new WhenClause(
                w.Test,
                ResolveChildren(w.Children, fragments, visited)
            ))
            .ToImmutableArray();

        var otherwise = node.Otherwise is { Length: > 0 } arr
            ? ResolveChildren(arr, fragments, visited)
            : node.Otherwise;

        return node with { Whens = whens, Otherwise = otherwise };
    }

    private static ImmutableArray<ParsedSqlNode> ResolveChildren(
        ImmutableArray<ParsedSqlNode> nodes,
        Dictionary<string, ParsedSqlNode> fragments,
        HashSet<string> visited) {

        var builder = ImmutableArray.CreateBuilder<ParsedSqlNode>(nodes.Length);
        foreach (var n in nodes) {
            builder.Add(ResolveNode(n, fragments, visited));
        }
        return builder.ToImmutable();
    }

    private static ParsedSqlNode ResolveInclude(
        IncludeNode node,
        Dictionary<string, ParsedSqlNode> fragments,
        HashSet<string> visited) {

        if (!fragments.TryGetValue(node.RefId, out var fragmentNode)) {
            return new TextNode($"/* UNRESOLVED: {node.RefId} */");
        }

        if (!visited.Add(node.RefId)) {
            return new TextNode($"/* CIRCULAR: {node.RefId} */");
        }

        try {
            return ResolveNode(fragmentNode, fragments, visited);
        } finally {
            visited.Remove(node.RefId);
        }
    }
}
