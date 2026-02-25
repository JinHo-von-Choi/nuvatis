using System.Data;
using Microsoft.Data.Sqlite;
using NuVatis.Provider;
using NuVatis.Transaction;
using Xunit;

namespace NuVatis.Tests;

/**
 * AdoTransaction 단위 테스트. SQLite in-memory로 Lazy Connection,
 * Commit, Rollback, Dispose 동작을 검증한다.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public class AdoTransactionTests {

    private const string ConnStr = "Data Source=:memory:";

    private sealed class SqliteTestProvider : IDbProvider {
        public string Name => "Sqlite";
        public System.Data.Common.DbConnection CreateConnection(string connectionString)
            => new SqliteConnection(connectionString);
        public string ParameterPrefix => "@";
        public string GetParameterName(int index) => $"@p{index}";
        public string WrapIdentifier(string name) => $"\"{name}\"";
    }

    [Fact]
    public void LazyConnection_NullBeforeFirstAccess() {
        var provider = new SqliteTestProvider();
        using var tx = new AdoTransaction(provider, ConnStr);

        Assert.Null(tx.Connection);
    }

    [Fact]
    public void GetConnection_OpensAndReturnsConnection() {
        var provider = new SqliteTestProvider();
        using var tx = new AdoTransaction(provider, ConnStr);

        var conn = tx.GetConnection();

        Assert.NotNull(conn);
        Assert.Equal(ConnectionState.Open, conn.State);
    }

    [Fact]
    public void GetConnection_ReturnsSameInstance() {
        var provider = new SqliteTestProvider();
        using var tx = new AdoTransaction(provider, ConnStr, autoCommit: true);

        var conn1 = tx.GetConnection();
        var conn2 = tx.GetConnection();

        Assert.Same(conn1, conn2);
    }

    [Fact]
    public void AutoCommitTrue_NoTransaction() {
        var provider = new SqliteTestProvider();
        using var tx = new AdoTransaction(provider, ConnStr, autoCommit: true);

        tx.GetConnection();

        Assert.Null(tx.GetDbTransaction());
    }

    [Fact]
    public void AutoCommitFalse_CreatesTransaction() {
        var provider = new SqliteTestProvider();
        using var tx = new AdoTransaction(provider, ConnStr, autoCommit: false);

        tx.GetConnection();

        Assert.NotNull(tx.GetDbTransaction());
    }

    [Fact]
    public void Commit_ClearsTransaction() {
        var provider = new SqliteTestProvider();
        using var tx = new AdoTransaction(provider, ConnStr, autoCommit: false);

        tx.GetConnection();
        tx.Commit();

        Assert.Null(tx.GetDbTransaction());
    }

    [Fact]
    public void Rollback_ClearsTransaction() {
        var provider = new SqliteTestProvider();
        using var tx = new AdoTransaction(provider, ConnStr, autoCommit: false);

        tx.GetConnection();
        tx.Rollback();

        Assert.Null(tx.GetDbTransaction());
    }

    [Fact]
    public void Commit_WithoutConnection_NoOp() {
        var provider = new SqliteTestProvider();
        using var tx = new AdoTransaction(provider, ConnStr, autoCommit: true);

        tx.Commit();
    }

    [Fact]
    public void Dispose_ThenGetConnection_Throws() {
        var provider = new SqliteTestProvider();
        var tx       = new AdoTransaction(provider, ConnStr);
        tx.Dispose();

        Assert.Throws<ObjectDisposedException>(() => tx.GetConnection());
    }

    [Fact]
    public async Task DisposeAsync_ThenGetConnectionAsync_Throws() {
        var provider = new SqliteTestProvider();
        var tx       = new AdoTransaction(provider, ConnStr);
        await tx.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => tx.GetConnectionAsync());
    }

    [Fact]
    public async Task GetConnectionAsync_OpensConnection() {
        var provider = new SqliteTestProvider();
        await using var tx = new AdoTransaction(provider, ConnStr, autoCommit: true);

        var conn = await tx.GetConnectionAsync();

        Assert.NotNull(conn);
        Assert.Equal(ConnectionState.Open, conn.State);
    }

    [Fact]
    public async Task CommitAsync_ClearsTransaction() {
        var provider = new SqliteTestProvider();
        await using var tx = new AdoTransaction(provider, ConnStr, autoCommit: false);

        await tx.GetConnectionAsync();
        await tx.CommitAsync();

        Assert.Null(tx.GetDbTransaction());
    }
}
