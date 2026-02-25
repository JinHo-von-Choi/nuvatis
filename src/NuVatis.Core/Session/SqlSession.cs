using System.Data.Common;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using NuVatis.Binding;
using NuVatis.Cache;
using NuVatis.Configuration;
using NuVatis.Executor;
using NuVatis.Interceptor;
using NuVatis.Mapping;
using NuVatis.Statement;

namespace NuVatis.Session;

/**
 * ISqlSession의 기본 구현.
 * Lazy Connection, Thread Safety 보호, autoCommit 지원.
 *
 * @author   최진호
 * @date     2026-02-24
 * @modified 2026-02-25 Stopwatch+Interceptor 반복 패턴 DRY 리팩토링
 */
public sealed class SqlSession : ISqlSession {
    private readonly NuVatisConfiguration _configuration;
    private readonly IExecutor _executor;
    private readonly Func<Type, ISqlSession, object>? _mapperFactory;
    private readonly InterceptorPipeline? _interceptorPipeline;
    private readonly ILogger? _logger;
    private readonly bool _autoCommit;

    private int _isBusy;
    private bool _committed;
    private bool _disposed;

    internal SqlSession(
        NuVatisConfiguration configuration,
        IExecutor executor,
        bool autoCommit = false,
        Func<Type, ISqlSession, object>? mapperFactory = null,
        ILogger? logger = null,
        InterceptorPipeline? interceptorPipeline = null) {
        _configuration       = configuration;
        _executor            = executor;
        _autoCommit          = autoCommit;
        _mapperFactory       = mapperFactory;
        _logger              = logger;
        _interceptorPipeline = interceptorPipeline;
    }

    public T? SelectOne<T>(string statementId, object? parameter = null) {
        EnsureNotDisposed();
        EnsureNotBusy();
        try {
            var statement = ResolveStatement(statementId);
            if (TryGetCached<T?>(statement, parameter, out var cached)) return cached;

            var (sql, parameters) = BuildSql(statement, parameter);
            var ctx               = CreateInterceptorContext(statement, sql, parameters, parameter);

            var result = ExecuteTimed(ctx,
                () => _executor.SelectOne(statement, ctx.Sql, ctx.Parameters, ColumnMapper.MapRow<T>));
            PutCache(statement, parameter, result);
            return result;
        } finally {
            ReleaseBusy();
        }
    }

    public async Task<T?> SelectOneAsync<T>(string statementId, object? parameter = null, CancellationToken ct = default) {
        EnsureNotDisposed();
        EnsureNotBusy();
        try {
            var statement = ResolveStatement(statementId);
            if (TryGetCached<T?>(statement, parameter, out var cached)) return cached;

            var (sql, parameters) = BuildSql(statement, parameter);
            var ctx               = CreateInterceptorContext(statement, sql, parameters, parameter);

            var result = await ExecuteTimedAsync(ctx,
                () => _executor.SelectOneAsync(statement, ctx.Sql, ctx.Parameters, ColumnMapper.MapRow<T>, ct), ct)
                .ConfigureAwait(false);
            PutCache(statement, parameter, result);
            return result;
        } finally {
            ReleaseBusy();
        }
    }

    public IList<T> SelectList<T>(string statementId, object? parameter = null) {
        EnsureNotDisposed();
        EnsureNotBusy();
        try {
            var statement = ResolveStatement(statementId);
            if (TryGetCached<IList<T>>(statement, parameter, out var cached)) return cached!;

            var (sql, parameters) = BuildSql(statement, parameter);
            var ctx               = CreateInterceptorContext(statement, sql, parameters, parameter);

            var result = ExecuteTimed(ctx,
                () => _executor.SelectList(statement, ctx.Sql, ctx.Parameters, ColumnMapper.MapRow<T>));
            PutCache(statement, parameter, result);
            return result;
        } finally {
            ReleaseBusy();
        }
    }

    public async Task<IList<T>> SelectListAsync<T>(string statementId, object? parameter = null, CancellationToken ct = default) {
        EnsureNotDisposed();
        EnsureNotBusy();
        try {
            var statement = ResolveStatement(statementId);
            if (TryGetCached<IList<T>>(statement, parameter, out var cached)) return cached!;

            var (sql, parameters) = BuildSql(statement, parameter);
            var ctx               = CreateInterceptorContext(statement, sql, parameters, parameter);

            var result = await ExecuteTimedAsync(ctx,
                () => _executor.SelectListAsync(statement, ctx.Sql, ctx.Parameters, ColumnMapper.MapRow<T>, ct), ct)
                .ConfigureAwait(false);
            PutCache(statement, parameter, result);
            return result;
        } finally {
            ReleaseBusy();
        }
    }

