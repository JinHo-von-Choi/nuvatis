using Microsoft.Data.Sqlite;
using NuVatis.Executor;
using NuVatis.Provider;
using NuVatis.Statement;
using NuVatis.Transaction;
using NuVatis.Binding;

namespace NuVatis.Tests;

/**
 * BatchExecutor 테스트.
 * SQLite 인메모리 DB로 배치 실행, Flush, Count 검증.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public class BatchExecutorTests : IDisposable {
    private readonly SqliteConnection _keepAlive;
    private const string ConnectionString = "Data Source=batch_test;Mode=Memory;Cache=Shared";

    public BatchExecutorTests() {
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
    }

    [Fact]
    public void Add_IncrementsCount() {
        using var batch = CreateBatchExecutor();

        batch.Add(CreateStatement("insert"), "INSERT INTO batch_items (name, value) VALUES ('a', 1)", Array.Empty<System.Data.Common.DbParameter>());
        batch.Add(CreateStatement("insert"), "INSERT INTO batch_items (name, value) VALUES ('b', 2)", Array.Empty<System.Data.Common.DbParameter>());

        Assert.Equal(2, batch.Count);
    }

    [Fact]
    public void Flush_ExecutesAllAndClearsBatch() {
        using var batch = CreateBatchExecutor();

        batch.Add(CreateStatement("insert"), "INSERT INTO batch_items (name, value) VALUES ('x', 10)", Array.Empty<System.Data.Common.DbParameter>());
        batch.Add(CreateStatement("insert"), "INSERT INTO batch_items (name, value) VALUES ('y', 20)", Array.Empty<System.Data.Common.DbParameter>());
        batch.Add(CreateStatement("insert"), "INSERT INTO batch_items (name, value) VALUES ('z', 30)", Array.Empty<System.Data.Common.DbParameter>());

        var affected = batch.Flush();
        batch.Commit();

        Assert.Equal(3, affected);
        Assert.Equal(0, batch.Count);
        Assert.Equal(3, CountRows());
    }

    [Fact]
    public async Task FlushAsync_ExecutesAllAndClearsBatch() {
        var batch = CreateBatchExecutor();

        batch.Add(CreateStatement("insert"), "INSERT INTO batch_items (name, value) VALUES ('a1', 100)", Array.Empty<System.Data.Common.DbParameter>());
        batch.Add(CreateStatement("insert"), "INSERT INTO batch_items (name, value) VALUES ('a2', 200)", Array.Empty<System.Data.Common.DbParameter>());

        var affected = await batch.FlushAsync();
        await batch.CommitAsync();

        Assert.Equal(2, affected);
        Assert.Equal(0, batch.Count);
        Assert.Equal(2, CountRows());

        await batch.DisposeAsync();
    }

    [Fact]
    public void Flush_WithParameters_BindsCorrectly() {
        using var batch = CreateBatchExecutor();

        var (sql, parameters) = ParameterBinder.Bind(
            "INSERT INTO batch_items (name, value) VALUES (#{Name}, #{Value})",
            new { Name = "param_test", Value = 999 });

        batch.Add(CreateStatement("insert"), sql, parameters);
        var affected = batch.Flush();
        batch.Commit();

        Assert.Equal(1, affected);

        using var verifyCmd     = _keepAlive.CreateCommand();
        verifyCmd.CommandText  = "SELECT value FROM batch_items WHERE name = 'param_test'";
        var result             = verifyCmd.ExecuteScalar();
        Assert.Equal(999L, result);
    }

    [Fact]
    public void Flush_EmptyBatch_ReturnsZero() {
        using var batch = CreateBatchExecutor();

        var affected = batch.Flush();

        Assert.Equal(0, affected);
    }

    [Fact]
    public void Rollback_RevertsFlush() {
        using var batch = CreateBatchExecutor();

        batch.Add(CreateStatement("insert"), "INSERT INTO batch_items (name, value) VALUES ('rollback_test', 1)", Array.Empty<System.Data.Common.DbParameter>());
        batch.Flush();
        batch.Rollback();

        Assert.Equal(0, CountRows());
    }

    public void Dispose() {
        _keepAlive.Dispose();
    }

    private BatchExecutor CreateBatchExecutor() {
        var provider    = new SqliteProvider();
        var transaction = new AdoTransaction(provider, ConnectionString, autoCommit: false);
        return new BatchExecutor(transaction);
    }

    private long CountRows() {
        using var cmd    = _keepAlive.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM batch_items";
        return (long)cmd.ExecuteScalar()!;
    }

    private static MappedStatement CreateStatement(string id) {
        return new MappedStatement {
            Id        = id,
            Namespace = "batch",
            Type      = StatementType.Insert,
            SqlSource = ""
        };
    }

    private sealed class SqliteProvider : IDbProvider {
        public string Name => "SQLite";
        public System.Data.Common.DbConnection CreateConnection(string connectionString) => new SqliteConnection(connectionString);
        public string ParameterPrefix => "@";
        public string GetParameterName(int index) => $"@p{index}";
        public string WrapIdentifier(string name) => $"\"{name}\"";
    }
}
