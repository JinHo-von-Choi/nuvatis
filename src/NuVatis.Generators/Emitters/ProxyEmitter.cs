#nullable enable
using System.Linq;
using System.Text;
using NuVatis.Generators.Analysis;
using NuVatis.Generators.Models;

namespace NuVatis.Generators.Emitters;

/**
 * Mapper 인터페이스의 정적 프록시 구현 클래스 C# 소스를 생성한다.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public static class ProxyEmitter {

    public static string Emit(MapperInterfaceInfo interfaceInfo, ParsedMapper? mapper) {
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

        foreach (var method in interfaceInfo.Methods) {
            sb.AppendLine();
            EmitMethod(sb, method, sqlNamespace, mapper);
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
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
            EmitSelectMethod(sb, method, sqlId, paramExpr, ctExpr);
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
        string ctExpr) {

        var effectiveType = method.IsAsync ? method.UnwrappedReturnType : method.ReturnType;

        if (method.ReturnsList) {
            var elementType = method.ElementType ?? "object";
            if (method.IsAsync) {
                sb.AppendLine($"            return await _session.SelectListAsync<{elementType}>(\"{sqlId}\", {paramExpr}, {ctExpr}).ConfigureAwait(false);");
            } else {
                sb.AppendLine($"            return _session.SelectList<{elementType}>(\"{sqlId}\", {paramExpr});");
            }
        } else {
            var resultType = method.IsAsync
                ? (method.UnwrappedReturnType ?? "object")
                : method.ReturnType;
            if (method.IsAsync) {
                sb.AppendLine($"            return await _session.SelectOneAsync<{resultType}>(\"{sqlId}\", {paramExpr}, {ctExpr}).ConfigureAwait(false);");
            } else {
                sb.AppendLine($"            return _session.SelectOne<{resultType}>(\"{sqlId}\", {paramExpr});");
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
