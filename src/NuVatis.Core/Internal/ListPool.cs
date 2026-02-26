using System.Collections.Concurrent;
using System.Data.Common;

namespace NuVatis.Internal;

/**
 * DbParameter 리스트 풀링.
 * 매 쿼리마다 List<DbParameter>를 new하는 대신 풀에서 가져와 재사용한다.
 * GC Gen0 컬렉션을 줄이는 것이 목적이다.
 *
 * @author 최진호
 * @date   2026-02-26
 */
internal static class DbParameterListPool {
    private static readonly ConcurrentBag<List<DbParameter>> Pool = new();
    private const int MaxPoolSize = 64;

    internal static List<DbParameter> Rent() {
        if (Pool.TryTake(out var list)) {
            return list;
        }
        return new List<DbParameter>(8);
    }

    internal static void Return(List<DbParameter> list) {
        if (list.Count > 128 || Pool.Count >= MaxPoolSize) {
            return;
        }
        list.Clear();
        Pool.Add(list);
    }
}
