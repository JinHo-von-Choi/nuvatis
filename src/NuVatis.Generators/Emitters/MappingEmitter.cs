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
