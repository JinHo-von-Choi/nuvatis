#nullable enable
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using NuVatis.Generators.Models;

namespace NuVatis.Generators.Emitters;

/**
 * ResultMap 기반 매핑 코드를 생성한다.
 * DbDataReader에서 프로퍼티로 값을 할당하는 static 메서드를 방출.
 * Compilation 정보가 있으면 프로퍼티 타입에 맞는 타입 안전 reader 메서드를 생성한다.
 *
 * @author 최진호
 * @date   2026-02-24
 * @modified 2026-02-26 타입 안전 reader 메서드 생성 (GetString, GetInt32 등)
 */
public static class MappingEmitter {

    private static readonly Dictionary<string, string> TypeToReaderMethod = new() {
        ["System.String"]         = "GetString",
        ["System.Int32"]          = "GetInt32",
        ["System.Int64"]          = "GetInt64",
        ["System.Int16"]          = "GetInt16",
        ["System.Boolean"]        = "GetBoolean",
        ["System.Decimal"]        = "GetDecimal",
        ["System.Double"]         = "GetDouble",
        ["System.Single"]         = "GetFloat",
        ["System.DateTime"]       = "GetDateTime",
        ["System.Guid"]           = "GetGuid",
        ["System.Byte"]           = "GetByte",
        ["System.Char"]           = "GetChar",
    };

    private static readonly System.Collections.Generic.HashSet<SpecialType> ScalarSpecialTypes = new() {
        SpecialType.System_Boolean,
        SpecialType.System_Byte,
        SpecialType.System_SByte,
        SpecialType.System_Int16,
        SpecialType.System_UInt16,
        SpecialType.System_Int32,
        SpecialType.System_UInt32,
        SpecialType.System_Int64,
        SpecialType.System_UInt64,
        SpecialType.System_Single,
        SpecialType.System_Double,
        SpecialType.System_Decimal,
        SpecialType.System_Char,
        SpecialType.System_String,
    };

    /**
     * ResultMap에 대한 매핑 메서드를 생성한다.
     *
     * @param resultMap 파싱된 ResultMap 정의
     * @param targetTypeName 생성할 대상 타입의 전체 이름
     * @param typeSymbol Compilation에서 찾은 타입 심볼 (null이면 GetFieldValue&lt;object&gt; 폴백)
     */
    public static string EmitMapMethod(
        ParsedResultMap resultMap, string targetTypeName, INamedTypeSymbol? typeSymbol = null) {

        var propertyTypes = BuildPropertyTypeMap(typeSymbol);
        var sb            = new StringBuilder(512);

        sb.AppendLine($"        static {targetTypeName} Map_{SanitizeId(resultMap.Id)}(System.Data.Common.DbDataReader reader)");
        sb.AppendLine("        {");
        sb.AppendLine($"            var obj = new {targetTypeName}();");

        foreach (var mapping in resultMap.Mappings) {
            var ordinalVar = $"ordinal_{SanitizeName(mapping.Column)}";
            var readExpr   = GetReadExpression(mapping.Property, ordinalVar, propertyTypes);

            sb.AppendLine($"            var {ordinalVar} = reader.GetOrdinal(\"{mapping.Column}\");");
            sb.AppendLine($"            if (!reader.IsDBNull({ordinalVar}))");
            sb.AppendLine("            {");
            sb.AppendLine($"                obj.{mapping.Property} = {readExpr};");
            sb.AppendLine("            }");
        }

        sb.AppendLine("            return obj;");
        sb.AppendLine("        }");

        return sb.ToString();
    }

    /**
     * 프로퍼티 이름 → 타입 전체 이름 매핑을 구축한다.
     * Nullable<T>인 경우 내부 T 타입으로 매핑한다.
     */
    private static Dictionary<string, string>? BuildPropertyTypeMap(INamedTypeSymbol? typeSymbol) {
        if (typeSymbol is null) return null;

        var map     = new Dictionary<string, string>();
        var current = typeSymbol;

        while (current is not null) {
            foreach (var member in current.GetMembers()) {
                if (member is IPropertySymbol { DeclaredAccessibility: Accessibility.Public, IsStatic: false } prop) {
                    var typeName = UnwrapNullable(prop.Type);
                    if (!map.ContainsKey(prop.Name)) {
                        map[prop.Name] = typeName;
                    }
                }
            }
            current = current.BaseType;
        }

        return map;
    }

