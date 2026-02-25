using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using NuVatis.Configuration;
using NuVatis.Executor;
using NuVatis.Interceptor;
using NuVatis.Provider;
using NuVatis.Session;
using NuVatis.Statement;
using NuVatis.Transaction;
using StatementType = NuVatis.Statement.StatementType;

namespace NuVatis.Tests;

/**
 * 외부 커넥션/트랜잭션 공유 (Phase 6.2 B-1) 테스트.
 * AdoTransaction.FromExisting과 SqlSessionFactory.FromExistingConnection의
 * 전체 동작을 SQLite 인메모리 DB로 검증한다.
 *
 * @author 최진호
 * @date   2026-02-25
 */
public class ExternalConnectionTests : IDisposable {

    private readonly SqliteConnection _keepAlive;
    private readonly SqliteProvider   _provider;
    private readonly NuVatisConfiguration _config;

    private sealed class SqliteProvider : IDbProvider {
        private readonly string _connStr;
        public SqliteProvider(string connStr) { _connStr = connStr; }
        public string Name => "Sqlite";
        public DbConnection CreateConnection(string connectionString) => new SqliteConnection(_connStr);
        public string ParameterPrefix => "@";
        public string GetParameterName(int index) => $"@p{index}";
        public string WrapIdentifier(string name) => $"\"{name}\"";
    }

    private static readonly MappedStatement SelectAll = new() {
        Id        = "All",
        Namespace = "User",
        Type      = StatementType.Select,
        SqlSource = "SELECT id, name, age FROM users ORDER BY id"
    };

    private static readonly MappedStatement SelectById = new() {
        Id        = "ById",
        Namespace = "User",
        Type      = StatementType.Select,
        SqlSource = "SELECT id, name, age FROM users WHERE id = @p0"
    };

    private static readonly MappedStatement InsertUser = new() {
        Id        = "Insert",
        Namespace = "User",
        Type      = StatementType.Insert,
        SqlSource = "INSERT INTO users (id, name, age) VALUES (@p0, @p1, @p2)"
    };

    private static readonly MappedStatement SelectCount = new() {
        Id        = "Count",
        Namespace = "User",
        Type      = StatementType.Select,
        SqlSource = "SELECT COUNT(*) FROM users"
    };

