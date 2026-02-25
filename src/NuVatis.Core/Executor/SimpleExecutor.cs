using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using NuVatis.Mapping;
using NuVatis.Statement;
using NuVatis.Transaction;

namespace NuVatis.Executor;

/**
 * 기본 SQL 실행기. ADO.NET DbCommand를 직접 생성하고 실행한다.
 * 매 쿼리마다 새 DbCommand를 생성하는 단순 전략.
 *
 * @author 최진호
 * @date   2026-02-24
 * @modified 2026-02-25 CommandTimeout 지원, SelectStream 구현 (Phase 6.1)
 */
public sealed class SimpleExecutor : IExecutor {
    private readonly AdoTransaction _transaction;
    private readonly int?           _defaultCommandTimeout;
    private bool                    _disposed;

    public SimpleExecutor(AdoTransaction transaction, int? defaultCommandTimeout = null) {
        _transaction           = transaction;
        _defaultCommandTimeout = defaultCommandTimeout;
    }

    public T? SelectOne<T>(
        MappedStatement statement,
        string sql,
        IReadOnlyList<DbParameter> parameters,
        Func<DbDataReader, T> mapper) {

        var connection       = _transaction.GetConnection();
        var effectiveTimeout = ResolveTimeout(statement);
        using var command    = CreateCommand(connection, sql, parameters, effectiveTimeout);

        using var reader = command.ExecuteReader();
        return reader.Read() ? mapper(reader) : default;
    }

    public async Task<T?> SelectOneAsync<T>(
        MappedStatement statement,
        string sql,
        IReadOnlyList<DbParameter> parameters,
        Func<DbDataReader, T> mapper,
        CancellationToken ct = default) {

        var connection       = await _transaction.GetConnectionAsync(ct).ConfigureAwait(false);
        var effectiveTimeout = ResolveTimeout(statement);
        await using var command = CreateCommand(connection, sql, parameters, effectiveTimeout);

        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? mapper(reader) : default;
    }

    public IList<T> SelectList<T>(
        MappedStatement statement,
        string sql,
        IReadOnlyList<DbParameter> parameters,
        Func<DbDataReader, T> mapper) {

        var connection       = _transaction.GetConnection();
        var effectiveTimeout = ResolveTimeout(statement);
        using var command    = CreateCommand(connection, sql, parameters, effectiveTimeout);

        using var reader = command.ExecuteReader();
        var results      = new List<T>();
        while (reader.Read()) {
            results.Add(mapper(reader));
        }
        return results;
    }

    public async Task<IList<T>> SelectListAsync<T>(
        MappedStatement statement,
        string sql,
        IReadOnlyList<DbParameter> parameters,
        Func<DbDataReader, T> mapper,
        CancellationToken ct = default) {

        var connection       = await _transaction.GetConnectionAsync(ct).ConfigureAwait(false);
        var effectiveTimeout = ResolveTimeout(statement);
        await using var command = CreateCommand(connection, sql, parameters, effectiveTimeout);

        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results            = new List<T>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) {
            results.Add(mapper(reader));
        }
        return results;
    }

    /**
     * 대용량 결과를 IAsyncEnumerable로 스트리밍한다.
     * CommandBehavior.SequentialAccess로 메모리 사용을 최소화하고,
     * DbDataReader/DbCommand의 수명은 열거 완료 또는 취소 시 정리된다.
     */
    public async IAsyncEnumerable<T> SelectStream<T>(
        MappedStatement statement,
        string sql,
        IReadOnlyList<DbParameter> parameters,
        Func<DbDataReader, T> mapper,
        [EnumeratorCancellation] CancellationToken ct = default) {

        var connection       = await _transaction.GetConnectionAsync(ct).ConfigureAwait(false);
        var effectiveTimeout = ResolveTimeout(statement);
        var command          = CreateCommand(connection, sql, parameters, effectiveTimeout);

        DbDataReader? reader = null;
        try {
            reader = await command.ExecuteReaderAsync(
                CommandBehavior.SequentialAccess, ct).ConfigureAwait(false);

            while (await reader.ReadAsync(ct).ConfigureAwait(false)) {
                yield return mapper(reader);
            }
        } finally {
            if (reader is not null) {
                await reader.DisposeAsync().ConfigureAwait(false);
            }
            await command.DisposeAsync().ConfigureAwait(false);
        }
    }

    public ResultSetGroup SelectMultiple(
        MappedStatement statement,
        string sql,
        IReadOnlyList<DbParameter> parameters) {

        var connection       = _transaction.GetConnection();
        var effectiveTimeout = ResolveTimeout(statement);
        var command          = CreateCommand(connection, sql, parameters, effectiveTimeout);

        var reader = command.ExecuteReader();
        return new ResultSetGroup(reader, command);
    }

    public async Task<ResultSetGroup> SelectMultipleAsync(
        MappedStatement statement,
        string sql,
        IReadOnlyList<DbParameter> parameters,
        CancellationToken ct = default) {

        var connection       = await _transaction.GetConnectionAsync(ct).ConfigureAwait(false);
        var effectiveTimeout = ResolveTimeout(statement);
        var command          = CreateCommand(connection, sql, parameters, effectiveTimeout);

        var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return new ResultSetGroup(reader, command);
    }

    public int Execute(
        MappedStatement statement,
        string sql,
        IReadOnlyList<DbParameter> parameters) {

        var connection       = _transaction.GetConnection();
        var effectiveTimeout = ResolveTimeout(statement);
        using var command    = CreateCommand(connection, sql, parameters, effectiveTimeout);
        return command.ExecuteNonQuery();
    }

    public async Task<int> ExecuteAsync(
        MappedStatement statement,
        string sql,
        IReadOnlyList<DbParameter> parameters,
        CancellationToken ct = default) {

        var connection       = await _transaction.GetConnectionAsync(ct).ConfigureAwait(false);
        var effectiveTimeout = ResolveTimeout(statement);
        await using var command = CreateCommand(connection, sql, parameters, effectiveTimeout);
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public void Commit() => _transaction.Commit();
    public Task CommitAsync(CancellationToken ct) => _transaction.CommitAsync(ct);
    public void Rollback() => _transaction.Rollback();
    public Task RollbackAsync(CancellationToken ct) => _transaction.RollbackAsync(ct);

    /**
     * Statement timeout > DefaultCommandTimeout > ADO.NET 기본값(30초) 우선순위 적용.
     */
    private int? ResolveTimeout(MappedStatement statement) {
        return statement.CommandTimeout ?? _defaultCommandTimeout;
    }

    /**
     * DbCommand를 생성하고 트랜잭션/파라미터/타임아웃을 설정한다.
     * DbTransaction은 AdoTransaction에서 가져와 명시적으로 설정한다.
     * 외부 커넥션 공유 시 커맨드가 올바른 트랜잭션에 참여하기 위해 필수.
     */
    private DbCommand CreateCommand(
        DbConnection connection,
        string sql,
        IReadOnlyList<DbParameter> parameters,
        int? commandTimeout = null) {

        var command         = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = _transaction.GetDbTransaction();

        if (commandTimeout.HasValue) {
            command.CommandTimeout = commandTimeout.Value;
        }

        for (var i = 0; i < parameters.Count; i++) {
            var source = parameters[i];
            var param  = command.CreateParameter();
            param.ParameterName = source.ParameterName;
            param.Value         = source.Value ?? DBNull.Value;
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
}
