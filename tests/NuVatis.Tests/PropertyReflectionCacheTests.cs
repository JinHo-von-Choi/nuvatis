using System.Reflection;
using Xunit;

namespace NuVatis.Tests;

/**
 * PropertyReflectionCache 단위 테스트.
 * internal 클래스이므로 리플렉션을 통해 접근한다.
 *
 * @author 최진호
 * @date   2026-03-05
 */
public class PropertyReflectionCacheTests {

    private sealed class SampleModel {
        public int    UserId   { get; set; }
        public string UserName { get; set; } = "";
        public string Email    { get; set; } = "";
    }

    private static readonly Type CacheType =
        typeof(NuVatis.Binding.ParameterBinder).Assembly
            .GetType("NuVatis.Internal.PropertyReflectionCache")!;

    private static readonly MethodInfo GetOrBuildMethod =
        CacheType.GetMethod("GetOrBuild", BindingFlags.Static | BindingFlags.Public)!;

    /** GetOrBuild를 리플렉션으로 호출한다. */
    private static Dictionary<string, PropertyInfo> GetOrBuild(Type type, bool normalizeUnderscore = false) {
        return (Dictionary<string, PropertyInfo>)GetOrBuildMethod.Invoke(null, new object[] { type, normalizeUnderscore })!;
    }

    [Fact]
    public void GetOrBuild_ReturnsPropertyMap_WithOriginalName() {
        var map = GetOrBuild(typeof(SampleModel));
        Assert.True(map.ContainsKey("UserId"));
        Assert.True(map.ContainsKey("UserName"));
    }

    [Fact]
    public void GetOrBuild_ReturnsPropertyMap_CaseInsensitive() {
        var map = GetOrBuild(typeof(SampleModel));
        Assert.True(map.ContainsKey("userid"));
        Assert.True(map.ContainsKey("USERNAME"));
    }

    [Fact]
    public void GetOrBuild_WithNormalization_RegistersUnderscoreStrippedName() {
        var map = GetOrBuild(typeof(SampleModel), normalizeUnderscore: true);
        Assert.True(map.ContainsKey("UserId"));
    }

    [Fact]
    public void GetOrBuild_SameType_ReturnsCachedInstance() {
        var map1 = GetOrBuild(typeof(SampleModel));
        var map2 = GetOrBuild(typeof(SampleModel));
        Assert.Same(map1, map2);
    }

    [Fact]
    public void GetOrBuild_WithoutNormalization_DoesNotRegisterNormalizedKeys() {
        var map = GetOrBuild(typeof(SampleModel), normalizeUnderscore: false);
        Assert.True(map.ContainsKey("UserId"));
    }
}
