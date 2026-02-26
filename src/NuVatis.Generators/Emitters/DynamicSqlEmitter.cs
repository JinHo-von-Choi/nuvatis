#nullable enable
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NuVatis.Generators.Models;

namespace NuVatis.Generators.Emitters;

/**
 * 동적 SQL 노드 트리를 C# 코드로 변환하는 Emitter.
 *
 * MyBatis test 표현식을 C# 조건식으로 변환하고,
 * ParsedSqlNode 트리를 StringBuilder 기반 SQL 빌드 코드로 방출한다.
 * 생성된 코드는 런타임 리플렉션 없이 직접 프로퍼티에 접근한다.
 *
 * @author 최진호
 * @date   2026-02-26
 */
public static class DynamicSqlEmitter {

    /**
     * ParsedSqlNode 트리에 동적 SQL 태그(if, choose, where, set, foreach)가
     * 포함되어 있는지 검사한다.
     */
    public static bool HasDynamicNodes(ParsedSqlNode node) {
        return node switch {
            IfNode      => true,
            ChooseNode  => true,
            WhereNode   => true,
            SetNode     => true,
            ForEachNode => true,
            MixedNode m => m.Children.Any(HasDynamicNodes),
            _           => false
        };
    }

    /**
     * ParsedSqlNode 트리를 C# StringBuilder 코드로 변환한다.
     * 생성된 코드 블록은 paramVarName으로 지정된 파라미터 변수를 참조한다.
     *
     * @param node SQL 노드 트리 루트
     * @param paramVarName 파라미터 변수 C# 이름 (예: "param")
     * @param paramTypeName 파라미터 타입의 전체 이름 (예: "MyApp.UserSearchParam")
     * @param indent 들여쓰기 깊이
     * @return C# 코드 문자열
     */
    public static string EmitSqlBuilder(
        ParsedSqlNode node,
        string paramVarName,
        string? paramTypeName,
        int indent = 3) {

        var sb = new StringBuilder(1024);
        sb.AppendLine(Indent(indent) + "var __sql = new System.Text.StringBuilder(256);");
        EmitNode(sb, node, paramVarName, paramTypeName, indent);
        return sb.ToString();
    }

    /**
     * MyBatis test 표현식을 C# 조건식으로 변환한다.
     *
     * 변환 규칙:
     *   and → &&, or → ||
     *   name != null → {paramVar}.Name != null
     *   age > 18 → {paramVar}.Age > 18
     *   type == 'admin' → {paramVar}.Type == "admin"
     *   list.size > 0 → {paramVar}.List?.Count > 0
     *
     * @param testExpr MyBatis test 표현식
     * @param paramVar C# 파라미터 변수명
     * @return C# 조건식 문자열
     */
    public static string ConvertTestExpression(string testExpr, string paramVar) {
        var result = testExpr;

        result = Regex.Replace(result, @"\band\b", "&&");
        result = Regex.Replace(result, @"\bor\b", "||");

        result = Regex.Replace(result, @"'([^']*)'", "\"$1\"");

        result = Regex.Replace(result,
            @"(\w+(?:\.\w+)*)\s*(!=|==|>=|<=|>|<)\s*",
            m => {
                var prop = ConvertPropertyAccess(m.Groups[1].Value, paramVar);
                var op   = m.Groups[2].Value;
                return $"{prop} {op} ";
            });

        result = Regex.Replace(result,
            @"(?<=&&\s*|^\s*|\|\|\s*)(\w+(?:\.\w+)*)(?=\s*$|\s*&&|\s*\|\|)",
            m => ConvertPropertyAccess(m.Groups[1].Value, paramVar));

        return result;
    }

