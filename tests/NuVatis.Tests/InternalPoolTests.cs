using System.Reflection;
using System.Text;
using Xunit;

namespace NuVatis.Tests;

/**
 * Internal 풀링 유틸리티 테스트.
 * Internal 클래스에 접근하기 위해 InternalsVisibleTo 또는 리플렉션 사용.
 *
 * @author 최진호
 * @date   2026-02-26
 */
public class InternalPoolTests {

    [Fact]
    public void StringBuilderCache_AcquireAndRelease() {
        var cacheType = typeof(NuVatis.Binding.ParameterBinder).Assembly
            .GetType("NuVatis.Internal.StringBuilderCache");
        Assert.NotNull(cacheType);

        var acquire = cacheType.GetMethod("Acquire", BindingFlags.Static | BindingFlags.NonPublic);
        var release = cacheType.GetMethod("GetStringAndRelease", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(acquire);
        Assert.NotNull(release);

        var sb = (StringBuilder)acquire.Invoke(null, new object[] { 256 })!;
        sb.Append("hello");
        var result = (string)release.Invoke(null, new object[] { sb })!;
        Assert.Equal("hello", result);

        var sb2 = (StringBuilder)acquire.Invoke(null, new object[] { 128 })!;
        Assert.True(sb2.Length == 0);
    }

    [Fact]
    public void DbParameterListPool_RentAndReturn() {
        var poolType = typeof(NuVatis.Binding.ParameterBinder).Assembly
            .GetType("NuVatis.Internal.DbParameterListPool");
        Assert.NotNull(poolType);

        var rent   = poolType.GetMethod("Rent", BindingFlags.Static | BindingFlags.NonPublic);
        var ret    = poolType.GetMethod("Return", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(rent);
        Assert.NotNull(ret);

        var list = rent.Invoke(null, null);
        Assert.NotNull(list);

        ret.Invoke(null, new[] { list });
    }
}
