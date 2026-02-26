using System.Collections.Concurrent;
using NuVatis.Interceptor;

namespace NuVatis.Internal;

/**
 * InterceptorContext 오브젝트 풀링.
 * 매 쿼리마다 InterceptorContext를 new하는 대신 풀에서 가져와 재사용한다.
 *
 * @author 최진호
 * @date   2026-02-26
 */
internal static class InterceptorContextPool {
    private static readonly ConcurrentBag<InterceptorContext> Pool = new();
    private const int MaxPoolSize = 64;

    internal static InterceptorContext Rent() {
        if (Pool.TryTake(out var ctx)) {
            ctx.Items.Clear();
            return ctx;
        }
        return new InterceptorContext {
            StatementId   = string.Empty,
            Sql           = string.Empty,
            Parameters    = Array.Empty<System.Data.Common.DbParameter>(),
            StatementType = NuVatis.Statement.StatementType.Select
        };
    }

    internal static void Return(InterceptorContext ctx) {
        if (Pool.Count >= MaxPoolSize) return;

        ctx.Sql                 = string.Empty;
        ctx.Parameters          = Array.Empty<System.Data.Common.DbParameter>();
        ctx.Parameter           = null;
        ctx.ElapsedMilliseconds = 0;
        ctx.AffectedRows        = null;
        ctx.Exception           = null;
        ctx.Items.Clear();
        Pool.Add(ctx);
    }
}
