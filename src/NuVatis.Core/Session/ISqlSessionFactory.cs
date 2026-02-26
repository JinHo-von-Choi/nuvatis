using System.Data.Common;
using NuVatis.Configuration;

namespace NuVatis.Session;

/**
 * SqlSession을 생성하는 팩토리 인터페이스.
 * 애플리케이션 생명주기 동안 Singleton으로 유지된다.
 *
 * @author 최진호
 * @date   2026-02-24
 * @modified 2026-02-26 OpenBatchSession 추가
 */
public interface ISqlSessionFactory {

    /** 새 세션을 생성한다. autoCommit=false면 명시적 Commit 필요. */
    ISqlSession OpenSession(bool autoCommit = false);

    /** autoCommit=true인 읽기 전용 세션을 생성한다. */
    ISqlSession OpenReadOnlySession();

    /**
     * 배치 모드 세션을 생성한다.
     * Insert/Update/Delete 호출 시 쿼리를 배치에 누적하고,
     * FlushStatements() 호출 시 일괄 실행한다.
     * Select 연산은 즉시 실행된다.
     * Commit 전 미처리 배치가 있으면 자동 Flush 후 Commit한다.
     */
    ISqlSession OpenBatchSession();

    /**
     * 이미 열린 DbConnection과 선택적 DbTransaction을 사용하여 세션을 생성한다.
     * EF Core 등 다른 프레임워크와 동일 커넥션/트랜잭션을 공유할 때 사용.
     *
     * 반환된 세션의 특성:
     * - Dispose 시 커넥션/트랜잭션을 닫지 않음 (외부에서 관리)
     * - Commit/Rollback 호출을 무시함 (외부에서 제어)
     * - transaction이 null이면 autoCommit 모드로 동작
     *
     * @param connection 이미 Open 상태인 DbConnection
     * @param transaction 외부에서 시작한 DbTransaction (null 가능)
     */
    ISqlSession FromExistingConnection(DbConnection connection, DbTransaction? transaction = null);

    NuVatisConfiguration Configuration { get; }
}
