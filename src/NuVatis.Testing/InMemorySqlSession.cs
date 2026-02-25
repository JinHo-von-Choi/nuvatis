using System.Runtime.CompilerServices;
using NuVatis.Mapping;
using NuVatis.Session;

namespace NuVatis.Testing;

/**
 * 테스트용 ISqlSession 구현.
 * 실제 DB 연결 없이 미리 설정된 결과를 반환하고 쿼리 호출을 캡처한다.
 *
 * @author 최진호
 * @date   2026-02-24
 * @modified 2026-02-25 SelectStream 추가 (Phase 6.1 A-1)
 */
public sealed class InMemorySqlSession : ISqlSession {

    private readonly Dictionary<string, object> _results         = new();
    private readonly Dictionary<Type, object>   _mappers         = new();
    private readonly List<CapturedQuery>        _capturedQueries = new();

    public IReadOnlyList<CapturedQuery> CapturedQueries => _capturedQueries;

    /**
     * 특정 statementId에 대한 단일 결과값을 사전 등록한다.
     */
    public void Setup<T>(string statementId, T result) {
        _results[statementId] = result!;
    }

    /**
     * 특정 statementId에 대한 리스트 결과값을 사전 등록한다.
     */
    public void SetupList<T>(string statementId, IList<T> results) {
        _results[statementId] = results;
    }

    /**
     * 테스트용 Mapper 인스턴스를 등록한다.
     */
    public void RegisterMapper<T>(T mapper) where T : class {
        _mappers[typeof(T)] = mapper;
    }

    /**
     * 캡처된 쿼리 기록을 초기화한다.
     */
    public void ClearCaptures() => _capturedQueries.Clear();

    public T? SelectOne<T>(string statementId, object? parameter = null) {
        _capturedQueries.Add(new(statementId, parameter, "SelectOne"));
        return _results.TryGetValue(statementId, out var result) ? (T?)result : default;
    }

    public Task<T?> SelectOneAsync<T>(string statementId, object? parameter = null, CancellationToken ct = default) {
        return Task.FromResult(SelectOne<T>(statementId, parameter));
    }

    public IList<T> SelectList<T>(string statementId, object? parameter = null) {
        _capturedQueries.Add(new(statementId, parameter, "SelectList"));
        return _results.TryGetValue(statementId, out var result) ? (IList<T>)result : new List<T>();
    }

    public Task<IList<T>> SelectListAsync<T>(string statementId, object? parameter = null, CancellationToken ct = default) {
        return Task.FromResult(SelectList<T>(statementId, parameter));
    }

    public async IAsyncEnumerable<T> SelectStream<T>(
        string statementId,
        object? parameter = null,
        [EnumeratorCancellation] CancellationToken ct = default) {
        _capturedQueries.Add(new(statementId, parameter, "SelectStream"));
        var list = SelectList<T>(statementId, parameter);
        foreach (var item in list) {
            ct.ThrowIfCancellationRequested();
            yield return item;
        }
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public ResultSetGroup SelectMultiple(string statementId, object? parameter = null) {
        _capturedQueries.Add(new(statementId, parameter, "SelectMultiple"));
        throw new NotSupportedException("InMemorySqlSession does not support SelectMultiple.");
    }

    public Task<ResultSetGroup> SelectMultipleAsync(
        string statementId, object? parameter = null, CancellationToken ct = default) {
        _capturedQueries.Add(new(statementId, parameter, "SelectMultipleAsync"));
        throw new NotSupportedException("InMemorySqlSession does not support SelectMultipleAsync.");
    }

    public int Insert(string statementId, object? parameter = null) {
        _capturedQueries.Add(new(statementId, parameter, "Insert"));
        return _results.TryGetValue(statementId, out var result) ? (int)result : 0;
    }

    public Task<int> InsertAsync(string statementId, object? parameter = null, CancellationToken ct = default) {
        return Task.FromResult(Insert(statementId, parameter));
    }

    public int Update(string statementId, object? parameter = null) {
        _capturedQueries.Add(new(statementId, parameter, "Update"));
        return _results.TryGetValue(statementId, out var result) ? (int)result : 0;
    }

    public Task<int> UpdateAsync(string statementId, object? parameter = null, CancellationToken ct = default) {
        return Task.FromResult(Update(statementId, parameter));
    }

    public int Delete(string statementId, object? parameter = null) {
        _capturedQueries.Add(new(statementId, parameter, "Delete"));
        return _results.TryGetValue(statementId, out var result) ? (int)result : 0;
    }

    public Task<int> DeleteAsync(string statementId, object? parameter = null, CancellationToken ct = default) {
        return Task.FromResult(Delete(statementId, parameter));
    }

    public T GetMapper<T>() where T : class {
        if (_mappers.TryGetValue(typeof(T), out var mapper)) {
            return (T)mapper;
        }
        throw new InvalidOperationException($"Mapper '{typeof(T).Name}'이 등록되지 않았습니다.");
    }

    public void Commit() { }
    public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
    public void Rollback() { }
    public Task RollbackAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default) => action();
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
