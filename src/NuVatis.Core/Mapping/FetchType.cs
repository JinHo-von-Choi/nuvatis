namespace NuVatis.Mapping;

/**
 * Association/Collection의 로딩 전략.
 * Eager: 즉시 로딩 (기본), Lazy: 첫 접근 시 로딩.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public enum FetchType {
    /// <summary>쿼리 실행 시 즉시 연관 객체를 로딩한다 (기본값).</summary>
    Eager,
    /// <summary>연관 프로퍼티에 처음 접근할 때 로딩한다.</summary>
    Lazy
}
