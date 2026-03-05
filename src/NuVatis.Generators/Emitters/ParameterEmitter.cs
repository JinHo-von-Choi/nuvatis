#nullable enable
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using NuVatis.Generators.Models;

namespace NuVatis.Generators.Emitters;

/**
 * ParsedSqlNode 트리를 순회하여 런타임 SQL 빌드 메서드 코드를 생성한다.
 * 동적 SQL 태그(if, where, set, foreach, choose 등)에 대응하는
 * C# 코드를 방출한다.
 *
 * paramTypeMap을 통해 각 파라미터의 CLR 타입명을 수신하면,
 * ${} 문자열 치환 경로에서 SqlIdentifier 여부를 판별한다.
 *   - SqlIdentifier 타입: ToString() 직접 호출 (생성 시점에 이미 검증 완료)
 *   - 그 외 타입 또는 정보 없음: InvalidOperationException 런타임 가드 삽입
 *
 * @author 최진호
 * @date   2026-02-24
 * @modified 2026-02-28 SqlIdentifier 타입 가드 통합
 */
public static class ParameterEmitter {

    /**
     * ${} 문자열 치환 경로에서 SqlIdentifier로 인정하는 유일한 FQN.
     * EndsWith 방식은 MySqlIdentifier 같은 유사 타입명을 오인할 수 있어 정확 일치만 허용한다.
     */
    private const string SqlIdentifierFqn = "NuVatis.Core.Sql.SqlIdentifier";

