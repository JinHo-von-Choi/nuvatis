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

    // StringBuilderCache는 제품 코드에서 호출 지점이 0건이라 클래스째로 제거했다.
    // (자체 테스트만 존재하는 코드는 실제로 쓰이지 않는 코드이며, 통과하는 테스트가
    //  미사용 상태를 정상으로 보이게 만드는 문제가 있었다)

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
