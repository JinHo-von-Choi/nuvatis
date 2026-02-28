#nullable enable
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using NuVatis.Generators.Analysis;
using NuVatis.Generators.Models;

namespace NuVatis.Generators.Emitters;

/**
 * Mapper 인터페이스의 정적 프록시 구현 클래스 C# 소스를 생성한다.
 * ResultMap이 있는 select 쿼리에 대해 SG 생성 매핑 메서드를 통합한다.
 *
 * NOTE (Issue 2 — paramTypeMap SG 파이프라인 연결):
 * 현재 ProxyEmitter는 SQL 세션에 SQL ID만 전달하는 방식으로 작동한다.
 * ParameterEmitter.EmitBuildSqlMethod는 이 파이프라인에서 호출되지 않는다.
 * 동적 SQL BuildSql_XXX 로컬 함수는 런타임에 ISqlSession이 직접 생성한다.
 *
 * SG 파이프라인에서 paramTypeMap을 구성하여 ParameterEmitter에 전달하려면
 * ProxyEmitter가 ISqlSession 대신 BuildSql_XXX 메서드를 인라인으로 방출해야 한다.
 * MapperMethodInfo.Parameters에는 FQN 타입 정보(Type 필드)가 이미 존재하므로
 * 기술적 제약은 없으나, 세션 위임 방식에서 인라인 방출 방식으로의 아키텍처 전환이 필요하다.
 *
 * TODO: paramTypeMap을 SG 파이프라인에서 전달 필요 — v3.0 AOT 작업과 함께
 *
 * @author 최진호
 * @date   2026-02-24
 * @modified 2026-02-26 SG ResultMap 매핑 통합
 * @modified 2026-02-28 Issue 2 제약사항 문서화
 */
public static class ProxyEmitter {

    public static string Emit(
        MapperInterfaceInfo interfaceInfo, ParsedMapper? mapper, Compilation? compilation = null) {
        var sb           = new StringBuilder(2048);
        var sqlNamespace = mapper?.Namespace ?? interfaceInfo.FullyQualifiedName;
        var implName     = GetImplementationName(interfaceInfo.Name);

        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Data.Common;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using NuVatis.Session;");
        sb.AppendLine();
        sb.AppendLine($"namespace {interfaceInfo.Namespace}");
        sb.AppendLine("{");
        sb.AppendLine("    [System.CodeDom.Compiler.GeneratedCode(\"NuVatis.Generators\", \"1.0.0\")]");
        sb.AppendLine($"    internal sealed class {implName} : {interfaceInfo.FullyQualifiedName}");
        sb.AppendLine("    {");
        sb.AppendLine("        private readonly ISqlSession _session;");
        sb.AppendLine();
        sb.AppendLine($"        public {implName}(ISqlSession session)");
        sb.AppendLine("        {");
        sb.AppendLine("            _session = session;");
        sb.AppendLine("        }");

        if (mapper is not null && compilation is not null) {
            EmitMappingMethods(sb, mapper, compilation);
        }

        foreach (var method in interfaceInfo.Methods) {
            sb.AppendLine();
            EmitMethod(sb, method, sqlNamespace, mapper);
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /**
     * ResultMap 정의에 대한 정적 매핑 메서드를 클래스 본문에 삽입한다.
     */
    private static void EmitMappingMethods(
        StringBuilder sb, ParsedMapper mapper, Compilation compilation) {

        foreach (var resultMap in mapper.ResultMaps) {
            var typeSymbol = compilation.GetTypeByMetadataName(resultMap.Type);
            var code       = MappingEmitter.EmitMapMethod(resultMap, resultMap.Type, typeSymbol);
            sb.AppendLine();
            sb.Append(code);
        }
    }

    private static void EmitMethod(
        StringBuilder sb,
        MapperMethodInfo method,
        string sqlNamespace,
        ParsedMapper? mapper) {

        var paramsList = string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));
        var asyncMod   = method.IsAsync ? "async " : "";

        sb.AppendLine($"        public {asyncMod}{method.ReturnType} {method.Name}({paramsList})");
        sb.AppendLine("        {");

        var ctParam   = method.Parameters.FirstOrDefault(p => p.IsCancellationToken);
        var dataParam = method.Parameters.FirstOrDefault(p => !p.IsCancellationToken);
        var sqlId     = $"{sqlNamespace}.{method.Name}";
        var paramExpr = dataParam?.Name ?? "null";
        var ctExpr    = ctParam?.Name ?? "default";

        var stmtType = ResolveStatementType(method, mapper);

        if (stmtType == "Select") {
            string? resultMapMethodRef = null;
            var parsedStmt = mapper?.Statements.FirstOrDefault(s => s.Id == method.Name);
            if (parsedStmt?.ResultMapId is not null) {
                resultMapMethodRef = $"Map_{MappingEmitter.SanitizeIdPublic(parsedStmt.ResultMapId)}";
            }
            EmitSelectMethod(sb, method, sqlId, paramExpr, ctExpr, resultMapMethodRef);
        } else {
            EmitWriteMethod(sb, method, stmtType, sqlId, paramExpr, ctExpr);
        }

        sb.AppendLine("        }");
    }

