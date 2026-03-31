#nullable enable
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using NuVatis.Generators.Analysis;
using NuVatis.Generators.Models;

namespace NuVatis.Generators.Emitters;

/**
 * resultType-only statement의 행 매핑 메서드를 공유 internal static 클래스에 생성한다.
 * 프록시와 레지스트리 양쪽에서 참조 가능하도록 프록시 클래스 외부에 위치시킨다.
 *
 * @author 최진호
 * @date   2026-03-31
 */
internal static class TypeMappersEmitter {

    /**
     * NuVatisTypeMappers 클래스 소스를 생성하고, 타입 FQN → 메서드명 매핑을 반환한다.
     */
    internal static (string Source, Dictionary<string, string> TypeToMethod) Emit(
        ImmutableArray<MapperInterfaceInfo> interfaces,
        ImmutableArray<ParsedMapper> xmlMappers,
        Compilation compilation) {

        var sb           = new StringBuilder(2048);
        var typeToMethod = new Dictionary<string, string>(System.StringComparer.Ordinal);
        var generated    = new HashSet<string>(System.StringComparer.Ordinal);

        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System.Data.Common;");
        sb.AppendLine();
        sb.AppendLine("namespace NuVatis");
        sb.AppendLine("{");
        sb.AppendLine("    [System.CodeDom.Compiler.GeneratedCode(\"NuVatis.Generators\", \"1.0.0\")]");
        sb.AppendLine("    internal static class NuVatisTypeMappers");
        sb.AppendLine("    {");

        // 1. Attribute-based methods
        foreach (var iface in interfaces) {
            foreach (var method in iface.Methods) {
                if (method.SqlAttributeValue is null) continue;
                if (method.ResultMapAttributeValue is not null) continue;

                var resultTypeFqn = method.ElementType ?? method.UnwrappedReturnType;
                if (resultTypeFqn is null) continue;

                TryEmitMapper(sb, compilation, resultTypeFqn, generated, typeToMethod);
            }
        }

        // 2. XML mapper statements (resultType-only)
        if (!xmlMappers.IsDefaultOrEmpty) {
            foreach (var mapper in xmlMappers) {
                foreach (var stmt in mapper.Statements) {
                    if (stmt.ResultType is null) continue;
                    if (stmt.ResultMapId is not null) continue;

                    TryEmitMapper(sb, compilation, stmt.ResultType, generated, typeToMethod);
                }
            }
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return (sb.ToString(), typeToMethod);
    }

    private static void TryEmitMapper(
        StringBuilder sb,
        Compilation compilation,
        string resultTypeFqn,
        HashSet<string> generated,
        Dictionary<string, string> typeToMethod) {

        if (generated.Contains(resultTypeFqn)) return;

        var typeSymbol = compilation.GetTypeByMetadataName(resultTypeFqn);
        if (typeSymbol is null) return;

        var methodName = "Map_T_" + MappingEmitter.SanitizeIdPublic(resultTypeFqn);
        var targetFqn  = TypeResolver.GetFullyQualifiedName(typeSymbol);

        var code = MappingEmitter.EmitMapMethodFromType(methodName, targetFqn, typeSymbol);

        // 스칼라 타입 등 매핑 불가 타입은 generated에 등록하여 이후 중복 호출을 차단한다.
        generated.Add(resultTypeFqn);

        if (code is null) return;

        sb.Append(code);
        sb.AppendLine();
        typeToMethod[resultTypeFqn] = methodName;
    }
}
