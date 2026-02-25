using NuVatis.Configuration;
using Xunit;

namespace NuVatis.Tests;

/**
 * TypeAliasRegistry 단위 테스트.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public class TypeAliasRegistryTests {

    [Fact]
    public void RegisterAndResolve_Success() {
        var registry = new TypeAliasRegistry();
        registry.Register("string", typeof(string));

        var type = registry.Resolve("string");

        Assert.Equal(typeof(string), type);
    }

    [Fact]
    public void ResolveIsCaseInsensitive() {
        var registry = new TypeAliasRegistry();
        registry.Register("MyType", typeof(int));

        var type1 = registry.Resolve("mytype");
        var type2 = registry.Resolve("MYTYPE");

        Assert.Equal(typeof(int), type1);
        Assert.Equal(typeof(int), type2);
    }

    [Fact]
    public void DuplicateRegistrationThrows() {
        var registry = new TypeAliasRegistry();
        registry.Register("dup", typeof(string));

        Assert.Throws<InvalidOperationException>(() => registry.Register("dup", typeof(int)));
    }

    [Fact]
    public void ResolveUnknownAliasThrows() {
        var registry = new TypeAliasRegistry();

        Assert.Throws<InvalidOperationException>(() => registry.Resolve("nonexistent_type_xyz"));
    }

    [Fact]
    public void TryResolveReturnsFalseForUnknown() {
        var registry = new TypeAliasRegistry();

        var result = registry.TryResolve("nonexistent_type_xyz", out var type);

        Assert.False(result);
        Assert.Null(type);
    }

    [Fact]
    public void TryResolveReturnsTrueForRegistered() {
        var registry = new TypeAliasRegistry();
        registry.Register("bool", typeof(bool));

        var result = registry.TryResolve("bool", out var type);

        Assert.True(result);
        Assert.Equal(typeof(bool), type);
    }
}