    /**
     * 대용량 결과를 IAsyncEnumerable로 스트리밍한다.
     * async iterator를 사용하므로 EnsureNotBusy/ReleaseBusy는
     * 열거 시작부터 완료(또는 취소)까지 유지된다.
     * Interceptor Before는 열거 시작 시, After는 열거 종료 시 호출된다.
     */
    public async IAsyncEnumerable<T> SelectStream<T>(
        string statementId,
        object? parameter = null,
        [EnumeratorCancellation] CancellationToken ct = default) {

        EnsureNotDisposed();
        EnsureNotBusy();

        var statement         = ResolveStatement(statementId);
        var (sql, parameters) = BuildSql(statement, parameter);
        var ctx               = CreateInterceptorContext(statement, sql, parameters, parameter);

        await RunBeforeAsync(ctx, ct).ConfigureAwait(false);
        var sw = Stopwatch.StartNew();

        try {
            await foreach (var item in _executor.SelectStream(
                statement, ctx.Sql, ctx.Parameters, ColumnMapper.MapRow<T>, ct).ConfigureAwait(false)) {
                yield return item;
            }
        } finally {
            sw.Stop();
            ctx.ElapsedMilliseconds = sw.ElapsedMilliseconds;
            await RunAfterAsync(ctx, ct).ConfigureAwait(false);
            ReleaseBusy();
        }
    }

    public ResultSetGroup SelectMultiple(string statementId, object? parameter = null) {
        EnsureNotDisposed();
        EnsureNotBusy();
        try {
            var statement         = ResolveStatement(statementId);
            var (sql, parameters) = BuildSql(statement, parameter);
            var ctx               = CreateInterceptorContext(statement, sql, parameters, parameter);

            return ExecuteTimed(ctx,
                () => _executor.SelectMultiple(statement, ctx.Sql, ctx.Parameters));
        } finally {
            ReleaseBusy();
        }
    }

    public async Task<ResultSetGroup> SelectMultipleAsync(
        string statementId, object? parameter = null, CancellationToken ct = default) {
        EnsureNotDisposed();
        EnsureNotBusy();
        try {
            var statement         = ResolveStatement(statementId);
            var (sql, parameters) = BuildSql(statement, parameter);
            var ctx               = CreateInterceptorContext(statement, sql, parameters, parameter);

            return await ExecuteTimedAsync(ctx,
                () => _executor.SelectMultipleAsync(statement, ctx.Sql, ctx.Parameters, ct), ct)
                .ConfigureAwait(false);
        } finally {
            ReleaseBusy();
        }
    }

    public int Insert(string statementId, object? parameter = null) => ExecuteWrite(statementId, parameter);
    public Task<int> InsertAsync(string statementId, object? parameter = null, CancellationToken ct = default) => ExecuteWriteAsync(statementId, parameter, ct);
    public int Update(string statementId, object? parameter = null) => ExecuteWrite(statementId, parameter);
    public Task<int> UpdateAsync(string statementId, object? parameter = null, CancellationToken ct = default) => ExecuteWriteAsync(statementId, parameter, ct);
    public int Delete(string statementId, object? parameter = null) => ExecuteWrite(statementId, parameter);
    public Task<int> DeleteAsync(string statementId, object? parameter = null, CancellationToken ct = default) => ExecuteWriteAsync(statementId, parameter, ct);

    public void Commit() {
        EnsureNotDisposed();
        _executor.Commit();
        _committed = true;
    }

    public async Task CommitAsync(CancellationToken ct = default) {
        EnsureNotDisposed();
        await _executor.CommitAsync(ct).ConfigureAwait(false);
        _committed = true;
    }

    public void Rollback() {
        EnsureNotDisposed();
        _executor.Rollback();
    }

    public async Task RollbackAsync(CancellationToken ct = default) {
        EnsureNotDisposed();
        await _executor.RollbackAsync(ct).ConfigureAwait(false);
    }

