#nullable enable
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using NuVatis.Generators.Analysis;

namespace NuVatis.Generators.Emitters;

/**
 * NuVatisMapperRegistry 클래스 소스 코드를 생성한다.
 * SG가 발견한 모든 Mapper를 DI 비의존적 방식으로 등록하고,
 * Attribute SQL 기반 MappedStatement도 등록하는 코드.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public static class RegistryEmitter {

    public static string Emit(ImmutableArray<MapperInterfaceInfo> interfaces) {
        var sb = new StringBuilder(2048);

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

    private static string GetImplementationName(string interfaceName) {
        return interfaceName.StartsWith("I") && interfaceName.Length > 1 && char.IsUpper(interfaceName[1])
            ? interfaceName.Substring(1) + "Impl"
            : interfaceName + "Impl";
    }
}
