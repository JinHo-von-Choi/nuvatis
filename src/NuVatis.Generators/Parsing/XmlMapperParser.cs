#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;
using NuVatis.Generators.Models;

namespace NuVatis.Generators.Parsing;

/**
 * Mapper XML 파서. SG 빌드타임에 XML을 ParsedMapper 모델로 변환한다.
 * 동적 SQL 태그를 재귀적으로 파싱하여 SQL 노드 트리를 구성한다.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public static class XmlMapperParser {
    private static readonly Regex ParamRegex = new Regex(@"([#$])\{([^}]+)\}", RegexOptions.Compiled);

    public static ParsedMapper Parse(string xmlContent, CancellationToken ct) {
        var doc  = XDocument.Parse(xmlContent);
        var root = doc.Element("mapper")
            ?? throw new InvalidOperationException("Root element <mapper> not found.");

        var ns = root.Attribute("namespace")?.Value
            ?? throw new InvalidOperationException("Attribute 'namespace' is missing in <mapper>.");

        var resultMaps   = ImmutableArray.CreateBuilder<ParsedResultMap>();
        var statements   = ImmutableArray.CreateBuilder<ParsedStatement>();
        var sqlFragments = ImmutableArray.CreateBuilder<ParsedSqlFragment>();

        foreach (var element in root.Elements()) {
            ct.ThrowIfCancellationRequested();

            switch (element.Name.LocalName) {
                case "resultMap":
                    resultMaps.Add(ParseResultMap(element));
                    break;
                case "sql":
                    sqlFragments.Add(ParseSqlFragment(element));
                    break;
                case "select":
                case "insert":
                case "update":
                case "delete":
                    statements.Add(ParseStatement(element));
                    break;
            }
        }

        return new ParsedMapper(
            ns,
            resultMaps.ToImmutable(),
            statements.ToImmutable(),
            sqlFragments.ToImmutable()
        );
    }

    private static ParsedResultMap ParseResultMap(XElement element) {
        var id   = element.Attribute("id")?.Value
            ?? throw new InvalidOperationException("Attribute 'id' is missing in <resultMap>.");
        var type = element.Attribute("type")?.Value
            ?? throw new InvalidOperationException("Attribute 'type' is missing in <resultMap>.");
        var extends_ = element.Attribute("extends")?.Value;

        var mappings     = ImmutableArray.CreateBuilder<ParsedResultMapping>();
        var associations = ImmutableArray.CreateBuilder<ParsedAssociation>();
        var collections  = ImmutableArray.CreateBuilder<ParsedCollection>();

        foreach (var child in element.Elements()) {
            switch (child.Name.LocalName) {
                case "id":
                    mappings.Add(new ParsedResultMapping(
                        Column:      RequireAttr(child, "column"),
                        Property:    RequireAttr(child, "property"),
                        TypeHandler: child.Attribute("typeHandler")?.Value,
                        IsId:        true
                    ));
                    break;
                case "result":
                    mappings.Add(new ParsedResultMapping(
                        Column:      RequireAttr(child, "column"),
                        Property:    RequireAttr(child, "property"),
                        TypeHandler: child.Attribute("typeHandler")?.Value,
                        IsId:        false
                    ));
                    break;
                case "association":
                    associations.Add(new ParsedAssociation(
                        Property:     RequireAttr(child, "property"),
                        ResultMapId:  child.Attribute("resultMap")?.Value,
                        ColumnPrefix: child.Attribute("columnPrefix")?.Value,
                        Select:       child.Attribute("select")?.Value,
                        Column:       child.Attribute("column")?.Value
                    ));
                    break;
                case "collection":
                    collections.Add(new ParsedCollection(
                        Property:     RequireAttr(child, "property"),
                        ResultMapId:  child.Attribute("resultMap")?.Value,
                        OfType:       child.Attribute("ofType")?.Value,
                        ColumnPrefix: child.Attribute("columnPrefix")?.Value,
                        Select:       child.Attribute("select")?.Value,
                        Column:       child.Attribute("column")?.Value
                    ));
                    break;
            }
        }

        return new ParsedResultMap(
            id, type, extends_,
            mappings.ToImmutable(),
            associations.ToImmutable(),
            collections.ToImmutable()
        );
    }

    private static ParsedStatement ParseStatement(XElement element) {
        int? timeout     = null;
        var timeoutAttr = element.Attribute("timeout")?.Value;
        if (timeoutAttr is not null && int.TryParse(timeoutAttr, out var parsed)) {
            timeout = parsed;
        }

        return new ParsedStatement(
            Id:            RequireAttr(element, "id"),
            StatementType: CapitalizeFirst(element.Name.LocalName),
            ResultMapId:   element.Attribute("resultMap")?.Value,
            ResultType:    element.Attribute("resultType")?.Value,
            ParameterType: element.Attribute("parameterType")?.Value,
            RootNode:      ParseChildNodes(element),
            Timeout:       timeout
        );
    }

    private static ParsedSqlFragment ParseSqlFragment(XElement element) {
        return new ParsedSqlFragment(
            Id:       RequireAttr(element, "id"),
            RootNode: ParseChildNodes(element)
        );
    }

    private static ParsedSqlNode ParseChildNodes(XElement element) {
        var nodes = ImmutableArray.CreateBuilder<ParsedSqlNode>();

        foreach (var node in element.Nodes()) {
            if (node is XText text) {
                nodes.AddRange(ParseTextWithParameters(text.Value));
            } else if (node is XElement child) {
                nodes.Add(ParseElementNode(child));
            }
        }

        if (nodes.Count == 0) return new TextNode("");
        if (nodes.Count == 1) return nodes[0];
        return new MixedNode(nodes.ToImmutable());
    }

    private static IEnumerable<ParsedSqlNode> ParseTextWithParameters(string text) {
        var lastIndex = 0;
        var matches   = ParamRegex.Matches(text);

        foreach (Match match in matches) {
            if (match.Index > lastIndex) {
                yield return new TextNode(text.Substring(lastIndex, match.Index - lastIndex));
            }

            var paramName        = match.Groups[2].Value.Trim();
            var isStringSubst    = match.Groups[1].Value == "$";
            yield return new ParameterNode(paramName, isStringSubst);

            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < text.Length) {
            yield return new TextNode(text.Substring(lastIndex));
        }
    }

    private static ParsedSqlNode ParseElementNode(XElement element) {
        switch (element.Name.LocalName) {
            case "if":
                return new IfNode(
                    Test:     RequireAttr(element, "test"),
                    Children: CollectChildren(element)
                );

            case "choose":
                var whens = element.Elements("when")
                    .Select(w => new WhenClause(
                        Test:     RequireAttr(w, "test"),
                        Children: CollectChildren(w)
                    ))
                    .ToImmutableArray();

                var otherwise    = element.Element("otherwise");
                var otherwiseArr = otherwise != null ? CollectChildren(otherwise) : (ImmutableArray<ParsedSqlNode>?)null;

                return new ChooseNode(whens, otherwiseArr);

            case "where":
                return new WhereNode(CollectChildren(element));

            case "set":
                return new SetNode(CollectChildren(element));

            case "foreach":
                return new ForEachNode(
                    Collection: RequireAttr(element, "collection"),
                    Item:       RequireAttr(element, "item"),
                    Open:       element.Attribute("open")?.Value,
                    Close:      element.Attribute("close")?.Value,
                    Separator:  element.Attribute("separator")?.Value,
                    Children:   CollectChildren(element)
                );

            case "include":
                return new IncludeNode(RequireAttr(element, "refid"));

            case "bind":
                return new BindNode(
                    Name:  RequireAttr(element, "name"),
                    Value: RequireAttr(element, "value")
                );

            default:
                return ParseChildNodes(element);
        }
    }

    private static ImmutableArray<ParsedSqlNode> CollectChildren(XElement element) {
        var nodes = ImmutableArray.CreateBuilder<ParsedSqlNode>();

        foreach (var node in element.Nodes()) {
            if (node is XText text) {
                nodes.AddRange(ParseTextWithParameters(text.Value));
            } else if (node is XElement child) {
                nodes.Add(ParseElementNode(child));
            }
        }

        return nodes.ToImmutable();
    }

    private static string RequireAttr(XElement element, string name) {
        return element.Attribute(name)?.Value
            ?? throw new InvalidOperationException(
                $"Attribute '{name}' is missing in <{element.Name.LocalName}>.");
    }

    private static string CapitalizeFirst(string s) {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