    /**
     * BuildSql_{id} 로컬 함수 소스를 생성한다.
     *
     * @param statement              파싱된 SQL 구문 정보
     * @param providerParameterPrefix DB 파라미터 접두사 (@, :, ? 등)
     * @param paramTypeMap           파라미터명 → CLR FQN 타입명 매핑 (null이면 보수적 가드 삽입)
     */
    public static string EmitBuildSqlMethod(
        ParsedStatement statement,
        string providerParameterPrefix,
        IReadOnlyDictionary<string, string>? paramTypeMap = null) {

        var sb = new StringBuilder(2048);

        sb.AppendLine($"        static (string sql, System.Collections.Generic.List<System.Data.Common.DbParameter> parameters) BuildSql_{SanitizeId(statement.Id)}(object? param, System.Data.Common.DbProviderFactory dbFactory)");
        sb.AppendLine("        {");
        sb.AppendLine("            var sb = new System.Text.StringBuilder();");
        sb.AppendLine("            var parameters = new System.Collections.Generic.List<System.Data.Common.DbParameter>();");
        sb.AppendLine("            var paramIndex = 0;");

        EmitNode(sb, statement.RootNode, providerParameterPrefix, 3, paramTypeMap);

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

    private static void EmitNode(
        StringBuilder sb,
        ParsedSqlNode node,
        string prefix,
        int indent,
        IReadOnlyDictionary<string, string>? paramTypeMap = null) {

        var sp = new string(' ', indent * 4);

        switch (node) {
            case TextNode textNode:
                EmitTextNode(sb, textNode, sp);
                break;

            case ParameterNode paramNode:
                EmitParameterNode(sb, paramNode, prefix, sp, paramTypeMap);
                break;

            case IfNode ifNode:
                EmitIfNode(sb, ifNode, prefix, indent, paramTypeMap);
                break;

            case ChooseNode chooseNode:
                EmitChooseNode(sb, chooseNode, prefix, indent, paramTypeMap);
                break;

            case WhereNode whereNode:
                EmitWhereNode(sb, whereNode, prefix, indent, paramTypeMap);
                break;

            case SetNode setNode:
                EmitSetNode(sb, setNode, prefix, indent, paramTypeMap);
                break;

            case ForEachNode forEachNode:
                EmitForEachNode(sb, forEachNode, prefix, indent, paramTypeMap);
                break;

            case MixedNode mixedNode:
                foreach (var child in mixedNode.Children) {
                    EmitNode(sb, child, prefix, indent, paramTypeMap);
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

    private static void EmitParameterNode(
        StringBuilder sb,
        ParameterNode paramNode,
        string prefix,
        string sp,
        IReadOnlyDictionary<string, string>? paramTypeMap = null) {

        if (paramNode.IsStringSubstitution) {
            EmitStringSubstitution(sb, paramNode.Name, sp, paramTypeMap);
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

    /**
     * ${} 문자열 치환 코드를 방출한다.
     *
     * paramTypeMap에서 해당 파라미터의 타입이 SqlIdentifier임을 확인할 수 있으면
     * ToString() 직접 호출 코드를 생성한다. 그렇지 않으면 런타임에서 SqlIdentifier
     * 타입을 검증하는 가드 코드를 삽입하여 NV004 오류 우회 시에도 SQL Injection을 차단한다.
     */
    private static void EmitStringSubstitution(
        StringBuilder sb,
        string paramName,
        string sp,
        IReadOnlyDictionary<string, string>? paramTypeMap) {

        var isSqlIdentifier = false;
        if (paramTypeMap is not null
            && paramTypeMap.TryGetValue(paramName, out var typeName)
            && typeName is not null) {

            // FQN 정확 일치만 허용: EndsWith 방식은 MySqlIdentifier 등 유사 타입명을 오인할 수 있다
            isSqlIdentifier = typeName == SqlIdentifierFqn;
        }

        if (isSqlIdentifier) {
            // SqlIdentifier: 생성 시점에 이미 검증됨 — ToString() 직접 호출
            sb.AppendLine($"{sp}sb.Append(GetPropertyValue(param, \"{paramName}\")?.ToString() ?? \"\");");
        } else {
            // string 등: NV004 Error로 빌드가 차단되어야 하나, 우회된 경우에도 런타임 차단
            var varName = $"__subst_{SanitizeId(paramName)}";
            sb.AppendLine($"{sp}{{");
            sb.AppendLine($"{sp}    var {varName} = GetPropertyValue(param, \"{paramName}\");");
            sb.AppendLine($"{sp}    if ({varName} is not NuVatis.Core.Sql.SqlIdentifier)");
            sb.AppendLine($"{sp}        throw new System.InvalidOperationException(");
            sb.AppendLine($"{sp}            \"${{{paramName}}} 치환에는 SqlIdentifier 타입이 필요합니다. \" +");
            sb.AppendLine($"{sp}            \"SqlIdentifier.From(), FromEnum(), FromAllowed() 중 하나를 사용하세요.\");");
            sb.AppendLine($"{sp}    sb.Append({varName}.ToString());");
            sb.AppendLine($"{sp}}}");
        }
    }

    private static void EmitIfNode(
        StringBuilder sb,
        IfNode ifNode,
        string prefix,
        int indent,
        IReadOnlyDictionary<string, string>? paramTypeMap = null) {

        var sp = new string(' ', indent * 4);
        sb.AppendLine($"{sp}if (GetPropertyValue(param, \"{ExtractPropertyName(ifNode.Test)}\") != null)");
        sb.AppendLine($"{sp}{{");
        foreach (var child in ifNode.Children) {
            EmitNode(sb, child, prefix, indent + 1, paramTypeMap);
        }
        sb.AppendLine($"{sp}}}");
    }

    private static void EmitChooseNode(
        StringBuilder sb,
        ChooseNode chooseNode,
        string prefix,
        int indent,
        IReadOnlyDictionary<string, string>? paramTypeMap = null) {

        var sp    = new string(' ', indent * 4);
        var first = true;

        foreach (var when in chooseNode.Whens) {
            var keyword = first ? "if" : "else if";
            sb.AppendLine($"{sp}{keyword} (GetPropertyValue(param, \"{ExtractPropertyName(when.Test)}\") != null)");
            sb.AppendLine($"{sp}{{");
            foreach (var child in when.Children) {
                EmitNode(sb, child, prefix, indent + 1, paramTypeMap);
            }
            sb.AppendLine($"{sp}}}");
            first = false;
        }

        if (chooseNode.Otherwise is { Length: > 0 } otherwise) {
            sb.AppendLine($"{sp}else");
            sb.AppendLine($"{sp}{{");
            foreach (var child in otherwise) {
                EmitNode(sb, child, prefix, indent + 1, paramTypeMap);
            }
            sb.AppendLine($"{sp}}}");
        }
    }

    private static void EmitWhereNode(
        StringBuilder sb,
        WhereNode whereNode,
        string prefix,
        int indent,
        IReadOnlyDictionary<string, string>? paramTypeMap = null) {

        var sp = new string(' ', indent * 4);
        sb.AppendLine($"{sp}{{");
        sb.AppendLine($"{sp}    var whereSb = new System.Text.StringBuilder();");
        sb.AppendLine($"{sp}    var outerSb = sb;");
        sb.AppendLine($"{sp}    sb = whereSb;");

        foreach (var child in whereNode.Children) {
            EmitNode(sb, child, prefix, indent + 1, paramTypeMap);
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

    private static void EmitSetNode(
        StringBuilder sb,
        SetNode setNode,
        string prefix,
        int indent,
        IReadOnlyDictionary<string, string>? paramTypeMap = null) {

        var sp = new string(' ', indent * 4);
        sb.AppendLine($"{sp}{{");
        sb.AppendLine($"{sp}    var setSb = new System.Text.StringBuilder();");
        sb.AppendLine($"{sp}    var outerSb = sb;");
        sb.AppendLine($"{sp}    sb = setSb;");

        foreach (var child in setNode.Children) {
            EmitNode(sb, child, prefix, indent + 1, paramTypeMap);
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

    private static void EmitForEachNode(
        StringBuilder sb,
        ForEachNode forEachNode,
        string prefix,
        int indent,
        IReadOnlyDictionary<string, string>? paramTypeMap = null) {

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
            EmitForEachChildNode(sb, child, forEachNode.Item, prefix, indent + 3, paramTypeMap);
        }

        sb.AppendLine($"{sp}        }}");
        if (forEachNode.Close is not null) {
            sb.AppendLine($"{sp}        sb.Append(\"{Escape(forEachNode.Close)}\");");
        }
        sb.AppendLine($"{sp}    }}");
        sb.AppendLine($"{sp}}}");
    }

    /**
     * ForEach 내부 노드에서 ParameterNode는 컬렉션 아이템을 바인딩해야 한다.
     *
     * #{item}              → item 자체를 바인딩
     * #{item.Property}     → GetPropertyValue(item, "Property")
     * #{item.A.B}          → GetPropertyValue(GetPropertyValue(item, "A"), "B")
     *
     * ${} 치환인 경우 아이템 자체가 SqlIdentifier인지 런타임 체크한다.
     * ForEach 컨텍스트에서는 컬렉션 원소 타입 정보를 paramTypeMap으로 전달받을 수 없으므로
     * 항상 런타임 타입 체크를 수행한다.
     */
    private static void EmitForEachChildNode(
        StringBuilder sb,
        ParsedSqlNode node,
        string itemVar,
        string prefix,
        int indent,
        IReadOnlyDictionary<string, string>? paramTypeMap = null) {

        var sp = new string(' ', indent * 4);

        if (node is TextNode textNode) {
            var escaped = textNode.Text.Replace("\"", "\"\"");
            sb.AppendLine($"{sp}sb.Append(@\"{escaped}\");");
        } else if (node is ParameterNode paramNode && !paramNode.IsStringSubstitution) {
            var valueExpr = BuildItemPropertyAccess(itemVar, paramNode.Name);
            sb.AppendLine($"{sp}{{");
            sb.AppendLine($"{sp}    var pName = \"{prefix}p\" + paramIndex++;");
            sb.AppendLine($"{sp}    sb.Append(pName);");
            sb.AppendLine($"{sp}    var p = dbFactory.CreateParameter();");
            sb.AppendLine($"{sp}    p.ParameterName = pName;");
            sb.AppendLine($"{sp}    p.Value = {valueExpr} ?? (object)System.DBNull.Value;");
            sb.AppendLine($"{sp}    parameters.Add(p);");
            sb.AppendLine($"{sp}}}");
        } else if (node is ParameterNode strSubNode && strSubNode.IsStringSubstitution) {
            // ForEach 내 ${}는 아이템 자체 — 아이템이 SqlIdentifier인지 런타임 체크
            sb.AppendLine($"{sp}if ({itemVar} is not NuVatis.Core.Sql.SqlIdentifier)");
            sb.AppendLine($"{sp}    throw new System.InvalidOperationException(");
            sb.AppendLine($"{sp}        \"foreach 내 ${{{strSubNode.Name}}} 치환에는 SqlIdentifier 타입이 필요합니다.\");");
            sb.AppendLine($"{sp}sb.Append({itemVar}.ToString());");
        } else {
            EmitNode(sb, node, prefix, indent, paramTypeMap);
        }
    }

    /**
     * foreach 아이템의 프로퍼티 접근 표현식을 생성한다.
     *
     * #{user}          → "user"
     * #{user.UserName} → "GetPropertyValue(user, \"UserName\")"
     * #{user.A.B}      → "GetPropertyValue(GetPropertyValue(user, \"A\"), \"B\")"
     */
    private static string BuildItemPropertyAccess(string itemVar, string paramName) {
        if (!paramName.Contains(".")) return itemVar;

        var firstDot   = paramName.IndexOf('.');
        var nestedPath = paramName.Substring(firstDot + 1);
        return BuildChainedPropertyAccess(itemVar, nestedPath);
    }

    private static string BuildChainedPropertyAccess(string obj, string path) {
        var parts   = path.Split('.');
        var current = obj;
        foreach (var part in parts) {
            current = $"GetPropertyValue({current}, \"{part}\")";
        }
        return current;
    }

    /**
     * ParsedSqlNode 트리로부터 MappedStatement.DynamicSqlBuilder에 사용할
     * Func<object?, (string, List<DbParameter>)> 람다 소스 코드를 생성한다.
     *
     * 생성 결과 예시:
     *   static (__param_) =>
     *   {
     *       var __sb_ = ...
     *       ...
     *       return (__sb_.ToString(), __params_);
     *   }
     *
     * @param rootNode     ParsedStatement.RootNode
     * @param paramPrefix  파라미터 접두사 (예: "@")
     */
    public static string EmitDynamicBuilderLambda(ParsedSqlNode rootNode, string paramPrefix = "@") {
        var sb = new StringBuilder(2048);
        sb.AppendLine("static (__param_) =>");
        sb.AppendLine("        {");
        sb.AppendLine("            var __sb_     = new System.Text.StringBuilder(256);");
        sb.AppendLine("            var __params_ = new System.Collections.Generic.List<System.Data.Common.DbParameter>();");
        sb.AppendLine("            var __idx_    = 0;");
        sb.AppendLine("            static object? __getprop_(object? o_, string n_)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (o_ == null) return null;");
        sb.AppendLine("                var p_ = o_.GetType().GetProperty(n_,");
        sb.AppendLine("                    System.Reflection.BindingFlags.Public |");
        sb.AppendLine("                    System.Reflection.BindingFlags.Instance |");
        sb.AppendLine("                    System.Reflection.BindingFlags.IgnoreCase);");
        sb.AppendLine("                return p_?.GetValue(o_);");
        sb.AppendLine("            }");
        EmitLambdaNode(sb, rootNode, "__param_", paramPrefix, 3);
        sb.AppendLine("            return (__sb_.ToString(), __params_);");
        sb.Append("        }");
        return sb.ToString();
    }

    private static void EmitLambdaNode(
        StringBuilder sb,
        ParsedSqlNode node,
        string paramVar,
        string prefix,
        int indent) {

        var sp = new string(' ', indent * 4);

        switch (node) {
            case TextNode text:
                if (!string.IsNullOrWhiteSpace(text.Text)) {
                    var escaped = text.Text.Replace("\"", "\"\"");
                    sb.AppendLine($"{sp}__sb_.Append(@\"{escaped}\");");
                }
                break;

            case ParameterNode param when !param.IsStringSubstitution: {
                var propAccess = BuildLambdaNestedAccess(paramVar, param.Name);
                sb.AppendLine($"{sp}{{");
                sb.AppendLine($"{sp}    var __pn_ = \"{prefix}p\" + __idx_++;");
                sb.AppendLine($"{sp}    __sb_.Append(__pn_);");
                sb.AppendLine($"{sp}    __params_.Add(NuVatis.Binding.ParameterBinder.CreateParameter(");
                sb.AppendLine($"{sp}        __pn_, {propAccess} ?? System.DBNull.Value));");
                sb.AppendLine($"{sp}}}");
                break;
            }

            case ForEachNode forEach: {
                var collAccess = BuildLambdaNestedAccess(paramVar, forEach.Collection);
                var collVar    = $"__coll_{SanitizeId(forEach.Collection)}_";
                var itemVar    = $"{SanitizeId(forEach.Item)}_";
                var firstVar   = $"__first_{SanitizeId(forEach.Collection)}_";
                sb.AppendLine($"{sp}{{");
                sb.AppendLine($"{sp}    var {collVar} = {collAccess} as System.Collections.IEnumerable;");
                sb.AppendLine($"{sp}    if ({collVar} != null)");
                sb.AppendLine($"{sp}    {{");
                if (forEach.Open is not null) {
                    sb.AppendLine($"{sp}        __sb_.Append(@\"{Escape(forEach.Open)}\");");
                }
                sb.AppendLine($"{sp}        var {firstVar} = true;");
                sb.AppendLine($"{sp}        foreach (var {itemVar} in {collVar})");
                sb.AppendLine($"{sp}        {{");
                if (forEach.Separator is not null) {
                    sb.AppendLine($"{sp}            if (!{firstVar}) __sb_.Append(@\"{Escape(forEach.Separator)}\");");
                }
                sb.AppendLine($"{sp}            {firstVar} = false;");
                foreach (var child in forEach.Children) {
                    EmitLambdaForEachChild(sb, child, itemVar, prefix, indent + 3);
                }
                sb.AppendLine($"{sp}        }}");
                if (forEach.Close is not null) {
                    sb.AppendLine($"{sp}        __sb_.Append(@\"{Escape(forEach.Close)}\");");
                }
                sb.AppendLine($"{sp}    }}");
                sb.AppendLine($"{sp}}}");
                break;
            }

            case ParameterNode strSub when strSub.IsStringSubstitution:
                // ${}는 SqlIdentifier 타입만 허용한다.
                sb.AppendLine($"{sp}{{");
                sb.AppendLine($"{sp}    var __sv_ = {BuildLambdaNestedAccess(paramVar, strSub.Name)};");
                sb.AppendLine($"{sp}    if (__sv_ is not NuVatis.Core.Sql.SqlIdentifier)");
                sb.AppendLine($"{sp}        throw new System.InvalidOperationException(");
                sb.AppendLine($"{sp}            \"${{{strSub.Name}}} 치환에는 SqlIdentifier 타입이 필요합니다.\");");
                sb.AppendLine($"{sp}    __sb_.Append(__sv_.ToString());");
                sb.AppendLine($"{sp}}}");
                break;

            case IfNode ifNode: {
                var propName   = ExtractPropertyName(ifNode.Test);
                var propAccess = BuildLambdaNestedAccess(paramVar, propName);
                sb.AppendLine($"{sp}if ({propAccess} != null)");
                sb.AppendLine($"{sp}{{");
                foreach (var child in ifNode.Children) {
                    EmitLambdaNode(sb, child, paramVar, prefix, indent + 1);
                }
                sb.AppendLine($"{sp}}}");
                break;
            }

            case ChooseNode choose: {
                var first = true;
                foreach (var when in choose.Whens) {
                    var keyword    = first ? "if" : "else if";
                    var propName   = ExtractPropertyName(when.Test);
                    var propAccess = BuildLambdaNestedAccess(paramVar, propName);
                    sb.AppendLine($"{sp}{keyword} ({propAccess} != null)");
                    sb.AppendLine($"{sp}{{");
                    foreach (var child in when.Children) {
                        EmitLambdaNode(sb, child, paramVar, prefix, indent + 1);
                    }
                    sb.AppendLine($"{sp}}}");
                    first = false;
                }
                if (choose.Otherwise is { Length: > 0 } otherwise) {
                    sb.AppendLine($"{sp}else");
                    sb.AppendLine($"{sp}{{");
                    foreach (var child in otherwise) {
                        EmitLambdaNode(sb, child, paramVar, prefix, indent + 1);
                    }
                    sb.AppendLine($"{sp}}}");
                }
                break;
            }

            case WhereNode where: {
                sb.AppendLine($"{sp}{{");
                sb.AppendLine($"{sp}    var __wSb_ = new System.Text.StringBuilder();");
                sb.AppendLine($"{sp}    var __outerSb_ = __sb_; __sb_ = __wSb_;");
                foreach (var child in where.Children) {
                    EmitLambdaNode(sb, child, paramVar, prefix, indent + 1);
                }
                sb.AppendLine($"{sp}    __sb_ = __outerSb_;");
                sb.AppendLine($"{sp}    var __wc_ = __wSb_.ToString().Trim();");
                sb.AppendLine($"{sp}    if (!string.IsNullOrEmpty(__wc_))");
                sb.AppendLine($"{sp}    {{");
                sb.AppendLine($"{sp}        if (__wc_.StartsWith(\"AND \", System.StringComparison.OrdinalIgnoreCase))");
                sb.AppendLine($"{sp}            __wc_ = __wc_.Substring(4);");
                sb.AppendLine($"{sp}        else if (__wc_.StartsWith(\"OR \", System.StringComparison.OrdinalIgnoreCase))");
                sb.AppendLine($"{sp}            __wc_ = __wc_.Substring(3);");
                sb.AppendLine($"{sp}        __sb_.Append(\" WHERE \").Append(__wc_);");
                sb.AppendLine($"{sp}    }}");
                sb.AppendLine($"{sp}}}");
                break;
            }

            case SetNode set: {
                sb.AppendLine($"{sp}{{");
                sb.AppendLine($"{sp}    var __sSb_ = new System.Text.StringBuilder();");
                sb.AppendLine($"{sp}    var __outerSb_ = __sb_; __sb_ = __sSb_;");
                foreach (var child in set.Children) {
                    EmitLambdaNode(sb, child, paramVar, prefix, indent + 1);
                }
                sb.AppendLine($"{sp}    __sb_ = __outerSb_;");
                sb.AppendLine($"{sp}    var __sc_ = __sSb_.ToString().Trim();");
                sb.AppendLine($"{sp}    if (!string.IsNullOrEmpty(__sc_))");
                sb.AppendLine($"{sp}    {{");
                sb.AppendLine($"{sp}        if (__sc_.EndsWith(\",\")) __sc_ = __sc_.Substring(0, __sc_.Length - 1);");
                sb.AppendLine($"{sp}        __sb_.Append(\" SET \").Append(__sc_);");
                sb.AppendLine($"{sp}    }}");
                sb.AppendLine($"{sp}}}");
                break;
            }

            case MixedNode mixed:
                foreach (var child in mixed.Children) {
                    EmitLambdaNode(sb, child, paramVar, prefix, indent);
                }
                break;
        }
    }

    /**
     * foreach 내부 자식 노드를 아이템 변수 기준으로 emit한다.
     * #{item.Property} 형태의 중첩 프로퍼티 접근을 처리한다.
     */
    private static void EmitLambdaForEachChild(
        StringBuilder sb,
        ParsedSqlNode node,
        string itemVar,
        string prefix,
        int indent) {

        var sp = new string(' ', indent * 4);

        switch (node) {
            case TextNode text:
                if (!string.IsNullOrWhiteSpace(text.Text)) {
                    var escaped = text.Text.Replace("\"", "\"\"");
                    sb.AppendLine($"{sp}__sb_.Append(@\"{escaped}\");");
                }
                break;

            case ParameterNode param when !param.IsStringSubstitution: {
                // #{item}          → itemVar
                // #{item.Property} → __getprop_(itemVar, "Property")
                // #{item.A.B}      → __getprop_(__getprop_(itemVar, "A"), "B")
                string valueExpr;
                if (param.Name.Contains(".")) {
                    var firstDot   = param.Name.IndexOf('.');
                    var nestedPath = param.Name.Substring(firstDot + 1);
                    valueExpr = BuildLambdaChainedAccess(itemVar, nestedPath);
                } else {
                    valueExpr = itemVar;
                }
                sb.AppendLine($"{sp}{{");
                sb.AppendLine($"{sp}    var __pn_ = \"{prefix}p\" + __idx_++;");
                sb.AppendLine($"{sp}    __sb_.Append(__pn_);");
                sb.AppendLine($"{sp}    __params_.Add(NuVatis.Binding.ParameterBinder.CreateParameter(");
                sb.AppendLine($"{sp}        __pn_, {valueExpr} ?? System.DBNull.Value));");
                sb.AppendLine($"{sp}}}");
                break;
            }

            case ParameterNode strSub when strSub.IsStringSubstitution:
                sb.AppendLine($"{sp}if ({itemVar} is not NuVatis.Core.Sql.SqlIdentifier)");
                sb.AppendLine($"{sp}    throw new System.InvalidOperationException(");
                sb.AppendLine($"{sp}        \"foreach 내 ${{{strSub.Name}}} 치환에는 SqlIdentifier 타입이 필요합니다.\");");
                sb.AppendLine($"{sp}__sb_.Append({itemVar}.ToString());");
                break;

            case MixedNode mixed:
                foreach (var child in mixed.Children) {
                    EmitLambdaForEachChild(sb, child, itemVar, prefix, indent);
                }
                break;
        }
    }

    /**
     * __getprop_ 함수를 사용한 다단계 프로퍼티 접근 표현식을 생성한다.
     * "users"       → "__getprop_(__param_, \"users\")"
     * "user.Name"   → "__getprop_(__getprop_(__param_, \"user\"), \"Name\")"
     */
    private static string BuildLambdaNestedAccess(string paramVar, string propertyPath) {
        var parts   = propertyPath.Split('.');
        var current = paramVar;
        foreach (var part in parts) {
            current = $"__getprop_({current}, \"{part}\")";
        }
        return current;
    }

    private static string BuildLambdaChainedAccess(string startObj, string path) {
        var parts   = path.Split('.');
        var current = startObj;
        foreach (var part in parts) {
            current = $"__getprop_({current}, \"{part}\")";
        }
        return current;
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
