#nullable enable
using System.Collections.Immutable;
using System.Text;
using NuVatis.Generators.Models;

namespace NuVatis.Generators.Emitters;

/**
 * ParsedSqlNode 트리를 순회하여 런타임 SQL 빌드 메서드 코드를 생성한다.
 * 동적 SQL 태그(if, where, set, foreach, choose 등)에 대응하는
 * C# 코드를 방출한다.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public static class ParameterEmitter {

    public static string EmitBuildSqlMethod(ParsedStatement statement, string providerParameterPrefix) {
        var sb = new StringBuilder(2048);

        sb.AppendLine($"        static (string sql, System.Collections.Generic.List<System.Data.Common.DbParameter> parameters) BuildSql_{SanitizeId(statement.Id)}(object? param, System.Data.Common.DbProviderFactory dbFactory)");
        sb.AppendLine("        {");
        sb.AppendLine("            var sb = new System.Text.StringBuilder();");
        sb.AppendLine("            var parameters = new System.Collections.Generic.List<System.Data.Common.DbParameter>();");
        sb.AppendLine("            var paramIndex = 0;");

        EmitNode(sb, statement.RootNode, providerParameterPrefix, 3);

        sb.AppendLine("            return (sb.ToString(), parameters);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        static object? GetPropertyValue(object? obj, string propertyName)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (obj == null) return null;");
        sb.AppendLine("            var prop = obj.GetType().GetProperty(propertyName);");
        sb.AppendLine("            return prop?.GetValue(obj);");
        sb.AppendLine("        }");

        return sb.ToString();
    }

    private static void EmitNode(StringBuilder sb, ParsedSqlNode node, string prefix, int indent) {
        var sp = new string(' ', indent * 4);

        switch (node) {
            case TextNode textNode:
                EmitTextNode(sb, textNode, sp);
                break;

            case ParameterNode paramNode:
                EmitParameterNode(sb, paramNode, prefix, sp);
                break;

            case IfNode ifNode:
                EmitIfNode(sb, ifNode, prefix, indent);
                break;

            case ChooseNode chooseNode:
                EmitChooseNode(sb, chooseNode, prefix, indent);
                break;

            case WhereNode whereNode:
                EmitWhereNode(sb, whereNode, prefix, indent);
                break;

            case SetNode setNode:
                EmitSetNode(sb, setNode, prefix, indent);
                break;

            case ForEachNode forEachNode:
                EmitForEachNode(sb, forEachNode, prefix, indent);
                break;

            case MixedNode mixedNode:
                foreach (var child in mixedNode.Children) {
                    EmitNode(sb, child, prefix, indent);
                }
                break;

            case IncludeNode:
                sb.AppendLine($"{sp}// <include> resolved at parse-time");
                break;
        }
    }

    private static void EmitTextNode(StringBuilder sb, TextNode textNode, string sp) {
        var escaped = textNode.Text.Replace("\"", "\"\"");
        sb.AppendLine($"{sp}sb.Append(@\"{escaped}\");");
    }

    private static void EmitParameterNode(StringBuilder sb, ParameterNode paramNode, string prefix, string sp) {
        if (paramNode.IsStringSubstitution) {
            sb.AppendLine($"{sp}sb.Append(GetPropertyValue(param, \"{paramNode.Name}\")?.ToString() ?? \"\");");
        } else {
            sb.AppendLine($"{sp}{{");
            sb.AppendLine($"{sp}    var pName = \"{prefix}p\" + paramIndex++;");
            sb.AppendLine($"{sp}    sb.Append(pName);");
            sb.AppendLine($"{sp}    var p = dbFactory.CreateParameter();");
            sb.AppendLine($"{sp}    p.ParameterName = pName;");
            sb.AppendLine($"{sp}    p.Value = GetPropertyValue(param, \"{paramNode.Name}\") ?? System.DBNull.Value;");
            sb.AppendLine($"{sp}    parameters.Add(p);");
            sb.AppendLine($"{sp}}}");
        }
    }

    private static void EmitIfNode(StringBuilder sb, IfNode ifNode, string prefix, int indent) {
        var sp = new string(' ', indent * 4);
        sb.AppendLine($"{sp}if (GetPropertyValue(param, \"{ExtractPropertyName(ifNode.Test)}\") != null)");
        sb.AppendLine($"{sp}{{");
        foreach (var child in ifNode.Children) {
            EmitNode(sb, child, prefix, indent + 1);
        }
        sb.AppendLine($"{sp}}}");
    }

    private static void EmitChooseNode(StringBuilder sb, ChooseNode chooseNode, string prefix, int indent) {
        var sp = new string(' ', indent * 4);
        var first = true;

        foreach (var when in chooseNode.Whens) {
            var keyword = first ? "if" : "else if";
            sb.AppendLine($"{sp}{keyword} (GetPropertyValue(param, \"{ExtractPropertyName(when.Test)}\") != null)");
            sb.AppendLine($"{sp}{{");
            foreach (var child in when.Children) {
                EmitNode(sb, child, prefix, indent + 1);
            }
            sb.AppendLine($"{sp}}}");
            first = false;
        }

        if (chooseNode.Otherwise is { Length: > 0 } otherwise) {
            sb.AppendLine($"{sp}else");
            sb.AppendLine($"{sp}{{");
            foreach (var child in otherwise) {
                EmitNode(sb, child, prefix, indent + 1);
            }
            sb.AppendLine($"{sp}}}");
        }
    }

    private static void EmitWhereNode(StringBuilder sb, WhereNode whereNode, string prefix, int indent) {
        var sp = new string(' ', indent * 4);
        sb.AppendLine($"{sp}{{");
        sb.AppendLine($"{sp}    var whereSb = new System.Text.StringBuilder();");
        sb.AppendLine($"{sp}    var outerSb = sb;");
        sb.AppendLine($"{sp}    sb = whereSb;");

        foreach (var child in whereNode.Children) {
            EmitNode(sb, child, prefix, indent + 1);
        }

        sb.AppendLine($"{sp}    sb = outerSb;");
        sb.AppendLine($"{sp}    var whereContent = whereSb.ToString().Trim();");
        sb.AppendLine($"{sp}    if (!string.IsNullOrEmpty(whereContent))");
        sb.AppendLine($"{sp}    {{");
        sb.AppendLine($"{sp}        if (whereContent.StartsWith(\"AND \", System.StringComparison.OrdinalIgnoreCase))");
        sb.AppendLine($"{sp}            whereContent = whereContent.Substring(4);");
        sb.AppendLine($"{sp}        else if (whereContent.StartsWith(\"OR \", System.StringComparison.OrdinalIgnoreCase))");
        sb.AppendLine($"{sp}            whereContent = whereContent.Substring(3);");
        sb.AppendLine($"{sp}        sb.Append(\" WHERE \").Append(whereContent);");
        sb.AppendLine($"{sp}    }}");
        sb.AppendLine($"{sp}}}");
    }

    private static void EmitSetNode(StringBuilder sb, SetNode setNode, string prefix, int indent) {
        var sp = new string(' ', indent * 4);
        sb.AppendLine($"{sp}{{");
        sb.AppendLine($"{sp}    var setSb = new System.Text.StringBuilder();");
        sb.AppendLine($"{sp}    var outerSb = sb;");
        sb.AppendLine($"{sp}    sb = setSb;");

        foreach (var child in setNode.Children) {
            EmitNode(sb, child, prefix, indent + 1);
        }

        sb.AppendLine($"{sp}    sb = outerSb;");
        sb.AppendLine($"{sp}    var setContent = setSb.ToString().Trim();");
        sb.AppendLine($"{sp}    if (!string.IsNullOrEmpty(setContent))");
        sb.AppendLine($"{sp}    {{");
        sb.AppendLine($"{sp}        if (setContent.EndsWith(\",\"))");
        sb.AppendLine($"{sp}            setContent = setContent.Substring(0, setContent.Length - 1);");
        sb.AppendLine($"{sp}        sb.Append(\" SET \").Append(setContent);");
        sb.AppendLine($"{sp}    }}");
        sb.AppendLine($"{sp}}}");
    }

    private static void EmitForEachNode(StringBuilder sb, ForEachNode forEachNode, string prefix, int indent) {
        var sp   = new string(' ', indent * 4);
        var coll = SanitizeId(forEachNode.Collection);

        sb.AppendLine($"{sp}{{");
        sb.AppendLine($"{sp}    var coll_{coll} = GetPropertyValue(param, \"{forEachNode.Collection}\") as System.Collections.IEnumerable;");
        sb.AppendLine($"{sp}    if (coll_{coll} != null)");
        sb.AppendLine($"{sp}    {{");
        if (forEachNode.Open is not null) {
            sb.AppendLine($"{sp}        sb.Append(\"{Escape(forEachNode.Open)}\");");
        }
        sb.AppendLine($"{sp}        var isFirst = true;");
        sb.AppendLine($"{sp}        foreach (var {forEachNode.Item} in coll_{coll})");
        sb.AppendLine($"{sp}        {{");
        if (forEachNode.Separator is not null) {
            sb.AppendLine($"{sp}            if (!isFirst) sb.Append(\"{Escape(forEachNode.Separator)}\");");
        }
        sb.AppendLine($"{sp}            isFirst = false;");

        foreach (var child in forEachNode.Children) {
            EmitForEachChildNode(sb, child, forEachNode.Item, prefix, indent + 3);
        }

        sb.AppendLine($"{sp}        }}");
        if (forEachNode.Close is not null) {
            sb.AppendLine($"{sp}        sb.Append(\"{Escape(forEachNode.Close)}\");");
        }
        sb.AppendLine($"{sp}    }}");
        sb.AppendLine($"{sp}}}");
    }

    /**
     * ForEach 내부 노드에서 ParameterNode는 컬렉션 아이템 자체를 바인딩해야 한다.
     */
    private static void EmitForEachChildNode(
        StringBuilder sb, ParsedSqlNode node, string itemVar, string prefix, int indent) {

        var sp = new string(' ', indent * 4);

        if (node is TextNode textNode) {
            var escaped = textNode.Text.Replace("\"", "\"\"");
            sb.AppendLine($"{sp}sb.Append(@\"{escaped}\");");
        } else if (node is ParameterNode paramNode && !paramNode.IsStringSubstitution) {
            sb.AppendLine($"{sp}{{");
            sb.AppendLine($"{sp}    var pName = \"{prefix}p\" + paramIndex++;");
            sb.AppendLine($"{sp}    sb.Append(pName);");
            sb.AppendLine($"{sp}    var p = dbFactory.CreateParameter();");
            sb.AppendLine($"{sp}    p.ParameterName = pName;");
            sb.AppendLine($"{sp}    p.Value = {itemVar} ?? (object)System.DBNull.Value;");
            sb.AppendLine($"{sp}    parameters.Add(p);");
            sb.AppendLine($"{sp}}}");
        } else if (node is ParameterNode strSubNode && strSubNode.IsStringSubstitution) {
            sb.AppendLine($"{sp}sb.Append({itemVar}?.ToString() ?? \"\");");
        } else {
            EmitNode(sb, node, prefix, indent);
        }
    }

    /**
     * MyBatis test 표현식에서 프로퍼티명을 추출한다.
     * "name != null" -> "name", "age > 0" -> "age"
     */
    private static string ExtractPropertyName(string testExpression) {
        var trimmed = testExpression.Trim();
        var spaceIdx = trimmed.IndexOf(' ');
        return spaceIdx > 0 ? trimmed.Substring(0, spaceIdx) : trimmed;
    }

    private static string SanitizeId(string id) {
        return id.Replace(".", "_").Replace("-", "_");
    }

    private static string Escape(string s) {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
