namespace NuVatis.Cache;

/**
 * 2차 캐시 제공자 인터페이스.
 * Namespace 단위로 캐시를 관리한다.
 * 캐시 키 = statementId + 파라미터 해시.
 *
 * MemoryCacheProvider가 기본 구현이며,
 * Redis 등 외부 캐시 연동은 이 인터페이스의 추가 구현으로 확장한다.
 *
 * @author 최진호
 * @date   2026-02-25
 */
public interface ICacheProvider {
    /**
     * 캐시에서 값을 조회한다.
     * 캐시 미스 시 null을 반환한다.
     *
     * @param namespace_  네임스페이스
     * @param cacheKey    캐시 키 (statementId + 파라미터 해시)
     * @returns 캐시된 값 또는 null
     */
    object? Get(string namespace_, string cacheKey);

    /**
     * 캐시에 값을 저장한다.
     *
     * @param namespace_  네임스페이스
     * @param cacheKey    캐시 키
     * @param value       저장할 값
     */
    void Put(string namespace_, string cacheKey, object value);

    /**
     * 특정 네임스페이스의 캐시를 전부 무효화한다.
     * Insert/Update/Delete 실행 시 호출된다.
     *
     * @param namespace_ 무효화할 네임스페이스
     */
    void Flush(string namespace_);

    /**
     * 특정 네임스페이스의 캐시 설정을 등록한다.
     */
    void RegisterNamespace(string namespace_, CacheConfig config);
}