    public ExternalConnectionTests() {
        _keepAlive = new SqliteConnection("Data Source=ExternalConnTests;Mode=Memory;Cache=Shared");
        _keepAlive.Open();

        using var cmd = _keepAlive.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS users (
                id   INTEGER PRIMARY KEY,
                name TEXT NOT NULL,
                age  INTEGER NOT NULL
            );
            DELETE FROM users;
            INSERT INTO users (id, name, age) VALUES (1, 'Alice', 30);
            INSERT INTO users (id, name, age) VALUES (2, 'Bob', 25);
            INSERT INTO users (id, name, age) VALUES (3, 'Charlie', 35);
        """;
        cmd.ExecuteNonQuery();

        _provider = new SqliteProvider("Data Source=ExternalConnTests;Mode=Memory;Cache=Shared");
        _config   = new NuVatisConfiguration {
            DataSource = new DataSourceConfig {
                ProviderName     = "Sqlite",
                ConnectionString = "Data Source=ExternalConnTests;Mode=Memory;Cache=Shared"
            },
            Statements = new Dictionary<string, MappedStatement> {
                [SelectAll.FullId]   = SelectAll,
                [SelectById.FullId]  = SelectById,
                [InsertUser.FullId]  = InsertUser,
                [SelectCount.FullId] = SelectCount
            }
        };
    }

    /** --- AdoTransaction.FromExisting 단위 테스트 --- */

    [Fact]
    public void FromExisting_NullConnection_ThrowsArgumentNullException() {
        Assert.Throws<ArgumentNullException>(() =>
            AdoTransaction.FromExisting(null!));
    }

    [Fact]
    public void FromExisting_ClosedConnection_ThrowsInvalidOperationException() {
        using var conn = new SqliteConnection("Data Source=:memory:");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            AdoTransaction.FromExisting(conn));

        Assert.Contains("Open", ex.Message);
    }

    [Fact]
    public void FromExisting_OpenConnection_ReturnsTransaction() {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();

        using var tx = AdoTransaction.FromExisting(conn);

        Assert.NotNull(tx);
        Assert.Same(conn, tx.Connection);
        Assert.Same(conn, tx.GetConnection());
    }

    [Fact]
    public void FromExisting_WithTransaction_ReturnsDbTransaction() {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var dbTx = conn.BeginTransaction();

        using var tx = AdoTransaction.FromExisting(conn, dbTx);

        Assert.Same(dbTx, tx.GetDbTransaction());
    }

    [Fact]
    public void FromExisting_WithoutTransaction_ReturnsNullDbTransaction() {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();

        using var tx = AdoTransaction.FromExisting(conn);

        Assert.Null(tx.GetDbTransaction());
    }

    [Fact]
    public void FromExisting_Dispose_DoesNotCloseExternalConnection() {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();

        var tx = AdoTransaction.FromExisting(conn);
        tx.Dispose();

        Assert.Equal(ConnectionState.Open, conn.State);
    }

    [Fact]
    public async Task FromExisting_DisposeAsync_DoesNotCloseExternalConnection() {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();

        var tx = AdoTransaction.FromExisting(conn);
        await tx.DisposeAsync();

        Assert.Equal(ConnectionState.Open, conn.State);
    }

    [Fact]
    public void FromExisting_Dispose_DoesNotDisposeExternalTransaction() {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var dbTx = conn.BeginTransaction();

        var tx = AdoTransaction.FromExisting(conn, dbTx);
        tx.Dispose();

        Assert.Equal(ConnectionState.Open, conn.State);
    }

    [Fact]
    public void FromExisting_Commit_IsNoOp() {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var dbTx = conn.BeginTransaction();

        using var tx = AdoTransaction.FromExisting(conn, dbTx);
        tx.Commit();

        Assert.Same(dbTx, tx.GetDbTransaction());
    }

    [Fact]
    public async Task FromExisting_CommitAsync_IsNoOp() {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var dbTx = conn.BeginTransaction();

        using var tx = AdoTransaction.FromExisting(conn, dbTx);
        await tx.CommitAsync();

        Assert.Same(dbTx, tx.GetDbTransaction());
    }

    [Fact]
    public void FromExisting_Rollback_IsNoOp() {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var dbTx = conn.BeginTransaction();

        using var tx = AdoTransaction.FromExisting(conn, dbTx);
        tx.Rollback();

        Assert.Same(dbTx, tx.GetDbTransaction());
    }

    [Fact]
    public async Task FromExisting_RollbackAsync_IsNoOp() {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var dbTx = conn.BeginTransaction();

        using var tx = AdoTransaction.FromExisting(conn, dbTx);
        await tx.RollbackAsync();

        Assert.Same(dbTx, tx.GetDbTransaction());
    }

    /** --- SqlSessionFactory.FromExistingConnection 통합 테스트 --- */

    [Fact]
    public async Task Factory_FromExistingConnection_ReadsData() {
        var factory = new SqlSessionFactory(_config, _provider);

        using var externalConn = new SqliteConnection("Data Source=ExternalConnTests;Mode=Memory;Cache=Shared");
        externalConn.Open();

        using var session = factory.FromExistingConnection(externalConn);

        var result = await session.SelectListAsync<long>(SelectCount.FullId);
        Assert.Single(result);
        Assert.Equal(3L, result[0]);
    }

    [Fact]
    public async Task Factory_FromExistingConnection_WithTransaction_ReadsUncommittedData() {
        var factory = new SqlSessionFactory(_config, _provider);

        using var externalConn = new SqliteConnection("Data Source=ExternalConnTests;Mode=Memory;Cache=Shared");
        externalConn.Open();
        using var externalTx = externalConn.BeginTransaction();

        using var insertCmd = externalConn.CreateCommand();
        insertCmd.Transaction = externalTx;
        insertCmd.CommandText = "INSERT INTO users (id, name, age) VALUES (100, 'External', 40)";
        insertCmd.ExecuteNonQuery();

        using var session = factory.FromExistingConnection(externalConn, externalTx);

        var count = await session.SelectOneAsync<long>(SelectCount.FullId);
        Assert.Equal(4L, count);

        externalTx.Rollback();
    }

    [Fact]
    public async Task Factory_FromExistingConnection_SessionDisposeDoesNotCloseConnection() {
        var factory = new SqlSessionFactory(_config, _provider);

        using var externalConn = new SqliteConnection("Data Source=ExternalConnTests;Mode=Memory;Cache=Shared");
        externalConn.Open();

        var session = factory.FromExistingConnection(externalConn);
        await session.SelectListAsync<long>(SelectCount.FullId);
        session.Dispose();

        Assert.Equal(ConnectionState.Open, externalConn.State);

        using var verifyCmd = externalConn.CreateCommand();
        verifyCmd.CommandText = "SELECT COUNT(*) FROM users";
        var result = (long)verifyCmd.ExecuteScalar()!;
        Assert.Equal(3L, result);
    }

    [Fact]
    public async Task Factory_FromExistingConnection_CommitIsNoOp() {
        var factory = new SqlSessionFactory(_config, _provider);

        using var externalConn = new SqliteConnection("Data Source=ExternalConnTests;Mode=Memory;Cache=Shared");
        externalConn.Open();
        using var externalTx = externalConn.BeginTransaction();

        using var session = factory.FromExistingConnection(externalConn, externalTx);

        session.Commit();
        await session.CommitAsync();

        using var verifyCmd = externalConn.CreateCommand();
        verifyCmd.Transaction = externalTx;
        verifyCmd.CommandText = "SELECT 1";
        var result = verifyCmd.ExecuteScalar();
        Assert.NotNull(result);

        externalTx.Rollback();
    }

    [Fact]
    public async Task Factory_FromExistingConnection_RollbackIsNoOp() {
        var factory = new SqlSessionFactory(_config, _provider);

        using var externalConn = new SqliteConnection("Data Source=ExternalConnTests;Mode=Memory;Cache=Shared");
        externalConn.Open();
        using var externalTx = externalConn.BeginTransaction();

        using var session = factory.FromExistingConnection(externalConn, externalTx);

        session.Rollback();
        await session.RollbackAsync();

        using var verifyCmd = externalConn.CreateCommand();
        verifyCmd.Transaction = externalTx;
        verifyCmd.CommandText = "SELECT COUNT(*) FROM users";
        var result = (long)verifyCmd.ExecuteScalar()!;
        Assert.Equal(3L, result);

        externalTx.Rollback();
    }

    [Fact]
    public async Task Factory_FromExistingConnection_SelectStream_Works() {
        var factory = new SqlSessionFactory(_config, _provider);

        using var externalConn = new SqliteConnection("Data Source=ExternalConnTests;Mode=Memory;Cache=Shared");
        externalConn.Open();

        using var session = factory.FromExistingConnection(externalConn);

        var ids = new List<long>();
        await foreach (var id in session.SelectStream<long>(SelectAll.FullId)) {
            ids.Add(id);
        }

        Assert.Equal(3, ids.Count);
        Assert.Equal(new long[] { 1, 2, 3 }, ids);
    }

    [Fact]
    public async Task Factory_FromExistingConnection_WithInterceptors() {
        var factory = new SqlSessionFactory(_config, _provider);
        var log     = new InterceptorLog();
        factory.AddInterceptor(log);

        using var externalConn = new SqliteConnection("Data Source=ExternalConnTests;Mode=Memory;Cache=Shared");
        externalConn.Open();

        using var session = factory.FromExistingConnection(externalConn);

        await session.SelectListAsync<long>(SelectCount.FullId);

        Assert.True(log.BeforeCalled);
        Assert.True(log.AfterCalled);
    }

    [Fact]
    public async Task Factory_FromExistingConnection_SharedTransaction_BothSidesSeeWrites() {
        var factory = new SqlSessionFactory(_config, _provider);

        using var externalConn = new SqliteConnection("Data Source=ExternalConnTests;Mode=Memory;Cache=Shared");
        externalConn.Open();
        using var externalTx = externalConn.BeginTransaction();

        using var session = factory.FromExistingConnection(externalConn, externalTx);

        using var insertCmd = externalConn.CreateCommand();
        insertCmd.Transaction = externalTx;
        insertCmd.CommandText = "INSERT INTO users (id, name, age) VALUES (200, 'SharedTx', 50)";
        insertCmd.ExecuteNonQuery();

        var count = await session.SelectOneAsync<long>(SelectCount.FullId);
        Assert.Equal(4L, count);

        externalTx.Rollback();
    }

    [Fact]
    public async Task Factory_FromExistingConnection_SequentialSessions_ShareSameConnection() {
        var factory = new SqlSessionFactory(_config, _provider);

        using var externalConn = new SqliteConnection("Data Source=ExternalConnTests;Mode=Memory;Cache=Shared");
        externalConn.Open();

        var session1 = factory.FromExistingConnection(externalConn);
        var count1   = await session1.SelectOneAsync<long>(SelectCount.FullId);
        session1.Dispose();

        Assert.Equal(ConnectionState.Open, externalConn.State);

        var session2 = factory.FromExistingConnection(externalConn);
        var count2   = await session2.SelectOneAsync<long>(SelectCount.FullId);
        session2.Dispose();

        Assert.Equal(ConnectionState.Open, externalConn.State);
        Assert.Equal(count1, count2);
    }

    /** --- SimpleExecutor + FromExisting 저수준 테스트 --- */

    [Fact]
    public void Executor_FromExisting_CommandHasCorrectTransaction() {
        using var conn = new SqliteConnection("Data Source=ExternalConnTests;Mode=Memory;Cache=Shared");
        conn.Open();
        using var dbTx = conn.BeginTransaction();

        var adoTx    = AdoTransaction.FromExisting(conn, dbTx);
        var executor = new SimpleExecutor(adoTx);

        using (executor) {
            var result = executor.SelectOne(
                SelectCount, SelectCount.SqlSource,
                Array.Empty<DbParameter>(),
                reader => reader.GetInt64(0));

            Assert.Equal(3L, result);
        }
    }

    [Fact]
    public async Task Executor_FromExisting_AsyncOperationsWork() {
        using var conn = new SqliteConnection("Data Source=ExternalConnTests;Mode=Memory;Cache=Shared");
        conn.Open();

        var adoTx    = AdoTransaction.FromExisting(conn);
        var executor = new SimpleExecutor(adoTx);

        await using (executor) {
            var result = await executor.SelectListAsync(
                SelectAll, SelectAll.SqlSource,
                Array.Empty<DbParameter>(),
                reader => reader.GetInt64(0));

            Assert.Equal(3, result.Count);
        }

        Assert.Equal(ConnectionState.Open, conn.State);
    }

    public void Dispose() {
        _keepAlive.Dispose();
    }

    private sealed class InterceptorLog : ISqlInterceptor {
        public bool BeforeCalled { get; private set; }
        public bool AfterCalled  { get; private set; }

        public void BeforeExecute(InterceptorContext context)  => BeforeCalled = true;
        public void AfterExecute(InterceptorContext context)   => AfterCalled = true;
        public Task BeforeExecuteAsync(InterceptorContext context, CancellationToken ct) {
            BeforeCalled = true;
            return Task.CompletedTask;
        }
        public Task AfterExecuteAsync(InterceptorContext context, CancellationToken ct) {
            AfterCalled = true;
            return Task.CompletedTask;
        }
    }
}
