using System.Data.Common;
using NuVatis.Mapping;

namespace NuVatis.Session;

/**
 * SQL 세션 인터페이스.
 * 쿼리 실행, 트랜잭션 관리, Mapper 인스턴스 획득을 담당한다.
 * 기본 동작은 autoCommit=false (MyBatis 호환).
 *
 * Thread-safe하지 않음. 병렬 처리 시 별도 세션 사용 필요.
 *
 * @author 최진호
 * @date   2026-02-24
 * @modified 2026-02-25 SelectStream 추가 (Phase 6.1 A-1)
 */
public interface ISqlSession : IDisposable, IAsyncDisposable {

    /** 단일 행을 조회하여 T로 매핑한다. 결과가 없으면 default(T)를 반환한다. */
    T? SelectOne<T>(string statementId, object? parameter = null);
    Task<T?> SelectOneAsync<T>(string statementId, object? parameter = null, CancellationToken ct = default);

    /** 복수 행을 조회하여 IList<T>로 매핑한다. */
    IList<T> SelectList<T>(string statementId, object? parameter = null);
    Task<IList<T>> SelectListAsync<T>(string statementId, object? parameter = null, CancellationToken ct = default);

    /**
     * 대용량 결과를 IAsyncEnumerable로 스트리밍 반환한다.
     * 결과를 IList에 적재하지 않고 yield return으로 소비하여 메모리 사용을 최소화한다.
     * 열거가 완료되거나 취소될 때까지 세션의 커넥션이 유지된다.
     */
    IAsyncEnumerable<T> SelectStream<T>(string statementId, object? parameter = null, CancellationToken ct = default);

    /**
     * Multi-ResultSet 쿼리를 실행하고 ResultSetGroup을 반환한다.
     * 반환된 ResultSetGroup에서 Read/ReadList를 순서대로 호출하여 각 결과셋을 소비한다.
     * 사용 완료 후 ResultSetGroup을 반드시 Dispose해야 한다.
     */
    ResultSetGroup SelectMultiple(string statementId, object? parameter = null);
    Task<ResultSetGroup> SelectMultipleAsync(string statementId, object? parameter = null, CancellationToken ct = default);

    /** INSERT 문을 실행하고 영향받은 행 수를 반환한다. */
    int Insert(string statementId, object? parameter = null);
    Task<int> InsertAsync(string statementId, object? parameter = null, CancellationToken ct = default);

    /** UPDATE 문을 실행하고 영향받은 행 수를 반환한다. */
    int Update(string statementId, object? parameter = null);
    Task<int> UpdateAsync(string statementId, object? parameter = null, CancellationToken ct = default);

    /** DELETE 문을 실행하고 영향받은 행 수를 반환한다. */
    int Delete(string statementId, object? parameter = null);
    Task<int> DeleteAsync(string statementId, object? parameter = null, CancellationToken ct = default);

    /** 현재 트랜잭션을 커밋한다. */
    void Commit();
    Task CommitAsync(CancellationToken ct = default);

    /** 현재 트랜잭션을 롤백한다. */
    void Rollback();
    Task RollbackAsync(CancellationToken ct = default);

    /** SG가 생성한 Mapper 인스턴스를 반환한다. */
    T GetMapper<T>() where T : class;

    /**
     * 트랜잭션 내에서 작업을 실행한다.
     * 성공 시 자동 Commit, 예외 시 자동 Rollback 후 rethrow.
     */
    Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default);
}
