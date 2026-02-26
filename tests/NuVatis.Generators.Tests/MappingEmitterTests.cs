using System.Collections.Immutable;
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