    private static bool IsScalarTypeSymbol(INamedTypeSymbol typeSymbol) {
        var actual = typeSymbol;
        if (actual.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            && actual.TypeArguments.Length == 1
            && actual.TypeArguments[0] is INamedTypeSymbol inner) {
            actual = inner;
        }
        if (ScalarSpecialTypes.Contains(actual.SpecialType)) return true;
        if (actual.TypeKind == TypeKind.Enum) return true;
        var fqn = actual.ToDisplayString();
        return fqn is "System.DateTime" or "System.DateTimeOffset" or "System.Guid"
                    or "System.TimeSpan";
    }

    private static System.Collections.Generic.IEnumerable<string> GetWritablePropertyNames(
        INamedTypeSymbol? typeSymbol) {

        if (typeSymbol is null) yield break;

        var current = typeSymbol;
        var seen    = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);

        while (current is not null) {
            foreach (var member in current.GetMembers()) {
                if (member is IPropertySymbol {
                    DeclaredAccessibility: Accessibility.Public,
                    IsStatic: false,
                    SetMethod: not null } prop) {
                    if (seen.Add(prop.Name)) {
                        yield return prop.Name;
                    }
                }
            }
            current = current.BaseType;
        }
    }

    private static string UnwrapNullable(ITypeSymbol type) {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            && nullable.TypeArguments.Length == 1) {
            return nullable.TypeArguments[0].ToDisplayString();
        }
        return type.ToDisplayString();
    }

    private static string GetReadExpression(
        string propertyName, string ordinalVar, Dictionary<string, string>? propertyTypes) {

        if (propertyTypes is not null && propertyTypes.TryGetValue(propertyName, out var typeName)) {
            if (TypeToReaderMethod.TryGetValue(typeName, out var readerMethod)) {
                return $"reader.{readerMethod}({ordinalVar})";
            }
            return $"reader.GetFieldValue<{typeName}>({ordinalVar})";
        }

        return $"reader.GetFieldValue<object>({ordinalVar})";
    }

    /**
     * resultType-only 쿼리에 대해 switch-dispatch 매핑 메서드를 생성한다.
     * reader.FieldCount를 순회하며 정규화된 컬럼명(underscore 제거, 소문자)으로
     * 프로퍼티에 값을 할당한다. 스칼라 타입이면 null을 반환한다.
     *
     * @param methodName     생성할 메서드 이름 (예: "Map_T_MyApp_User")
     * @param targetTypeName 대상 타입의 FQN (예: "MyApp.User")
     * @param typeSymbol     Roslyn 타입 심볼. null이면 빈 switch 생성 (GetFieldValue 폴백 없음)
     * @returns 생성된 메서드 소스, 스칼라 타입이면 null
     */
    public static string? EmitMapMethodFromType(
        string methodName,
        string targetTypeName,
        INamedTypeSymbol? typeSymbol) {

        if (typeSymbol is not null && IsScalarTypeSymbol(typeSymbol)) return null;

        var propertyTypes = BuildPropertyTypeMap(typeSymbol);
        var propertyNames = GetWritablePropertyNames(typeSymbol);
        var sb            = new StringBuilder(512);

        sb.AppendLine($"        static {targetTypeName} {methodName}(System.Data.Common.DbDataReader reader)");
        sb.AppendLine("        {");
        sb.AppendLine($"            var obj = new {targetTypeName}();");
        sb.AppendLine("            for (int __i = 0; __i < reader.FieldCount; __i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.IsDBNull(__i)) continue;");
        sb.AppendLine("                var __key = reader.GetName(__i).Replace(\"_\", \"\").ToLowerInvariant();");
        sb.AppendLine("                switch (__key)");
        sb.AppendLine("                {");

        foreach (var propName in propertyNames) {
            var switchKey = propName.ToLowerInvariant();
            var readExpr  = GetReadExpression(propName, "__i", propertyTypes);
            sb.AppendLine($"                    case \"{switchKey}\": obj.{propName} = {readExpr}; break;");
        }

        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            return obj;");
        sb.AppendLine("        }");

        return sb.ToString();
    }

    public static string SanitizeIdPublic(string id) {
        return SanitizeId(id);
    }

    private static string SanitizeId(string id) {
        return id.Replace(".", "_").Replace("-", "_");
    }

    private static string SanitizeName(string name) {
        return name.Replace(".", "_").Replace("-", "_").Replace(" ", "_");
    }
}
