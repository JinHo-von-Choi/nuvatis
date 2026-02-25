#nullable enable
using System.Text;
using NuVatis.Generators.Models;

namespace NuVatis.Generators.Emitters;

/**
 * ResultMap 기반 매핑 코드를 생성한다.
 * DbDataReader에서 프로퍼티로 값을 할당하는 static 메서드를 방출.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public static class MappingEmitter {

    public static string EmitMapMethod(ParsedResultMap resultMap, string targetTypeName) {
        var sb = new StringBuilder(512);

        sb.AppendLine($"        static {targetTypeName} Map_{SanitizeId(resultMap.Id)}(System.Data.Common.DbDataReader reader)");
        sb.AppendLine("        {");
        sb.AppendLine($"            var obj = new {targetTypeName}();");

        foreach (var mapping in resultMap.Mappings) {
            sb.AppendLine($"            var ordinal_{SanitizeName(mapping.Column)} = reader.GetOrdinal(\"{mapping.Column}\");");
            sb.AppendLine($"            if (!reader.IsDBNull(ordinal_{SanitizeName(mapping.Column)}))");
            sb.AppendLine("            {");
            sb.AppendLine($"                obj.{mapping.Property} = reader.GetFieldValue<object>(ordinal_{SanitizeName(mapping.Column)});");
            sb.AppendLine("            }");
        }

        sb.AppendLine("            return obj;");
        sb.AppendLine("        }");

        return sb.ToString();
    }

    /**
     * ResultMap ID를 C# 식별자로 변환.
     * '.' 등 비식별자 문자를 '_'로 치환.
     */
    private static string SanitizeId(string id) {
        return id.Replace(".", "_").Replace("-", "_");
    }

    private static string SanitizeName(string name) {
        return name.Replace(".", "_").Replace("-", "_").Replace(" ", "_");
    }
}
