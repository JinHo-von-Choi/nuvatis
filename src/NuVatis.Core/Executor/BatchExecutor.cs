using System.Data.Common;
using NuVatis.Statement;
using NuVatis.Transaction;

namespace NuVatis.Executor;

/**
 * 여러 write 쿼리를 배치로 모아 한 번에 실행하는 실행기.
 * Add()로 쿼리를 쌓고 Flush()로 일괄 실행, 총 영향 행수를 반환한다.
 *
 * .NET 8+에서 DbConnection.CanCreateBatch=true인 프로바이더(Npgsql, MySqlConnector)는
 * ADO.NET DbBatch를 사용하여 단일 라운드트립으로 실행한다.
 * 미지원 프로바이더에서는 순차 DbCommand 실행으로 폴백한다.
 *
 * @author 최진호
 * @date   2026-02-24
 * @modified 2026-02-26 DbBatch 네이티브 배치 경로 추가
 */
public sealed class BatchExecutor : IDisposable, IAsyncDisposable {
    private readonly AdoTransaction  _transaction;
    private readonly List<BatchItem> _batch = new();
    private bool                     _disposed;

    public BatchExecutor(AdoTransaction transaction) {
        _transaction = transaction;
    }

    public int Count => _batch.Count;

    public void Add(MappedStatement statement, string sql, IReadOnlyList<DbParameter> parameters) {
        _batch.Add(new BatchItem(statement, sql, parameters));
    }

    public int Flush() {
        if (_batch.Count == 0) return 0;

        var connection    = _transaction.GetConnection();
        var dbTransaction = _transaction.GetDbTransaction();

#if NET8_0_OR_GREATER
        if (connection.CanCreateBatch) {
            return FlushWithDbBatch(connection, dbTransaction);
        }
#endif

        return FlushSequential(connection, dbTransaction);
    }

    public async Task<int> FlushAsync(CancellationToken ct = default) {
        if (_batch.Count == 0) return 0;

        var connection    = await _transaction.GetConnectionAsync(ct).ConfigureAwait(false);
        var dbTransaction = _transaction.GetDbTransaction();

#if NET8_0_OR_GREATER
        if (connection.CanCreateBatch) {
            return await FlushWithDbBatchAsync(connection, dbTransaction, ct).ConfigureAwait(false);
        }
#endif

        return await FlushSequentialAsync(connection, dbTransaction, ct).ConfigureAwait(false);
    }

    public void Commit()   => _transaction.Commit();
    public Task CommitAsync(CancellationToken ct = default) => _transaction.CommitAsync(ct);
    public void Rollback()  => _transaction.Rollback();
    public Task RollbackAsync(CancellationToken ct = default) => _transaction.RollbackAsync(ct);

#if NET8_0_OR_GREATER
    /**
     * DbBatch를 사용하여 단일 라운드트립으로 모든 쿼리를 실행한다.
     * Npgsql, MySqlConnector 등 CanCreateBatch=true인 프로바이더에서 사용.
     * O(1) 라운드트립 (전체 배치를 하나의 네트워크 패킷으로 전송).
     */
    private int FlushWithDbBatch(DbConnection connection, DbTransaction? dbTransaction) {
        using var batch = connection.CreateBatch();
        batch.Transaction = dbTransaction;

        foreach (var item in _batch) {
            var batchCmd         = batch.CreateBatchCommand();
            batchCmd.CommandText = item.Sql;

            foreach (var p in item.Parameters) {
                var param           = batchCmd.CreateParameter();
                param.ParameterName = p.ParameterName;
                param.Value         = p.Value ?? DBNull.Value;
                batchCmd.Parameters.Add(param);
            }

            batch.BatchCommands.Add(batchCmd);
        }

        var totalAffected = batch.ExecuteNonQuery();
        _batch.Clear();
        return totalAffected;
    }

    private async Task<int> FlushWithDbBatchAsync(
        DbConnection connection, DbTransaction? dbTransaction, CancellationToken ct) {

        await using var batch = connection.CreateBatch();
        batch.Transaction     = dbTransaction;

        foreach (var item in _batch) {
            var batchCmd         = batch.CreateBatchCommand();
            batchCmd.CommandText = item.Sql;

            foreach (var p in item.Parameters) {
                var param           = batchCmd.CreateParameter();
                param.ParameterName = p.ParameterName;
                param.Value         = p.Value ?? DBNull.Value;
                batchCmd.Parameters.Add(param);
            }

            batch.BatchCommands.Add(batchCmd);
        }

        var totalAffected = await batch.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        _batch.Clear();
        return totalAffected;
    }
#endif

    private int FlushSequential(DbConnection connection, DbTransaction? dbTransaction) {
        var totalAffected = 0;

        foreach (var item in _batch) {
            using var command = CreateCommand(connection, dbTransaction, item);
            totalAffected   += command.ExecuteNonQuery();
        }

        _batch.Clear();
        return totalAffected;
    }

    private async Task<int> FlushSequentialAsync(
        DbConnection connection, DbTransaction? dbTransaction, CancellationToken ct) {

        var totalAffected = 0;

        foreach (var item in _batch) {
            await using var command = CreateCommand(connection, dbTransaction, item);
            totalAffected         += await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        _batch.Clear();
        return totalAffected;
    }

    private static DbCommand CreateCommand(
        DbConnection connection,
        DbTransaction? dbTransaction,
        BatchItem item) {

        var command         = connection.CreateCommand();
        command.CommandText = item.Sql;
        command.Transaction = dbTransaction;

        foreach (var p in item.Parameters) {
            var param           = command.CreateParameter();
            param.ParameterName = p.ParameterName;
            param.Value         = p.Value ?? DBNull.Value;
            command.Parameters.Add(param);
        }

        return command;
    }

    public void Dispose() {
        if (_disposed) return;
        _disposed = true;
        _transaction.Dispose();
    }

    public async ValueTask DisposeAsync() {
        if (_disposed) return;
        _disposed = true;
        await _transaction.DisposeAsync().ConfigureAwait(false);
    }

    private sealed record BatchItem(
        MappedStatement Statement,
        string Sql,
        IReadOnlyList<DbParameter> Parameters);
}
