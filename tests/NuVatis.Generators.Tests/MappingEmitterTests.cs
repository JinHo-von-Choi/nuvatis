using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NuVatis.Generators.Emitters;
using NuVatis.Generators.Models;
using Xunit;

namespace NuVatis.Generators.Tests;

/**
 * MappingEmitter 단위 테스트.
 * 타입 안전 reader 메서드 생성 및 fallback 동작을 검증한다.
 *
 * @author 최진호
 * @date   2026-02-26
 */
public class MappingEmitterTests {

    [Fact]
    public void EmitMapMethod_WithoutTypeSymbol_UsesGetFieldValueObject() {
        var resultMap = CreateResultMap("userMap", "MyApp.User",
            ("id", "Id"), ("name", "Name"));

        var code = MappingEmitter.EmitMapMethod(resultMap, "MyApp.User");

        Assert.Contains("reader.GetFieldValue<object>", code);
        Assert.Contains("Map_userMap", code);
        Assert.Contains("new MyApp.User()", code);
    }

    [Fact]
    public void EmitMapMethod_GeneratesOrdinalAndNullCheck() {
        var resultMap = CreateResultMap("userMap", "MyApp.User",
            ("id", "Id"));

        var code = MappingEmitter.EmitMapMethod(resultMap, "MyApp.User");

        Assert.Contains("reader.GetOrdinal(\"id\")", code);
        Assert.Contains("reader.IsDBNull(ordinal_id)", code);
        Assert.Contains("obj.Id =", code);
    }

    [Fact]
    public void EmitMapMethod_SanitizesSpecialCharactersInColumnNames() {
        var resultMap = CreateResultMap("map", "T",
            ("user.name", "UserName"), ("order-id", "OrderId"));

        var code = MappingEmitter.EmitMapMethod(resultMap, "T");

        Assert.Contains("ordinal_user_name", code);
        Assert.Contains("ordinal_order_id", code);
    }

    [Fact]
    public void EmitMapMethod_HandlesMultipleMappings() {
        var resultMap = CreateResultMap("map", "T",
            ("col_a", "A"), ("col_b", "B"), ("col_c", "C"));

        var code = MappingEmitter.EmitMapMethod(resultMap, "T");

        Assert.Contains("obj.A =", code);
        Assert.Contains("obj.B =", code);
        Assert.Contains("obj.C =", code);
    }

    [Fact]
    public void EmitMapMethodFromType_WithNullTypeSymbol_GeneratesSwitchDispatch()
    {
        var code = MappingEmitter.EmitMapMethodFromType(
            "Map_T_MyApp_User",
            "MyApp.User",
            typeSymbol: null);

        Assert.NotNull(code);
        Assert.Contains("Map_T_MyApp_User", code!);
        Assert.Contains("reader.FieldCount", code!);
        Assert.Contains("switch (__key)", code!);
        Assert.Contains("new MyApp.User()", code!);
        Assert.Contains("return obj;", code!);
    }

    [Fact]
    public void EmitMapMethodFromType_WithNullTypeSymbol_EmptySwitch_WhenNoProperties()
    {
        var code = MappingEmitter.EmitMapMethodFromType("Map_T_Empty", "Empty", null);

        Assert.NotNull(code);
        Assert.Contains("switch (__key)", code!);
        Assert.DoesNotContain("case ", code!);
    }

    [Fact]
    public void EmitMapMethodFromType_NullTypeSymbol_DoesNotReturnNull()
    {
        var code = MappingEmitter.EmitMapMethodFromType("Map_T_X", "X", null);
        Assert.NotNull(code);
    }

    [Fact]
    public void EmitMapMethodFromType_EnumProperty_GeneratesIntCast()
    {
        var source = @"
namespace TestApp {
    public enum UserStatus { Active, Inactive, Banned }
    public class UserWithEnum {
        public int Id { get; set; }
        public UserStatus Status { get; set; }
    }
}";

        var compilation = CreateCompilation(source);
        var typeSymbol  = compilation.GetTypeByMetadataName("TestApp.UserWithEnum")!;

        var code = MappingEmitter.EmitMapMethodFromType(
            "Map_T_TestApp_UserWithEnum",
            "TestApp.UserWithEnum",
            typeSymbol);

        Assert.NotNull(code);
        Assert.DoesNotContain("GetFieldValue<TestApp.UserStatus>", code!);
        Assert.Contains("(TestApp.UserStatus)", code!);
        Assert.Contains("GetInt32", code!);
    }

    [Fact]
    public void EmitMapMethodFromType_NullableEnumProperty_GeneratesNullableIntCast()
    {
        var source = @"
namespace TestApp {
    public enum Priority { Low, Medium, High }
    public class TaskWithNullableEnum {
        public int Id { get; set; }
        public Priority? Priority { get; set; }
    }
}";

        var compilation = CreateCompilation(source);
        var typeSymbol  = compilation.GetTypeByMetadataName("TestApp.TaskWithNullableEnum")!;

        var code = MappingEmitter.EmitMapMethodFromType(
            "Map_T_TestApp_TaskWithNullableEnum",
            "TestApp.TaskWithNullableEnum",
            typeSymbol);

        Assert.NotNull(code);
        Assert.Contains("(TestApp.Priority?)", code!);
        Assert.Contains("GetInt32", code!);
    }

    private static Compilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = new[] {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(
                System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!,
                    "System.Runtime.dll"))
        };

        return CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static ParsedResultMap CreateResultMap(
        string id, string type, params (string column, string property)[] mappings) {

        var builder = ImmutableArray.CreateBuilder<ParsedResultMapping>();
        foreach (var (column, property) in mappings) {
            builder.Add(new ParsedResultMapping(column, property, null, column == "id"));
        }

        return new ParsedResultMap(
            id, type, null,
            builder.ToImmutable(),
            ImmutableArray<ParsedAssociation>.Empty,
            ImmutableArray<ParsedCollection>.Empty);
    }
}
