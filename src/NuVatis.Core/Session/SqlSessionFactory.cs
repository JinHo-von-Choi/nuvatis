using System.Data.Common;
using Microsoft.Extensions.Logging;
using NuVatis.Configuration;
using NuVatis.Executor;
using NuVatis.Interceptor;
using NuVatis.Provider;
using NuVatis.Transaction;

namespace NuVatis.Session;

/**
 * ISqlSessionFactory 구현.
 * 애플리케이션 생명주기 동안 Singleton으로 사용된다.
 *
 * @author 최진호
 * @date   2026-02-24
 * @modified 2026-02-26 OpenBatchSession 구현
 */
public sealed class SqlSessionFactory : ISqlSessionFactory {
    private readonly IDbProvider _provider;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly InterceptorPipeline _interceptorPipeline = new();
    private Func<Type, ISqlSession, object>? _mapperFactory;

    public NuVatisConfiguration Configuration { get; }

    public SqlSessionFactory(
        NuVatisConfiguration configuration,
        IDbProvider provider,
        ILoggerFactory? loggerFactory = null) {
        Configuration  = configuration;
        _provider      = provider;
        _loggerFactory = loggerFactory;
    }

    /**
     * SG 생성 코드 또는 DI에서 Mapper 팩토리를 등록한다.
     */
    public void SetMapperFactory(Func<Type, ISqlSession, object> mapperFactory) {
        _mapperFactory = mapperFactory;
    }

    /**
     * SQL 인터셉터를 등록한다. 등록 순서대로 Before가 호출되고, After는 역순으로 호출된다.
     */
    public void AddInterceptor(ISqlInterceptor interceptor) {
        _interceptorPipeline.Add(interceptor);
    }

    public ISqlSession OpenSession(bool autoCommit = false) {
        var transaction = new AdoTransaction(
            _provider,
            Configuration.DataSource.ConnectionString,
            autoCommit);

        var executor = new SimpleExecutor(transaction, Configuration.DefaultCommandTimeout);
        var logger   = _loggerFactory?.CreateLogger<SqlSession>();

        return new SqlSession(Configuration, executor, autoCommit, _mapperFactory, logger, _interceptorPipeline);
    }

    public ISqlSession OpenReadOnlySession() {
        var transaction = new AdoTransaction(
            _provider,
            Configuration.DataSource.ConnectionString,
            autoCommit: true);

        var executor = new SimpleExecutor(transaction, Configuration.DefaultCommandTimeout);
        var logger   = _loggerFactory?.CreateLogger<SqlSession>();

        return new SqlSession(Configuration, executor, autoCommit: true, _mapperFactory, logger, _interceptorPipeline);
    }

    public ISqlSession OpenBatchSession() {
        var transaction = new AdoTransaction(
            _provider,
            Configuration.DataSource.ConnectionString,
            autoCommit: false);

        var executor       = new SimpleExecutor(transaction, Configuration.DefaultCommandTimeout);
        var batchExecutor  = new BatchExecutor(transaction);
        var logger         = _loggerFactory?.CreateLogger<SqlSession>();

        return new SqlSession(
            Configuration, executor, autoCommit: false,
            _mapperFactory, logger, _interceptorPipeline,
            batchExecutor);
    }

    /**
     * 이미 열린 DbConnection과 선택적 DbTransaction을 사용하여 세션을 생성한다.
     * 외부 커넥션 모드(ownsConnection=false)로 동작:
     * - Dispose 시 커넥션/트랜잭션 정리하지 않음
     * - Commit/Rollback 호출 무시 (외부 제어)
     * - transaction이 null이면 autoCommit 모드
     */
    public ISqlSession FromExistingConnection(DbConnection connection, DbTransaction? transaction = null) {
        var adoTransaction = AdoTransaction.FromExisting(connection, transaction);
        var executor       = new SimpleExecutor(adoTransaction, Configuration.DefaultCommandTimeout);
        var logger         = _loggerFactory?.CreateLogger<SqlSession>();
        var autoCommit     = transaction is null;

        return new SqlSession(Configuration, executor, autoCommit, _mapperFactory, logger, _interceptorPipeline);
    }
}
