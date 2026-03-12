using NuVatis.Cache;
using Xunit;

namespace NuVatis.Tests;

/**
 * CacheKey 단위 테스트.
 * 결정적(deterministic) 키 빌더 구현 검증, AOT 안전성 검증.
 *
 * @author   최진호
 * @date     2026-03-12
 */
public class CacheKeyTests {

    [Fact]
    public void Generate_NullParameter_ReturnsStatementId() {
        var key = CacheKey.Generate("ns.stmt", null);
        Assert.Equal("ns.stmt", key);
    }

    [Fact]
    public void Generate_SameParameter_ProducesSameKey() {
        var p  = new { Id = 1, Name = "Alice" };
        var k1 = CacheKey.Generate("ns.stmt", p);
        var k2 = CacheKey.Generate("ns.stmt", p);
        Assert.Equal(k1, k2);
    }

    [Fact]
    public void Generate_DifferentValues_ProducesDifferentKeys() {
        var k1 = CacheKey.Generate("ns.stmt", new { Id = 1 });
        var k2 = CacheKey.Generate("ns.stmt", new { Id = 2 });
        Assert.NotEqual(k1, k2);
    }

    [Fact]
    public void Generate_DictionaryParameter_Deterministic() {
        var d  = new Dictionary<string, object?> { ["x"] = 10, ["y"] = 20 };
        var k1 = CacheKey.Generate("ns.stmt", d);
        var k2 = CacheKey.Generate("ns.stmt", d);
        Assert.Equal(k1, k2);
    }

    [Fact]
    public void Generate_PrimitiveParameter_Works() {
        var k = CacheKey.Generate("ns.stmt", 42);
        Assert.NotEqual("ns.stmt", k);
        Assert.StartsWith("ns.stmt:", k);
    }
}
