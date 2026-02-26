using System.Text;

namespace NuVatis.Internal;

/**
 * Thread-local StringBuilder 캐싱.
 * .NET Runtime 내부의 StringBuilderCache와 동일한 패턴으로
 * 짧은 수명의 StringBuilder 할당을 제거한다.
 *
 * Acquire -> 사용 -> GetStringAndRelease 순서로 호출한다.
 * 기본 용량 256, 최대 캐시 대상 1024.
 *
 * @author 최진호
 * @date   2026-02-26
 */
internal static class StringBuilderCache {
    private const int DefaultCapacity = 256;
    private const int MaxCacheSize    = 1024;

    [ThreadStatic]
    private static StringBuilder? _cached;

    internal static StringBuilder Acquire(int capacity = DefaultCapacity) {
        if (capacity <= MaxCacheSize) {
            var sb = _cached;
            if (sb is not null && sb.Capacity >= capacity) {
                _cached = null;
                sb.Clear();
                return sb;
            }
        }
        return new StringBuilder(capacity);
    }

    internal static string GetStringAndRelease(StringBuilder sb) {
        var result = sb.ToString();
        if (sb.Capacity <= MaxCacheSize) {
            _cached = sb;
        }
        return result;
    }
}