    public T GetMapper<T>() where T : class {
        EnsureNotDisposed();

        if (_mapperFactory is null) {
            throw new InvalidOperationException(
                "Mapper 팩토리가 등록되지 않았습니다. " +
                "Source Generator가 생성한 코드를 통해 세션을 구성하세요.");
        }

        return (T)_mapperFactory(typeof(T), this);
    }

    public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default) {
        EnsureNotDisposed();
        try {
            await action().ConfigureAwait(false);
            await CommitAsync(ct).ConfigureAwait(false);
        } catch {
            await RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }
    }

    public void Dispose() {
        if (_disposed) return;
        _disposed = true;

        if (!_committed && !_autoCommit) {
            _logger?.LogWarning(
                "SqlSession이 Commit() 없이 Dispose되었습니다. 변경 사항이 Rollback됩니다.");
            try { _executor.Rollback(); } catch { /* Dispose 중 예외 무시 */ }
        }

        _executor.Dispose();
    }

    public async ValueTask DisposeAsync() {
        if (_disposed) return;
        _disposed = true;

        if (!_committed && !_autoCommit) {
            _logger?.LogWarning(
                "SqlSession이 Commit() 없이 Dispose되었습니다. 변경 사항이 Rollback됩니다.");
            try { await _executor.RollbackAsync().ConfigureAwait(false); } catch { /* Dispose 중 예외 무시 */ }
        }

        await _executor.DisposeAsync().ConfigureAwait(false);
    }

    private int ExecuteWrite(string statementId, object? parameter) {
        EnsureNotDisposed();
        EnsureNotBusy();
        try {
            var statement         = ResolveStatement(statementId);
            var (sql, parameters) = BuildSql(statement, parameter);
            var ctx               = CreateInterceptorContext(statement, sql, parameters, parameter);

            var affected = ExecuteTimed(ctx,
                () => _executor.Execute(statement, ctx.Sql, ctx.Parameters),
                (c, r) => c.AffectedRows = r);
            FlushNamespaceCache(statement);
            return affected;
        } finally {
            ReleaseBusy();
        }
    }

    private async Task<int> ExecuteWriteAsync(string statementId, object? parameter, CancellationToken ct) {
        EnsureNotDisposed();
        EnsureNotBusy();
        try {
            var statement         = ResolveStatement(statementId);
            var (sql, parameters) = BuildSql(statement, parameter);
            var ctx               = CreateInterceptorContext(statement, sql, parameters, parameter);

            var affected = await ExecuteTimedAsync(ctx,
                () => _executor.ExecuteAsync(statement, ctx.Sql, ctx.Parameters, ct), ct,
                (c, r) => c.AffectedRows = r)
                .ConfigureAwait(false);
            FlushNamespaceCache(statement);
            return affected;
        } finally {
            ReleaseBusy();
        }
    }

    /**
     * 2차 캐시에서 결과를 조회한다.
     * UseCache가 true이고 CacheProvider가 설정되어 있으며 캐시 히트 시 true를 반환한다.
     */
    private bool TryGetCached<T>(MappedStatement statement, object? parameter, out T? result) {
        result = default;
        var cache = _configuration.CacheProvider;
        if (cache is null || !statement.UseCache) return false;

        var key    = CacheKey.Generate(statement.FullId, parameter);
        var cached = cache.Get(statement.Namespace, key);
        if (cached is null) return false;

        result = (T?)cached;
        return true;
    }

    /**
     * 실행 결과를 2차 캐시에 저장한다.
     */
    private void PutCache(MappedStatement statement, object? parameter, object? value) {
        var cache = _configuration.CacheProvider;
        if (cache is null || !statement.UseCache || value is null) return;

        var key = CacheKey.Generate(statement.FullId, parameter);
        cache.Put(statement.Namespace, key, value);
    }

    /**
     * Write 연산 후 해당 namespace의 캐시를 무효화한다.
     */
    private void FlushNamespaceCache(MappedStatement statement) {
        _configuration.CacheProvider?.Flush(statement.Namespace);
    }

    private MappedStatement ResolveStatement(string statementId) {
        if (_configuration.Statements.TryGetValue(statementId, out var statement)) {
            return statement;
        }

        throw new InvalidOperationException(
            $"Statement '{statementId}'를 찾을 수 없습니다.");
    }

    /**
     * SQL 내의 #{paramName} 바인딩을 처리하여 실행 가능한 SQL과 파라미터를 반환한다.
     * #{} 패턴이 없으면 원본 SQL을 그대로 반환한다.
     */
    private static (string Sql, IReadOnlyList<DbParameter> Parameters) BuildSql(
        MappedStatement statement, object? parameter) {
        var (sql, parameters) = ParameterBinder.Bind(statement.SqlSource, parameter);
        return (sql, parameters);
    }

    private void EnsureNotDisposed() {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private void EnsureNotBusy() {
        if (Interlocked.CompareExchange(ref _isBusy, 1, 0) != 0) {
            throw new InvalidOperationException(
                "ISqlSession은 동시 접근을 지원하지 않습니다. " +
                "병렬 처리가 필요한 경우 ISqlSessionFactory.OpenSession()으로 별도 세션을 생성하세요.");
        }
    }

    private void ReleaseBusy() {
        Interlocked.Exchange(ref _isBusy, 0);
    }

    private InterceptorContext CreateInterceptorContext(
        MappedStatement statement,
        string sql,
        IReadOnlyList<DbParameter> parameters,
        object? parameter) {

        return new InterceptorContext {
            StatementId   = statement.FullId,
            Sql           = sql,
            Parameters    = parameters,
            Parameter     = parameter,
            StatementType = statement.Type
        };
    }

    /**
     * 동기 실행의 Stopwatch 측정 + Interceptor Before/After 호출을 통합한다.
     * enrichContext는 RunAfter 직전에 ctx를 보강할 때 사용한다 (예: AffectedRows 설정).
     */
    private TResult ExecuteTimed<TResult>(
        InterceptorContext ctx,
        Func<TResult> execute,
        Action<InterceptorContext, TResult>? enrichContext = null) {

        RunBefore(ctx);
        var sw = Stopwatch.StartNew();
        try {
            var result             = execute();
            sw.Stop();
            ctx.ElapsedMilliseconds = sw.ElapsedMilliseconds;
            enrichContext?.Invoke(ctx, result);
            RunAfter(ctx);
            return result;
        } catch (Exception ex) {
            sw.Stop();
            ctx.ElapsedMilliseconds = sw.ElapsedMilliseconds;
            ctx.Exception           = ex;
            RunAfter(ctx);
            throw;
        }
    }

    /**
     * 비동기 실행의 Stopwatch 측정 + Interceptor Before/After 호출을 통합한다.
     */
    private async Task<TResult> ExecuteTimedAsync<TResult>(
        InterceptorContext ctx,
        Func<Task<TResult>> execute,
        CancellationToken ct,
        Action<InterceptorContext, TResult>? enrichContext = null) {

        await RunBeforeAsync(ctx, ct).ConfigureAwait(false);
        var sw = Stopwatch.StartNew();
        try {
            var result             = await execute().ConfigureAwait(false);
            sw.Stop();
            ctx.ElapsedMilliseconds = sw.ElapsedMilliseconds;
            enrichContext?.Invoke(ctx, result);
            await RunAfterAsync(ctx, ct).ConfigureAwait(false);
            return result;
        } catch (Exception ex) {
            sw.Stop();
            ctx.ElapsedMilliseconds = sw.ElapsedMilliseconds;
            ctx.Exception           = ex;
            await RunAfterAsync(ctx, ct).ConfigureAwait(false);
            throw;
        }
    }

    private void RunBefore(InterceptorContext ctx) {
        if (_interceptorPipeline is { HasInterceptors: true }) {
            _interceptorPipeline.ExecuteBefore(ctx);
        }
    }

    private void RunAfter(InterceptorContext ctx) {
        if (_interceptorPipeline is { HasInterceptors: true }) {
            _interceptorPipeline.ExecuteAfter(ctx);
        }
    }

    private async Task RunBeforeAsync(InterceptorContext ctx, CancellationToken ct) {
        if (_interceptorPipeline is { HasInterceptors: true }) {
            await _interceptorPipeline.ExecuteBeforeAsync(ctx, ct).ConfigureAwait(false);
        }
    }

    private async Task RunAfterAsync(InterceptorContext ctx, CancellationToken ct) {
        if (_interceptorPipeline is { HasInterceptors: true }) {
            await _interceptorPipeline.ExecuteAfterAsync(ctx, ct).ConfigureAwait(false);
        }
    }
}
