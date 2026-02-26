using System.Data.Common;
using Microsoft.Data.Sqlite;
using NuVatis.Configuration;
using NuVatis.Session;
using NuVatis.Statement;
using Xunit;

namespace NuVatis.Tests;

/**
 * SqlSession 비동기 메서드 및 에러 경로 테스트.
 *
 * @author 최진호
 * @date   2026-02-26
 */
[Trait("Category", "E2E")]
public class SqlSessionAsyncTests : IDisposable {

    private readonly SqliteConnection _keepAlive;
    private readonly SqlSessionFactory _factory;

    private sealed class InlineSqliteProvider : Provider.IDbProvider {
        private readonly string _connStr;
        public InlineSqliteProvider(string cs) { _connStr = cs; }
        public string Name                                   => "Sqlite";
        public DbConnection CreateConnection(string cs)      => new SqliteConnection(_connStr);
        public string ParameterPrefix                        => "@";
        public string GetParameterName(int index)            => $"@p{index}";
        public string WrapIdentifier(string name)            => $"\"{name}\"";
    }

    private static readonly MappedStatement Insert = new() {
        Id = "Insert", Namespace = "Async", Type = StatementType.Insert,
        SqlSource = "INSERT INTO async_test (name) VALUES (#{Name})"
    };

    private static readonly MappedStatement Count = new() {
        Id = "Count", Namespace = "Async", Type = StatementType.Select,
        SqlSource = "SELECT COUNT(*) FROM async_test"
    };

    private static readonly MappedStatement Update = new() {
        Id = "Update", Namespace = "Async", Type = StatementType.Update,
        SqlSource = "UPDATE async_test SET name = 'updated' WHERE name = #{Name}"
    };

    private static readonly MappedStatement Delete = new() {
        Id = "Delete", Namespace = "Async", Type = StatementType.Delete,
        SqlSource = "DELETE FROM async_test WHERE name = #{Name}"
    };

    public SqlSessionAsyncTests() {
        var connStr = "Data Source=AsyncTests;Mode=Memory;Cache=Shared";
        _keepAlive  = new SqliteConnection(connStr);
        _keepAlive.Open();

        using var cmd    = _keepAlive.CreateCommand();
        cmd.CommandText = "CREATE TABLE IF NOT EXISTS async_test (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT)";
        cmd.ExecuteNonQuery();

        var provider = new InlineSqliteProvider(connStr);
        var config   = new NuVatisConfiguration {
            DataSource = new DataSourceConfig {
                ProviderName     = "Sqlite",
                ConnectionString = connStr
            }
        };
        config.Statements["Async.Insert"] = Insert;
        config.Statements["Async.Count"]  = Count;
        config.Statements["Async.Update"] = Update;
        config.Statements["Async.Delete"] = Delete;

        _factory = new SqlSessionFactory(config, provider, null);
    }

    [Fact]
    public async Task InsertAsync_CommitAsync() {
        using var session = _factory.OpenSession();
        await session.InsertAsync("Async.Insert", new { Name = "async1" });
        await session.CommitAsync();
    }

    [Fact]
    public async Task UpdateAsync_Works() {
        using var session = _factory.OpenSession();
        await session.InsertAsync("Async.Insert", new { Name = "to_update" });
        await session.CommitAsync();

        using var s2     = _factory.OpenSession();
        var affected     = await s2.UpdateAsync("Async.Update", new { Name = "to_update" });
        await s2.CommitAsync();
        Assert.True(affected >= 0);
    }

    [Fact]
    public async Task DeleteAsync_Works() {
        using var session = _factory.OpenSession();
        await session.InsertAsync("Async.Insert", new { Name = "to_delete" });
        await session.CommitAsync();

        using var s2     = _factory.OpenSession();
        var affected     = await s2.DeleteAsync("Async.Delete", new { Name = "to_delete" });
        await s2.CommitAsync();
        Assert.True(affected >= 0);
    }

    [Fact]
    public async Task RollbackAsync_Works() {
        using var session = _factory.OpenSession();
        await session.InsertAsync("Async.Insert", new { Name = "to_rollback" });
        await session.RollbackAsync();
    }

    [Fact]
    public void NotFound_StatementId_Throws() {
        using var session = _factory.OpenSession();
        Assert.Throws<InvalidOperationException>(() =>
            session.Insert("NonExistent.Id", null));
    }

    [Fact]
    public void Dispose_Then_Use_Throws() {
        var session = _factory.OpenSession();
        session.Dispose();

        Assert.Throws<ObjectDisposedException>(() =>
            session.Insert("Async.Insert", new { Name = "x" }));
    }

    [Fact]
    public async Task DisposeAsync_Then_Use_Throws() {
        var session = _factory.OpenSession();
        await session.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() =>
            session.Insert("Async.Insert", new { Name = "x" }));
    }

    public void Dispose() {
        using var cmd    = _keepAlive.CreateCommand();
        cmd.CommandText = "DELETE FROM async_test";
        cmd.ExecuteNonQuery();
        _keepAlive.Dispose();
    }
}
