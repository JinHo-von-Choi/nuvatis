using System.Data.Common;
using NuVatis.Mapping;
using NuVatis.Statement;

namespace NuVatis.Executor;

/**
 * SQL 실행 엔진 인터페이스.
 * DbCommand를 생성하고 실행하는 책임을 담당한다.
 *
 * @author 최진호
 * @date   2026-02-24
 * @modified 2026-02-25 SelectStream, SelectMultiple 추가 (Phase 6.1/6.4)
 */
public interface IExecutor : IDisposable, IAsyncDisposable {
    T? SelectOne<T>(
        MappedStatement statement,
        string sql,
        IReadOnlyList<DbParameter> parameters,
        Func<DbDataReader, T> mapper);

    Task<T?> SelectOneAsync<T>(
        MappedStatement statement,
        string sql,
        IReadOnlyList<DbParameter> parameters,
        Func<DbDataReader, T> mapper,
        CancellationToken ct = default);

    IList<T> SelectList<T>(
        MappedStatement statement,
        string sql,
        IReadOnlyList<DbParameter> parameters,
        Func<DbDataReader, T> mapper);

    Task<IList<T>> SelectListAsync<T>(
        MappedStatement statement,
        string sql,
        IReadOnlyList<DbParameter> parameters,
        Func<DbDataReader, T> mapper,
        CancellationToken ct = default);

    /**
     * 대용량 결과를 IAsyncEnumerable로 스트리밍 반환한다.
     * DbDataReader를 yield return으로 소비하여 메모리 사용을 최소화한다.
     * 열거가 완료되거나 취소될 때까지 커넥션이 유지된다.
     */
    IAsyncEnumerable<T> SelectStream<T>(
        MappedStatement statement,
        string sql,
        IReadOnlyList<DbParameter> parameters,
        Func<DbDataReader, T> mapper,
        CancellationToken ct = default);

    /**
     * Multi-ResultSet 쿼리를 실행하고 ResultSetGroup을 반환한다.
     * 반환된 ResultSetGroup의 소유권은 호출자에게 있다 (Dispose 필수).
     */
    ResultSetGroup SelectMultiple(
        MappedStatement statement,
        string sql,
        IReadOnlyList<DbParameter> parameters);

    /**
     * Multi-ResultSet 쿼리를 비동기로 실행한다.
     */
    Task<ResultSetGroup> SelectMultipleAsync(
        MappedStatement statement,
        string sql,
        IReadOnlyList<DbParameter> parameters,
        CancellationToken ct = default);

    int Execute(
        MappedStatement statement,
        string sql,
        IReadOnlyList<DbParameter> parameters);

    Task<int> ExecuteAsync(
        MappedStatement statement,
        string sql,
        IReadOnlyList<DbParameter> parameters,
        CancellationToken ct = default);

    void Commit();
    Task CommitAsync(CancellationToken ct = default);
    void Rollback();
    Task RollbackAsync(CancellationToken ct = default);
}
