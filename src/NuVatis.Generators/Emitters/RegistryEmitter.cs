#nullable enable
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using NuVatis.Generators.Analysis;
using NuVatis.Generators.Models;

namespace NuVatis.Generators.Emitters;

/**
 * NuVatisMapperRegistry 클래스 소스 코드를 생성한다.
 * SG가 발견한 모든 Mapper를 DI 비의존적 방식으로 등록하고,
 * Attribute SQL 기반 MappedStatement와 XML 매퍼 기반 MappedStatement도 등록하는 코드.
 *
 * @author 최진호
 * @date   2026-02-24
 * @modified 2026-03-06 RegisterXmlStatements 생성 추가 (foreach 중첩 프로퍼티 수정)
 */
public static class RegistryEmitter {

    public static string Emit(
        ImmutableArray<MapperInterfaceInfo> interfaces,
        ImmutableArray<ParsedMapper> xmlMappers = default) {

        var sb = new StringBuilder(4096);

        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using NuVatis.Session;");
        sb.AppendLine("using NuVatis.Statement;");
        sb.AppendLine();
        sb.AppendLine("namespace NuVatis");
        sb.AppendLine("{");
        sb.AppendLine("    [System.CodeDom.Compiler.GeneratedCode(\"NuVatis.Generators\", \"1.0.0\")]");
        sb.AppendLine("    public static class NuVatisMapperRegistry");
        sb.AppendLine("    {");

        EmitRegisterAll(sb, interfaces);
        sb.AppendLine();
        EmitRegisterStatements(sb, interfaces);

        if (!xmlMappers.IsDefaultOrEmpty) {
            sb.AppendLine();
            EmitRegisterXmlStatements(sb, xmlMappers);
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void EmitRegisterAll(StringBuilder sb, ImmutableArray<MapperInterfaceInfo> interfaces) {
        sb.AppendLine("        public static void RegisterAll(");
        sb.AppendLine("            ISqlSessionFactory factory,");
        sb.AppendLine("            Action<Type, Func<ISqlSession, object>> register)");
        sb.AppendLine("        {");

        foreach (var info in interfaces) {
            var implName     = GetImplementationName(info.Name);
            var fullImplName = $"{info.Namespace}.{implName}";
            sb.AppendLine($"            register(typeof({info.FullyQualifiedName}), session => new {fullImplName}(session));");
        }

        sb.AppendLine("        }");
    }

    /**
     * Attribute SQL ([Select], [Insert] 등)이 있는 메서드를
     * MappedStatement으로 등록하는 코드 생성.
     */
    private static void EmitRegisterStatements(StringBuilder sb, ImmutableArray<MapperInterfaceInfo> interfaces) {
        var attrMethods = interfaces
            .SelectMany(i => i.Methods
                .Where(m => m.SqlAttributeValue is not null)
                .Select(m => (Interface: i, Method: m)))
            .ToList();

        sb.AppendLine("        public static void RegisterAttributeStatements(");
        sb.AppendLine("            Dictionary<string, MappedStatement> statements)");
        sb.AppendLine("        {");

        foreach (var (iface, method) in attrMethods) {
            var fullId   = $"{iface.FullyQualifiedName}.{method.Name}";
            var stmtType = method.SqlAttributeType ?? "Select";
            var escaped  = method.SqlAttributeValue!.Replace("\"", "\"\"");

            sb.AppendLine($"            statements[\"{fullId}\"] = new MappedStatement");
            sb.AppendLine("            {");
            sb.AppendLine($"                Id        = \"{method.Name}\",");
            sb.AppendLine($"                Namespace = \"{iface.FullyQualifiedName}\",");
            sb.AppendLine($"                Type      = StatementType.{stmtType},");
            sb.AppendLine($"                SqlSource = @\"{escaped}\"");
            sb.AppendLine("            };");
        }

        sb.AppendLine("        }");
    }

    /**
     * XML 매퍼의 모든 statement를 MappedStatement로 등록하는 코드 생성.
     *
     * 정적 SQL (동적 태그 없음): SqlSource에 평탄화된 SQL 문자열 직접 삽입
     * 동적 SQL (foreach, if, where 등 포함): DynamicSqlBuilder 람다 생성
     */
    private static void EmitRegisterXmlStatements(
        StringBuilder sb,
        ImmutableArray<ParsedMapper> xmlMappers) {

        sb.AppendLine("        public static void RegisterXmlStatements(");
        sb.AppendLine("            Dictionary<string, MappedStatement> statements)");
        sb.AppendLine("        {");

        foreach (var mapper in xmlMappers) {
            foreach (var stmt in mapper.Statements) {
                var fullId    = $"{mapper.Namespace}.{stmt.Id}";
                var stmtType  = CapitalizeFirst(stmt.StatementType);
                var isDynamic = DynamicSqlEmitter.HasDynamicNodes(stmt.RootNode);

                sb.AppendLine($"            statements[\"{EscapeString(fullId)}\"] = new MappedStatement");
                sb.AppendLine("            {");
                sb.AppendLine($"                Id        = \"{EscapeString(stmt.Id)}\",");
                sb.AppendLine($"                Namespace = \"{EscapeString(mapper.Namespace)}\",");
                sb.AppendLine($"                Type      = StatementType.{stmtType},");

                if (stmt.ResultMapId is not null) {
                    sb.AppendLine($"                ResultMapId = \"{EscapeString(stmt.ResultMapId)}\",");
                }
                if (stmt.Timeout.HasValue) {
                    sb.AppendLine($"                CommandTimeout = {stmt.Timeout.Value},");
                }

                if (isDynamic) {
                    sb.AppendLine("                SqlSource = \"\",");
                    sb.Append("                DynamicSqlBuilder = ");
                    var lambda = ParameterEmitter.EmitDynamicBuilderLambda(stmt.RootNode, "@");
                    sb.Append(lambda);
                    sb.AppendLine(",");
                } else {
                    var sqlSource = FlattenToSqlSource(stmt.RootNode).Replace("\"", "\"\"");
                    sb.AppendLine($"                SqlSource = @\"{sqlSource}\",");
                }

                sb.AppendLine("            };");
            }
        }

        sb.AppendLine("        }");
    }

    /**
     * ParsedSqlNode 트리에서 정적 SQL 소스 문자열을 재구성한다.
     * #{...} 파라미터와 텍스트 노드만 처리한다 (동적 노드는 포함하지 않는다).
     */
    private static string FlattenToSqlSource(ParsedSqlNode node) {
        var sb = new StringBuilder(256);
        FlattenNode(sb, node);
        return sb.ToString().Trim();
    }

    private static void FlattenNode(StringBuilder sb, ParsedSqlNode node) {
        switch (node) {
            case TextNode t:
                sb.Append(t.Text);
                break;
            case ParameterNode p:
                sb.Append(p.IsStringSubstitution ? "${" : "#{");
                sb.Append(p.Name);
                sb.Append('}');
                break;
            case MixedNode m:
                foreach (var child in m.Children) {
                    FlattenNode(sb, child);
                }
                break;
        }
    }

    private static string CapitalizeFirst(string s) {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant();
    }

    private static string EscapeString(string s) {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string GetImplementationName(string interfaceName) {
        return interfaceName.StartsWith("I") && interfaceName.Length > 1 && char.IsUpper(interfaceName[1])
            ? interfaceName.Substring(1) + "Impl"
            : interfaceName + "Impl";
    }
}
