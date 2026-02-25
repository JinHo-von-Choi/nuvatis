using System.Data;
using System.Data.Common;
using NuVatis.Provider;

namespace NuVatis.Transaction;

/**
 * ADO.NET 기반 트랜잭션 구현.
 * 두 가지 모드를 지원한다:
 *
 * 1. 일반 모드 (ownsConnection=true): Lazy Connection 전략으로 커넥션을 직접 관리.
 *    Dispose 시 커넥션/트랜잭션을 정리한다.
 *
 * 2. 외부 커넥션 모드 (ownsConnection=false): 이미 열린 커넥션/트랜잭션을 수용.
 *    Dispose 시 커넥션/트랜잭션을 정리하지 않는다.
 *    Commit/Rollback도 무시한다 (외부에서 제어).
 *    EF Core 등 다른 프레임워크와 동일 트랜잭션을 공유할 때 사용.
 *
 * @author 최진호
 * @date   2026-02-24
 * @modified 2026-02-25 외부 커넥션 모드 추가 (Phase 6.2 B-1)
 */
public sealed class AdoTransaction : ITransaction {
    private readonly IDbProvider? _provider;
    private readonly string? _connectionString;
    private readonly bool _autoCommit;
    private readonly bool _ownsConnection;

    private DbConnection? _connection;
    private DbTransaction? _transaction;
    private bool _disposed;

    public DbConnection? Connection => _connection;

    /**
     * 일반 모드: 커넥션을 직접 생성하고 관리한다.
     */
    public AdoTransaction(IDbProvider provider, string connectionString, bool autoCommit = false) {
        _provider         = provider;
        _connectionString = connectionString;
        _autoCommit       = autoCommit;
        _ownsConnection   = true;
    }

    /**
     * 외부 커넥션 모드용 private 생성자.
     * FromExisting 팩토리 메서드를 통해서만 호출된다.
     */
    private AdoTransaction(DbConnection connection, DbTransaction? transaction) {
        _connection     = connection;
        _transaction    = transaction;
        _ownsConnection = false;
        _autoCommit     = transaction is null;
    }

    /**
     * 이미 열린 DbConnection과 선택적 DbTransaction을 수용하는 팩토리 메서드.
     * EF Core의 DbContext.Database.GetDbConnection() 등에서 추출한 커넥션을 주입할 때 사용.
     *
     * ownsConnection=false로 동작하므로:
     * - Dispose 시 커넥션/트랜잭션을 닫지 않는다
     * - Commit/Rollback 호출을 무시한다 (외부 제어)
     *
     * @param connection 이미 Open 상태인 DbConnection
     * @param transaction 외부에서 시작한 DbTransaction (null 가능)
     * @throws ArgumentNullException connection이 null일 때
     * @throws InvalidOperationException connection이 Open 상태가 아닐 때
     */
    public static AdoTransaction FromExisting(DbConnection connection, DbTransaction? transaction = null) {
        ArgumentNullException.ThrowIfNull(connection);

        if (connection.State != ConnectionState.Open) {
            throw new InvalidOperationException(
                $"외부 커넥션은 Open 상태여야 합니다. 현재 상태: {connection.State}");
        }

        return new AdoTransaction(connection, transaction);
    }

    public DbConnection GetConnection() {
        if (_disposed) throw new ObjectDisposedException(nameof(AdoTransaction));

        if (_connection is null) {
            _connection = _provider!.CreateConnection(_connectionString!);
            _connection.Open();

            if (!_autoCommit) {
                _transaction = _connection.BeginTransaction();
            }
        }

        return _connection;
    }

    public async Task<DbConnection> GetConnectionAsync(CancellationToken ct = default) {
        if (_disposed) throw new ObjectDisposedException(nameof(AdoTransaction));

        if (_connection is null) {
            _connection = _provider!.CreateConnection(_connectionString!);
            await _connection.OpenAsync(ct).ConfigureAwait(false);

            if (!_autoCommit) {
                _transaction = await _connection.BeginTransactionAsync(ct).ConfigureAwait(false);
            }
        }

        return _connection;
    }

    public DbTransaction? GetDbTransaction() => _transaction;

    public void Commit() {
        if (!_ownsConnection) return;

        if (_transaction is not null) {
            _transaction.Commit();
            _transaction.Dispose();
            _transaction = null;
        }
    }

    public async Task CommitAsync(CancellationToken ct = default) {
        if (!_ownsConnection) return;

        if (_transaction is not null) {
            await _transaction.CommitAsync(ct).ConfigureAwait(false);
            await _transaction.DisposeAsync().ConfigureAwait(false);
            _transaction = null;
        }
    }

    public void Rollback() {
        if (!_ownsConnection) return;

        if (_transaction is not null) {
            _transaction.Rollback();
            _transaction.Dispose();
            _transaction = null;
        }
    }

    public async Task RollbackAsync(CancellationToken ct = default) {
        if (!_ownsConnection) return;

        if (_transaction is not null) {
            await _transaction.RollbackAsync(ct).ConfigureAwait(false);
            await _transaction.DisposeAsync().ConfigureAwait(false);
            _transaction = null;
        }
    }

    public void Dispose() {
        if (_disposed) return;
        _disposed = true;

        if (_ownsConnection) {
            _transaction?.Dispose();
            _connection?.Dispose();
        }
    }

    public async ValueTask DisposeAsync() {
        if (_disposed) return;
        _disposed = true;

        if (_ownsConnection) {
            if (_transaction is not null) {
                await _transaction.DisposeAsync().ConfigureAwait(false);
            }
            if (_connection is not null) {
                await _connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
