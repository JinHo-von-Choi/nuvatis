#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using NuVatis.Generators.Analysis;
using NuVatis.Generators.Models;

namespace NuVatis.Generators.Emitters;

/**
 * Mapper 인터페이스의 정적 프록시 구현 클래스 C# 소스를 생성한다.
 * ResultMap이 있는 select 쿼리에 대해 SG 생성 매핑 메서드를 통합한다.
 * 각 statement마다 BuildSql_XXX 정적 메서드를 인라인으로 방출하여
 * ISqlSession 레지스트리 조회를 우회하고 SelectOneSql/ExecuteSql 계열 메서드를 호출한다.
 *
 * @author 최진호
 * @date   2026-02-24
 * @modified 2026-02-26 SG ResultMap 매핑 통합
 * @modified 2026-03-09 인라인 SQL 빌드 방식으로 전환 (BuildSql_XXX + SelectOneSql/ExecuteSql)
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
        sb.AppendLine("using NuVatis.Mapping;");
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

        Dictionary<string, string>? resultTypeMethodRefs = null;
        if (mapper is not null && compilation is not null) {
            resultTypeMethodRefs = EmitMappingMethods(sb, interfaceInfo, mapper, compilation);
        }

        // 각 statement마다 BuildSql_XXX 정적 메서드를 인라인으로 방출한다.
        if (mapper is not null) {
            foreach (var stmt in mapper.Statements) {
                var paramTypeMap = BuildParamTypeMap(interfaceInfo, stmt);
                var buildSqlCode = ParameterEmitter.EmitBuildSqlStaticMethod(stmt, "@", paramTypeMap);
                sb.AppendLine();
                sb.Append(buildSqlCode);
            }
        }

        foreach (var method in interfaceInfo.Methods) {
            sb.AppendLine();
            EmitMethod(sb, method, sqlNamespace, mapper, resultTypeMethodRefs);
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /**
     * statement의 데이터 파라미터 타입 맵을 구성한다.
     * MapperMethodInfo.Parameters에서 비-CancellationToken 파라미터를 찾아
     * 파라미터명 → FQN 타입명 매핑을 반환한다.
     */
    private static Dictionary<string, string>? BuildParamTypeMap(
        MapperInterfaceInfo interfaceInfo,
        ParsedStatement stmt) {

        var method = interfaceInfo.Methods.FirstOrDefault(m => m.Name == stmt.Id);
        if (method is null) return null;

        var dataParam = method.Parameters.FirstOrDefault(p => !p.IsCancellationToken);
        if (dataParam is null) return null;

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            [dataParam.Name] = dataParam.Type
        };
    }

    /**
     * ResultMap 정의에 대한 정적 매핑 메서드를 클래스 본문에 삽입한다.
     *
     * XML resultMap type 속성이 올바른 FQN을 포함하지 않는 경우(예: 프로젝트 이름에
     * "NuVatis"가 포함되어 루트 네임스페이스가 중복 생성되는 시나리오)를 위해,
     * GetTypeByMetadataName 실패 시 인터페이스 메서드의 Roslyn FQN으로 폴백한다.
     */
    private static Dictionary<string, string> EmitMappingMethods(
        StringBuilder sb, MapperInterfaceInfo interfaceInfo, ParsedMapper mapper, Compilation compilation) {

        var resultTypeMethodRefs = new Dictionary<string, string>(StringComparer.Ordinal);

        // 기존: ResultMap 기반 매핑 메서드
        var resultMapTypeOverrides = BuildResultMapTypeOverrides(interfaceInfo, mapper);

        foreach (var resultMap in mapper.ResultMaps) {
            var typeSymbol     = compilation.GetTypeByMetadataName(resultMap.Type);
            string targetTypeName;

            if (typeSymbol is not null) {
                targetTypeName = TypeResolver.GetFullyQualifiedName(typeSymbol);
            } else if (resultMapTypeOverrides.TryGetValue(resultMap.Id, out var overrideType)) {
                typeSymbol     = compilation.GetTypeByMetadataName(overrideType);
                targetTypeName = overrideType;
            } else {
                targetTypeName = resultMap.Type;
            }

            var code = MappingEmitter.EmitMapMethod(resultMap, targetTypeName, typeSymbol);
            sb.AppendLine();
            sb.Append(code);
        }

        // 신규: resultType-only statement 매핑 메서드 (중복 타입은 한 번만 생성)
        var generatedTypes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var stmt in mapper.Statements) {
            if (stmt.ResultType is null || stmt.ResultMapId is not null) continue;

            var typeSymbol = compilation.GetTypeByMetadataName(stmt.ResultType);
            if (typeSymbol is null) continue;

            var targetTypeName = TypeResolver.GetFullyQualifiedName(typeSymbol);
            string? methodName = "Map_T_" + SanitizeTypeName(stmt.ResultType);

            if (generatedTypes.Add(stmt.ResultType)) {
                var code = MappingEmitter.EmitMapMethodFromType(methodName, targetTypeName, typeSymbol);
                if (code is not null) {
                    sb.AppendLine();
                    sb.Append(code);
                } else {
                    methodName = null;
                }
            }

            if (methodName is not null) {
                resultTypeMethodRefs[stmt.Id] = methodName;
            }
        }

        return resultTypeMethodRefs;
    }

    /**
     * ResultMap ID → 인터페이스 메서드 Roslyn FQN 타입명 매핑을 구성한다.
     *
     * XML resultMap.Type이 GetTypeByMetadataName으로 해석되지 않을 때의 폴백으로 사용.
     * statement.ResultMapId와 인터페이스 메서드의 반환 타입(element type)을 연결한다.
     */
    private static System.Collections.Generic.Dictionary<string, string> BuildResultMapTypeOverrides(
        MapperInterfaceInfo interfaceInfo, ParsedMapper mapper) {

        var overrides = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal);

        foreach (var stmt in mapper.Statements) {
            if (stmt.ResultMapId is null) continue;

            var method = interfaceInfo.Methods.FirstOrDefault(m => m.Name == stmt.Id);
            if (method is null) continue;

            string? actualTypeName = method.ReturnsList
                ? method.ElementType
                : (method.IsAsync ? method.UnwrappedReturnType : method.ReturnType);

            if (actualTypeName is not null && !overrides.ContainsKey(stmt.ResultMapId)) {
                overrides[stmt.ResultMapId] = actualTypeName;
            }
        }

        return overrides;
    }

    private static void EmitMethod(
        StringBuilder sb,
        MapperMethodInfo method,
        string sqlNamespace,
        ParsedMapper? mapper,
        Dictionary<string, string>? resultTypeMethodRefs = null) {

        var paramsList = string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));
        var asyncMod   = method.IsAsync ? "async " : "";

        sb.AppendLine($"        public {asyncMod}{method.ReturnType} {method.Name}({paramsList})");
        sb.AppendLine("        {");

        var ctParam   = method.Parameters.FirstOrDefault(p => p.IsCancellationToken);
        var dataParam = method.Parameters.FirstOrDefault(p => !p.IsCancellationToken);
        var sqlId     = $"{sqlNamespace}.{method.Name}";
        var paramExpr = dataParam?.Name ?? "null";
        var ctExpr    = ctParam?.Name ?? "default";

        var stmtType   = ResolveStatementType(method, mapper);
        var parsedStmt = mapper?.Statements.FirstOrDefault(s => s.Id == method.Name);

        // BuildSql_XXX 인라인 방출은 ParsedStatement가 있을 때만 사용한다.
        // mapper가 없거나 statement가 없는 경우(어노테이션 전용)는 레지스트리 위임 방식으로 폴백한다.
        var hasBuildSql = parsedStmt is not null;

        if (stmtType == "Select") {
            string? resultMapMethodRef = null;
            if (parsedStmt?.ResultMapId is not null) {
                resultMapMethodRef = $"Map_{MappingEmitter.SanitizeIdPublic(parsedStmt.ResultMapId)}";
            } else if (parsedStmt?.ResultType is not null
                       && resultTypeMethodRefs?.TryGetValue(method.Name, out var rtRef) == true) {
                resultMapMethodRef = rtRef;
            }
            if (hasBuildSql) {
                EmitSelectMethod(sb, method, sqlId, paramExpr, ctExpr, resultMapMethodRef);
            } else {
                EmitSelectMethodLegacy(sb, method, sqlId, paramExpr, ctExpr, resultMapMethodRef);
            }
        } else {
            if (hasBuildSql) {
                EmitWriteMethod(sb, method, stmtType, sqlId, paramExpr, ctExpr);
            } else {
                EmitWriteMethodLegacy(sb, method, stmtType, sqlId, paramExpr, ctExpr);
            }
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

        var buildSqlCall = $"BuildSql_{SanitizeName(method.Name)}({paramExpr})";

        if (method.ReturnsList) {
            var elementType = method.ElementType ?? "object";
            var mapperArg   = resultMapMethodRef
                ?? $"NuVatis.Mapping.ColumnMapper.MapRow<{elementType}>";
            if (method.IsAsync) {
                sb.AppendLine($"            var (__sql_, __pars_) = {buildSqlCall};");
                sb.AppendLine($"            return await _session.SelectListSqlAsync<{elementType}>(\"{sqlId}\", __sql_, __pars_, {mapperArg}, {ctExpr}).ConfigureAwait(false);");
            } else {
                sb.AppendLine($"            var (__sql_, __pars_) = {buildSqlCall};");
                sb.AppendLine($"            return _session.SelectListSql<{elementType}>(\"{sqlId}\", __sql_, __pars_, {mapperArg});");
            }
        } else {
            var resultType = method.IsAsync
                ? (method.UnwrappedReturnType ?? "object")
                : method.ReturnType;
            var mapperArg = resultMapMethodRef
                ?? $"NuVatis.Mapping.ColumnMapper.MapRow<{resultType}>";
            if (method.IsAsync) {
                sb.AppendLine($"            var (__sql_, __pars_) = {buildSqlCall};");
                sb.AppendLine($"            return await _session.SelectOneSqlAsync<{resultType}>(\"{sqlId}\", __sql_, __pars_, {mapperArg}, {ctExpr}).ConfigureAwait(false);");
            } else {
                sb.AppendLine($"            var (__sql_, __pars_) = {buildSqlCall};");
                sb.AppendLine($"            return _session.SelectOneSql<{resultType}>(\"{sqlId}\", __sql_, __pars_, {mapperArg});");
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

        var buildSqlCall = $"BuildSql_{SanitizeName(method.Name)}({paramExpr})";
        sb.AppendLine($"            var (__sql_, __pars_) = {buildSqlCall};");
        if (method.IsAsync) {
            sb.AppendLine($"            return await _session.ExecuteSqlAsync(\"{sqlId}\", __sql_, __pars_, {ctExpr}).ConfigureAwait(false);");
        } else {
            sb.AppendLine($"            return _session.ExecuteSql(\"{sqlId}\", __sql_, __pars_);");
        }
    }

    /**
     * ParsedStatement 없는 레거시 경로 (어노테이션 전용 매퍼): _session.SelectOne/SelectList 위임.
     */
    private static void EmitSelectMethodLegacy(
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

    /**
     * ParsedStatement 없는 레거시 경로 (어노테이션 전용 매퍼): _session.Insert/Update/Delete 위임.
     */
    private static void EmitWriteMethodLegacy(
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

    /**
     * 메서드명을 BuildSql_ 접미사에 안전하게 사용할 수 있는 식별자로 변환한다.
     */
    private static string SanitizeName(string name) {
        return name.Replace(".", "_").Replace("-", "_");
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

    private static string SanitizeTypeName(string typeName) {
        return typeName.Replace(".", "_").Replace("<", "_").Replace(">", "_")
                       .Replace(",", "_").Replace(" ", "_");
    }
}
