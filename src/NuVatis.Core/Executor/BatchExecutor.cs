using System.Data.Common;
using NuVatis.Statement;
using NuVatis.Transaction;

namespace NuVatis.Executor;

/**
 * 여러 write 쿼리를 배치로 모아 한 번에 실행하는 실행기.
 * Add()로 쿼리를 쌓고 Flush()로 일괄 실행, 총 영향 행수를 반환한다.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public sealed class BatchExecutor : IDisposable, IAsyncDisposable {
    private readonly AdoTransaction   _transaction;
    private readonly List<BatchItem>  _batch = new();
    private bool                      _disposed;

    public BatchExecutor(AdoTransaction transaction) {
        _transaction = transaction;
    }

    public int Count => _batch.Count;

    public void Add(MappedStatement statement, string sql, IReadOnlyList<DbParameter> parameters) {
        _batch.Add(new BatchItem(statement, sql, parameters));
    }

    public int Flush() {
        var connection   = _transaction.GetConnection();
        var dbTransaction = _transaction.GetDbTransaction();
        var totalAffected = 0;

        foreach (var item in _batch) {
            using var command = CreateCommand(connection, dbTransaction, item);
            totalAffected   += command.ExecuteNonQuery();
        }

        _batch.Clear();
        return totalAffected;
    }

    public async Task<int> FlushAsync(CancellationToken ct = default) {
        var connection    = await _transaction.GetConnectionAsync(ct).ConfigureAwait(false);
        var dbTransaction = _transaction.GetDbTransaction();
        var totalAffected = 0;

        foreach (var item in _batch) {
            await using var command = CreateCommand(connection, dbTransaction, item);
            totalAffected         += await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        _batch.Clear();
        return totalAffected;
    }

    public void Commit()   => _transaction.Commit();
    public Task CommitAsync(CancellationToken ct = default) => _transaction.CommitAsync(ct);
    public void Rollback()  => _transaction.Rollback();
    public Task RollbackAsync(CancellationToken ct = default) => _transaction.RollbackAsync(ct);

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
