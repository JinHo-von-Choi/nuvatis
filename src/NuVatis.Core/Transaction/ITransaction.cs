using System.Data.Common;

namespace NuVatis.Transaction;

/**
 * 트랜잭션 추상화 인터페이스.
 *
 * @author 최진호
 * @date   2026-02-24
 */
/// <summary>트랜잭션 추상화 인터페이스. 커넥션 획득 및 커밋/롤백 제어를 담당한다.</summary>
public interface ITransaction : IAsyncDisposable, IDisposable {
    /// <summary>현재 활성 커넥션. 트랜잭션이 시작되지 않은 경우 null일 수 있다.</summary>
    DbConnection? Connection { get; }
    /// <summary>커넥션을 비동기적으로 획득한다. 필요 시 새 커넥션을 열고 반환한다.</summary>
    Task<DbConnection> GetConnectionAsync(CancellationToken ct = default);
    /// <summary>커넥션을 동기적으로 획득한다. 필요 시 새 커넥션을 열고 반환한다.</summary>
    DbConnection GetConnection();
    /// <summary>트랜잭션을 비동기적으로 커밋한다.</summary>
    Task CommitAsync(CancellationToken ct = default);
    /// <summary>트랜잭션을 동기적으로 커밋한다.</summary>
    void Commit();
    /// <summary>트랜잭션을 비동기적으로 롤백한다.</summary>
    Task RollbackAsync(CancellationToken ct = default);
    /// <summary>트랜잭션을 동기적으로 롤백한다.</summary>
    void Rollback();
}
