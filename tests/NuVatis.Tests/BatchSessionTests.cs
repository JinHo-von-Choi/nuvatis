using Microsoft.Data.Sqlite;
using System.Data.Common;
using NuVatis.Configuration;
using NuVatis.Executor;
using NuVatis.Provider;
using NuVatis.Session;
using NuVatis.Statement;
using NuVatis.Transaction;
using NuVatis.Binding;
using StatementType = NuVatis.Statement.StatementType;

namespace NuVatis.Tests;

/**
 * BatchSession 통합 테스트.
 * SqlSessionFactory.OpenBatchSession()으로 생성된 세션의
 * 배치 누적, FlushStatements, Commit 자동 Flush 동작을 검증한다.
 *
 * @author 최진호
 * @date   2026-02-26
 */
public class BatchSessionTests : IDisposable {

    private readonly SqliteConnection     _keepAlive;
    private readonly SqliteProvider       _provider;
    private readonly NuVatisConfiguration _config;

    private const string ConnectionString = "Data Source=batch_session_test;Mode=Memory;Cache=Shared";

    private sealed class SqliteProvider : IDbProvider {
        public string Name => "SQLite";
        public DbConnection CreateConnection(string connectionString) => new SqliteConnection(connectionString);
        public string ParameterPrefix => "@";
        public string GetParameterName(int index) => $"@p{index}";
        public string WrapIdentifier(string name) => $"\"{name}\"";
    }

    private static readonly MappedStatement InsertStmt = new() {
        Id        = "Insert",
        Namespace = "BatchItem",
        Type      = StatementType.Insert,
        SqlSource = "INSERT INTO batch_items (name, value) VALUES (#{Name}, #{Value})"
    };

    private static readonly MappedStatement SelectCountStmt = new() {
        Id        = "Count",
        Namespace = "BatchItem",
        Type      = StatementType.Select,
        SqlSource = "SELECT COUNT(*) FROM batch_items"
    };

    public BatchSessionTests() {
        _keepAlive = new SqliteConnection(ConnectionString);
        _keepAlive.Open();

        using var cmd = _keepAlive.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS batch_items (
                id    INTEGER PRIMARY KEY AUTOINCREMENT,
                name  TEXT NOT NULL,
                value INTEGER NOT NULL
            )
        """;
        cmd.ExecuteNonQuery();

        using var del = _keepAlive.CreateCommand();
        del.CommandText = "DELETE FROM batch_items";
        del.ExecuteNonQuery();

        _provider = new SqliteProvider();
        _config   = new NuVatisConfiguration {
            DataSource = new DataSourceConfig {
                ProviderName     = "SQLite",
                ConnectionString = ConnectionString
            },
            Statements = new Dictionary<string, MappedStatement> {
                [InsertStmt.FullId]      = InsertStmt,
                [SelectCountStmt.FullId] = SelectCountStmt
            }
        };
    }

    /**
     * OpenBatchSession에서 생성된 세션의 IsBatchMode가 true인지 검증.
     */
    [Fact]
    public void OpenBatchSession_CreatesSessionWithBatchMode() {
        var factory = new SqlSessionFactory(_config, _provider, null);

        using var session = factory.OpenBatchSession();

        Assert.True(session.IsBatchMode);
    }

    /**
     * 일반 OpenSession은 IsBatchMode가 false인지 검증.
     */
    [Fact]
    public void OpenSession_IsNotBatchMode() {
        var factory = new SqlSessionFactory(_config, _provider, null);

        using var session = factory.OpenSession();

        Assert.False(session.IsBatchMode);
    }

    /**
     * 배치 모드에서 Insert 호출 시 0을 반환하고, DB에 즉시 반영되지 않는지 검증.
     * FlushStatements 호출 후 실제 행이 삽입되는지 검증.
     */
    [Fact]
    public void BatchMode_Insert_AccumulatesUntilFlush() {
        var factory = new SqlSessionFactory(_config, _provider, null);
        using var session = factory.OpenBatchSession();

        var result1 = InsertItem(session, "a", 1);
        var result2 = InsertItem(session, "b", 2);

        Assert.Equal(0, result1);
        Assert.Equal(0, result2);
        Assert.Equal(0, CountRows());

        var flushed = session.FlushStatements();

        Assert.Equal(2, flushed);

        session.Commit();
        Assert.Equal(2, CountRows());
    }

    /**
     * 배치 모드에서 Commit 시 미처리 배치를 자동 Flush하는지 검증.
     */
    [Fact]
    public void BatchMode_Commit_AutoFlushes() {
        var factory = new SqlSessionFactory(_config, _provider, null);
        using var session = factory.OpenBatchSession();

        InsertItem(session, "auto_a", 10);
        InsertItem(session, "auto_b", 20);

        session.Commit();

        Assert.Equal(2, CountRows());
    }

    /**
     * FlushStatementsAsync 비동기 Flush 검증.
     */
    [Fact]
    public async Task BatchMode_FlushStatementsAsync_Works() {
        var factory = new SqlSessionFactory(_config, _provider, null);
        var session = factory.OpenBatchSession();

        InsertItem(session, "async_a", 100);
        InsertItem(session, "async_b", 200);

        var flushed = await session.FlushStatementsAsync();

        Assert.Equal(2, flushed);

        await session.CommitAsync();
        Assert.Equal(2, CountRows());

        await session.DisposeAsync();
    }

    /**
     * 비배치 세션에서 FlushStatements 호출 시 0을 반환하는지 검증.
     */
    [Fact]
    public void NonBatchMode_FlushStatements_ReturnsZero() {
        var factory = new SqlSessionFactory(_config, _provider, null);
        using var session = factory.OpenSession();

        Assert.Equal(0, session.FlushStatements());
    }

    /**
     * 빈 배치에 FlushStatements 호출 시 0을 반환하는지 검증.
     */
    [Fact]
    public void BatchMode_FlushEmptyBatch_ReturnsZero() {
        var factory = new SqlSessionFactory(_config, _provider, null);
        using var session = factory.OpenBatchSession();

        Assert.Equal(0, session.FlushStatements());
    }

    public void Dispose() {
        _keepAlive.Dispose();
    }

    private static int InsertItem(ISqlSession session, string name, int value) {
        return session.Insert("BatchItem.Insert", new { Name = name, Value = value });
    }

    private long CountRows() {
        using var cmd    = _keepAlive.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM batch_items";
        return (long)cmd.ExecuteScalar()!;
    }
}