    private static void EmitSelectMethod(
        StringBuilder sb,
        MapperMethodInfo method,
        string sqlId,
        string paramExpr,
        string ctExpr,
        string? resultMapMethodRef = null) {

        if (method.ReturnsList) {
            var elementType = method.ElementType ?? "object";
            if (resultMapMethodRef is not null) {
                if (method.IsAsync) {
                    sb.AppendLine($"            return await _session.SelectListAsync<{elementType}>(\"{sqlId}\", {paramExpr}, {resultMapMethodRef}, {ctExpr}).ConfigureAwait(false);");
                } else {
                    sb.AppendLine($"            return _session.SelectList<{elementType}>(\"{sqlId}\", {paramExpr}, {resultMapMethodRef});");
                }
            } else {
                if (method.IsAsync) {
                    sb.AppendLine($"            return await _session.SelectListAsync<{elementType}>(\"{sqlId}\", {paramExpr}, {ctExpr}).ConfigureAwait(false);");
                } else {
                    sb.AppendLine($"            return _session.SelectList<{elementType}>(\"{sqlId}\", {paramExpr});");
                }
            }
        } else {
            var resultType = method.IsAsync
                ? (method.UnwrappedReturnType ?? "object")
                : method.ReturnType;
            if (resultMapMethodRef is not null) {
                if (method.IsAsync) {
                    sb.AppendLine($"            return await _session.SelectOneAsync<{resultType}>(\"{sqlId}\", {paramExpr}, {resultMapMethodRef}, {ctExpr}).ConfigureAwait(false);");
                } else {
                    sb.AppendLine($"            return _session.SelectOne<{resultType}>(\"{sqlId}\", {paramExpr}, {resultMapMethodRef});");
                }
            } else {
                if (method.IsAsync) {
                    sb.AppendLine($"            return await _session.SelectOneAsync<{resultType}>(\"{sqlId}\", {paramExpr}, {ctExpr}).ConfigureAwait(false);");
                } else {
                    sb.AppendLine($"            return _session.SelectOne<{resultType}>(\"{sqlId}\", {paramExpr});");
                }
            }
        }
    }

    private static void EmitWriteMethod(
        StringBuilder sb,
        MapperMethodInfo method,
        string stmtType,
        string sqlId,
        string paramExpr,
        string ctExpr) {

        if (method.IsAsync) {
            sb.AppendLine($"            return await _session.{stmtType}Async(\"{sqlId}\", {paramExpr}, {ctExpr}).ConfigureAwait(false);");
        } else {
            sb.AppendLine($"            return _session.{stmtType}(\"{sqlId}\", {paramExpr});");
        }
    }

    private static string ResolveStatementType(MapperMethodInfo method, ParsedMapper? mapper) {
        if (method.SqlAttributeType is not null) {
            return method.SqlAttributeType;
        }

        var parsedStmt = mapper?.Statements
            .FirstOrDefault(s => s.Id == method.Name);

        if (parsedStmt is not null) {
            return parsedStmt.StatementType;
        }

        var nameLower = method.Name.ToLowerInvariant();
        if (nameLower.StartsWith("get") || nameLower.StartsWith("find")
            || nameLower.StartsWith("select") || nameLower.StartsWith("search")
            || nameLower.StartsWith("list") || nameLower.StartsWith("count")) {
            return "Select";
        }
        if (nameLower.StartsWith("insert") || nameLower.StartsWith("add") || nameLower.StartsWith("create")) {
            return "Insert";
        }
        if (nameLower.StartsWith("update") || nameLower.StartsWith("modify") || nameLower.StartsWith("set")) {
            return "Update";
        }
        if (nameLower.StartsWith("delete") || nameLower.StartsWith("remove")) {
            return "Delete";
        }

        return "Select";
    }

    private static string GetImplementationName(string interfaceName) {
        return interfaceName.StartsWith("I") && interfaceName.Length > 1 && char.IsUpper(interfaceName[1])
            ? interfaceName.Substring(1) + "Impl"
            : interfaceName + "Impl";
    }
}
