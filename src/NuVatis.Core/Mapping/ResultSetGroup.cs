using System.Data.Common;

namespace NuVatis.Mapping;

/**
 * Multi-ResultSet 조회 결과를 순차적으로 읽는 래퍼.
 * DbDataReader.NextResult()를 통해 여러 결과셋을 순서대로 소비한다.
 *
 * Read 호출 순서가 SQL의 결과셋 순서와 반드시 일치해야 한다 (MyBatis 의미론).
 * 사용 완료 후 반드시 Dispose하여 DbDataReader/DbCommand를 정리해야 한다.
 *
 * 사용 예:
 *   await using var results = await session.SelectMultipleAsync("Dashboard.Overview", param);
 *   var summary = results.Read&lt;Summary&gt;();
 *   var details = results.ReadList&lt;Detail&gt;();
 *   var trends  = results.ReadList&lt;Trend&gt;();
 *
 * @author 최진호
 * @date   2026-02-25
 */
public sealed class ResultSetGroup : IDisposable, IAsyncDisposable {

    private readonly DbDataReader _reader;
    private readonly DbCommand    _command;
    private int  _resultSetIndex;
    private bool _firstRead = true;
    private bool _disposed;

    internal ResultSetGroup(DbDataReader reader, DbCommand command) {
        _reader  = reader;
        _command = command;
    }

    /**
     * 현재 결과셋에서 첫 번째 행을 T로 매핑하여 반환한다.
     * 결과가 없으면 default(T)를 반환한다.
     * 호출 후 내부 포인터가 다음 결과셋으로 이동한다.
     */
    public T? Read<T>() {
        EnsureNotDisposed();
        MoveToNextResultSetIfNeeded();

        T? result = default;
        if (_reader.Read()) {
            result = ColumnMapper.MapRow<T>(_reader);
        }

        AdvanceResultSet();
        return result;
    }

    /**
     * 현재 결과셋의 모든 행을 T 리스트로 매핑하여 반환한다.
     * 결과가 없으면 빈 리스트를 반환한다.
     * 호출 후 내부 포인터가 다음 결과셋으로 이동한다.
     */
    public IList<T> ReadList<T>() {
        EnsureNotDisposed();
        MoveToNextResultSetIfNeeded();

        var results = new List<T>();
        while (_reader.Read()) {
            results.Add(ColumnMapper.MapRow<T>(_reader));
        }

        AdvanceResultSet();
        return results;
    }

    /**
     * 현재 결과셋에서 첫 번째 행을 비동기로 매핑한다.
     */
    public async Task<T?> ReadAsync<T>(CancellationToken ct = default) {
        EnsureNotDisposed();
        MoveToNextResultSetIfNeeded();

        T? result = default;
        if (await _reader.ReadAsync(ct).ConfigureAwait(false)) {
            result = ColumnMapper.MapRow<T>(_reader);
        }

        await AdvanceResultSetAsync(ct).ConfigureAwait(false);
        return result;
    }

    /**
     * 현재 결과셋의 모든 행을 비동기로 매핑한다.
     */
    public async Task<IList<T>> ReadListAsync<T>(CancellationToken ct = default) {
        EnsureNotDisposed();
        MoveToNextResultSetIfNeeded();

        var results = new List<T>();
        while (await _reader.ReadAsync(ct).ConfigureAwait(false)) {
            results.Add(ColumnMapper.MapRow<T>(_reader));
        }

        await AdvanceResultSetAsync(ct).ConfigureAwait(false);
        return results;
    }

    /** 현재까지 읽은 결과셋 수 (0-based). */
    public int CurrentResultSetIndex => _resultSetIndex;

    private void MoveToNextResultSetIfNeeded() {
        if (_firstRead) {
            _firstRead = false;
            return;
        }
    }

    private void AdvanceResultSet() {
        _reader.NextResult();
        _resultSetIndex++;
    }

    private async Task AdvanceResultSetAsync(CancellationToken ct) {
        await _reader.NextResultAsync(ct).ConfigureAwait(false);
        _resultSetIndex++;
    }

    private void EnsureNotDisposed() {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose() {
        if (_disposed) return;
        _disposed = true;
        _reader.Dispose();
        _command.Dispose();
    }

    public async ValueTask DisposeAsync() {
        if (_disposed) return;
        _disposed = true;
        await _reader.DisposeAsync().ConfigureAwait(false);
        await _command.DisposeAsync().ConfigureAwait(false);
    }
}
