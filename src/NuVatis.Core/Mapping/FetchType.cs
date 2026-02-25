namespace NuVatis.Mapping;

/**
 * Association/Collection의 로딩 전략.
 * Eager: 즉시 로딩 (기본), Lazy: 첫 접근 시 로딩.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public enum FetchType {
    Eager,
    Lazy
}
