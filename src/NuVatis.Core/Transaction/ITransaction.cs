using System.Data.Common;

namespace NuVatis.Transaction;

/**
 * 트랜잭션 추상화 인터페이스.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public interface ITransaction : IAsyncDisposable, IDisposable {
    DbConnection? Connection { get; }
    Task<DbConnection> GetConnectionAsync(CancellationToken ct = default);
    DbConnection GetConnection();
    Task CommitAsync(CancellationToken ct = default);
    void Commit();
    Task RollbackAsync(CancellationToken ct = default);
    void Rollback();
}
