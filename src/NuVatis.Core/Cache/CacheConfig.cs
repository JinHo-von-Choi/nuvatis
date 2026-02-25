namespace NuVatis.Cache;

/**
 * Namespace 단위 캐시 설정.
 * XML의 &lt;cache&gt; 엘리먼트에 대응한다.
 *
 * 예:
 *   &lt;cache eviction="LRU" flushInterval="600000" size="512" /&gt;
 *
 * @author 최진호
 * @date   2026-02-25
 */
public sealed class CacheConfig {
    /** 캐시 축출 정책 (기본: LRU). */
    public CacheEviction Eviction     { get; init; } = CacheEviction.Lru;

    /** 캐시 자동 갱신 주기 (ms). null이면 자동 갱신하지 않음. */
    public long? FlushIntervalMs      { get; init; }

    /** 캐시 최대 항목 수 (기본: 1024). */
    public int Size                   { get; init; } = 1024;

    /** 읽기 전용 캐시 여부 (기본: true). */
    public bool ReadOnly              { get; init; } = true;
}

/**
 * 캐시 축출 정책.
 */
public enum CacheEviction {
    /** Least Recently Used. */
    Lru,
    /** First-In First-Out. */
    Fifo
}