    private static void EmitNode(
        StringBuilder sb,
        ParsedSqlNode node,
        string paramVar,
        string? paramType,
        int indent) {

        switch (node) {
            case TextNode text:
                if (!string.IsNullOrWhiteSpace(text.Text)) {
                    sb.AppendLine(Indent(indent) + $"__sql.Append(\"{EscapeString(text.Text)}\");");
                }
                break;

            case ParameterNode param:
                if (param.IsStringSubstitution) {
                    var prop = ToPascalCase(param.Name);
                    sb.AppendLine(Indent(indent) + $"__sql.Append({paramVar}.{prop});");
                } else {
                    sb.AppendLine(Indent(indent) + $"__sql.Append(\"#{{{param.Name}}}\");");
                }
                break;

            case IfNode ifNode:
                var cond = ConvertTestExpression(ifNode.Test, paramVar);
                sb.AppendLine(Indent(indent) + $"if ({cond})");
                sb.AppendLine(Indent(indent) + "{");
                foreach (var child in ifNode.Children) {
                    EmitNode(sb, child, paramVar, paramType, indent + 1);
                }
                sb.AppendLine(Indent(indent) + "}");
                break;

            case ChooseNode choose:
                for (var i = 0; i < choose.Whens.Length; i++) {
                    var when    = choose.Whens[i];
                    var whenCond = ConvertTestExpression(when.Test, paramVar);
                    var prefix  = i == 0 ? "if" : "else if";
                    sb.AppendLine(Indent(indent) + $"{prefix} ({whenCond})");
                    sb.AppendLine(Indent(indent) + "{");
                    foreach (var child in when.Children) {
                        EmitNode(sb, child, paramVar, paramType, indent + 1);
                    }
                    sb.AppendLine(Indent(indent) + "}");
                }
                if (choose.Otherwise is { } otherwise) {
                    sb.AppendLine(Indent(indent) + "else");
                    sb.AppendLine(Indent(indent) + "{");
                    foreach (var child in otherwise) {
                        EmitNode(sb, child, paramVar, paramType, indent + 1);
                    }
                    sb.AppendLine(Indent(indent) + "}");
                }
                break;

            case WhereNode where:
                sb.AppendLine(Indent(indent) + "var __whereBuilder = new System.Text.StringBuilder();");
                foreach (var child in where.Children) {
                    EmitNode(sb, child, paramVar, paramType, indent);
                }
                sb.AppendLine(Indent(indent) + "var __whereClause = __whereBuilder.ToString().TrimStart();");
                sb.AppendLine(Indent(indent) + "if (__whereClause.Length > 0)");
                sb.AppendLine(Indent(indent) + "{");
                sb.AppendLine(Indent(indent + 1) + "if (__whereClause.StartsWith(\"AND \", StringComparison.OrdinalIgnoreCase))");
                sb.AppendLine(Indent(indent + 2) + "__whereClause = __whereClause.Substring(4);");
                sb.AppendLine(Indent(indent + 1) + "else if (__whereClause.StartsWith(\"OR \", StringComparison.OrdinalIgnoreCase))");
                sb.AppendLine(Indent(indent + 2) + "__whereClause = __whereClause.Substring(3);");
                sb.AppendLine(Indent(indent + 1) + "__sql.Append(\" WHERE \").Append(__whereClause);");
                sb.AppendLine(Indent(indent) + "}");
                break;

            case SetNode set:
                sb.AppendLine(Indent(indent) + "var __setBuilder = new System.Text.StringBuilder();");
                foreach (var child in set.Children) {
                    EmitNode(sb, child, paramVar, paramType, indent);
                }
                sb.AppendLine(Indent(indent) + "var __setClause = __setBuilder.ToString().TrimEnd().TrimEnd(',');");
                sb.AppendLine(Indent(indent) + "if (__setClause.Length > 0)");
                sb.AppendLine(Indent(indent) + "{");
                sb.AppendLine(Indent(indent + 1) + "__sql.Append(\" SET \").Append(__setClause);");
                sb.AppendLine(Indent(indent) + "}");
                break;

            case ForEachNode forEach:
                var collProp = ToPascalCase(forEach.Collection);
                var itemVar  = forEach.Item;
                if (forEach.Open is not null) {
                    sb.AppendLine(Indent(indent) + $"__sql.Append(\"{EscapeString(forEach.Open)}\");");
                }
                sb.AppendLine(Indent(indent) + $"var __idx = 0;");
                sb.AppendLine(Indent(indent) + $"foreach (var {itemVar} in {paramVar}.{collProp})");
                sb.AppendLine(Indent(indent) + "{");
                if (forEach.Separator is not null) {
                    sb.AppendLine(Indent(indent + 1) + $"if (__idx > 0) __sql.Append(\"{EscapeString(forEach.Separator)}\");");
                }
                foreach (var child in forEach.Children) {
                    EmitNode(sb, child, paramVar, paramType, indent + 1);
                }
                sb.AppendLine(Indent(indent + 1) + "__idx++;");
                sb.AppendLine(Indent(indent) + "}");
                if (forEach.Close is not null) {
                    sb.AppendLine(Indent(indent) + $"__sql.Append(\"{EscapeString(forEach.Close)}\");");
                }
                break;

            case MixedNode mixed:
                foreach (var child in mixed.Children) {
                    EmitNode(sb, child, paramVar, paramType, indent);
                }
                break;

            case BindNode bind:
                var bindExpr = ConvertBindExpression(bind.Value, paramVar);
                sb.AppendLine(Indent(indent) + $"var {bind.Name} = {bindExpr};");
                break;

            case IncludeNode:
                break;
        }
    }

    /**
     * <bind> 태그의 value 표현식을 C# 코드로 변환한다.
     * 예: "'%' + name + '%'" → "\"%" + paramVar.Name + "%\""
     */
    private static string ConvertBindExpression(string valueExpr, string paramVar) {
        var result = valueExpr;

        result = Regex.Replace(result, @"'([^']*)'", "\"$1\"");

        result = Regex.Replace(result, @"(?<!["".\w])([a-zA-Z_]\w*)(?!\s*\(|["".\w])",
            m => {
                var name = m.Groups[1].Value;
                if (name == "null" || name == "true" || name == "false") return name;
                return $"{paramVar}.{ToPascalCase(name)}";
            });

        return result;
    }

    private static string ConvertPropertyAccess(string path, string paramVar) {
        if (path == "null" || path == "true" || path == "false") return path;

        if (int.TryParse(path, out _) || decimal.TryParse(path, out _)) return path;

        var parts     = path.Split('.');
        var converted = new StringBuilder();
        converted.Append(paramVar);

        foreach (var part in parts) {
            var name = part switch {
                "size"   => "Count",
                "length" => "Length",
                _        => ToPascalCase(part)
            };
            converted.Append("?.").Append(name);
        }

        var result = converted.ToString();
        if (result.Contains("?.")) {
            result = paramVar + "." + result.Substring(paramVar.Length + 2);
        }

        return result;
    }

    private static string ToPascalCase(string name) {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToUpperInvariant(name[0]) + name.Substring(1);
    }

    private static string EscapeString(string s) {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }

    private static string Indent(int level) {
        return new string(' ', level * 4);
    }
}
