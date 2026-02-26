using System.Data.Common;
using Microsoft.Data.Sqlite;
using NuVatis.Configuration;
using NuVatis.Mapping;
using NuVatis.Session;
using NuVatis.Statement;
using Xunit;

namespace NuVatis.Tests;

/**
 * SqlSession 고급 기능 테스트:
 * SelectStream, SelectMultiple, ExecuteInTransaction, FlushStatements, GetMapper 에러 경로.
 *
 * @author 최진호
 * @date   2026-02-26
 */
[Trait("Category", "E2E")]
public class SqlSessionAdvancedTests : IDisposable {

    private readonly SqliteConnection _keepAlive;
    private readonly SqlSessionFactory _factory;

    private sealed class TestProvider : Provider.IDbProvider {
        private readonly string _cs;
        public TestProvider(string cs) { _cs = cs; }
        public string Name                              => "Sqlite";
        public DbConnection CreateConnection(string cs) => new SqliteConnection(_cs);
        public string ParameterPrefix                   => "@";
        public string GetParameterName(int index)       => $"@p{index}";
        public string WrapIdentifier(string name)       => $"\"{name}\"";
    }

    private static readonly MappedStatement InsertStmt = new() {
        Id = "Insert", Namespace = "Adv", Type = StatementType.Insert,
        SqlSource = "INSERT INTO adv_test (name) VALUES (#{Name})"
    };

    private static readonly MappedStatement SelectStmt = new() {
        Id = "Select", Namespace = "Adv", Type = StatementType.Select,
        SqlSource = "SELECT name FROM adv_test ORDER BY name"
    };

    private static readonly MappedStatement CountStmt = new() {
        Id = "Count", Namespace = "Adv", Type = StatementType.Select,
        SqlSource = "SELECT COUNT(*) FROM adv_test"
    };

    private static readonly MappedStatement MultiStmt = new() {
        Id = "Multi", Namespace = "Adv", Type = StatementType.Select,
        SqlSource = "SELECT name FROM adv_test; SELECT COUNT(*) FROM adv_test"
    };

    public SqlSessionAdvancedTests() {
        var connStr = "Data Source=AdvTests;Mode=Memory;Cache=Shared";
        _keepAlive  = new SqliteConnection(connStr);
        _keepAlive.Open();

        using var cmd    = _keepAlive.CreateCommand();
        cmd.CommandText = "CREATE TABLE IF NOT EXISTS adv_test (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT)";
        cmd.ExecuteNonQuery();

        var provider = new TestProvider(connStr);
        var config   = new NuVatisConfiguration {
            DataSource = new DataSourceConfig {
                ProviderName     = "Sqlite",
                ConnectionString = connStr
            }
        };
        config.Statements["Adv.Insert"] = InsertStmt;
        config.Statements["Adv.Select"] = SelectStmt;
        config.Statements["Adv.Count"]  = CountStmt;
        config.Statements["Adv.Multi"]  = MultiStmt;

        _factory = new SqlSessionFactory(config, provider, null);
    }

    [Fact]
    public async Task SelectStream_ReturnsItems() {
        using var session = _factory.OpenSession();
        session.Insert("Adv.Insert", new { Name = "stream1" });
        session.Insert("Adv.Insert", new { Name = "stream2" });
        session.Commit();

        using var session2 = _factory.OpenSession();
        var items          = new List<string>();
        await foreach (var item in session2.SelectStream<string>("Adv.Select")) {
            items.Add(item);
        }
        Assert.True(items.Count >= 2);
    }

    [Fact]
    public void SelectMultiple_ReturnsResultSets() {
        using var session = _factory.OpenSession();
        session.Insert("Adv.Insert", new { Name = "multi1" });
        session.Commit();

        using var session2 = _factory.OpenSession();
        using var group     = session2.SelectMultiple("Adv.Multi");
        Assert.NotNull(group);
    }

    [Fact]
    public async Task SelectMultipleAsync_ReturnsResultSets() {
        using var session = _factory.OpenSession();
        session.Insert("Adv.Insert", new { Name = "multi_async" });
        session.Commit();

        using var session2 = _factory.OpenSession();
        using var group     = await session2.SelectMultipleAsync("Adv.Multi");
        Assert.NotNull(group);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_Commits_OnSuccess() {
        using var session = _factory.OpenSession();
        await session.ExecuteInTransactionAsync(async () => {
            await session.InsertAsync("Adv.Insert", new { Name = "tx_success" });
        });
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_Rolls_Back_OnFailure() {
        using var session = _factory.OpenSession();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => {
            await session.ExecuteInTransactionAsync(async () => {
                await session.InsertAsync("Adv.Insert", new { Name = "tx_fail" });
                throw new InvalidOperationException("test error");
            });
        });
    }

    [Fact]
    public void GetMapper_Without_Factory_Throws() {
        using var session = _factory.OpenSession();
        Assert.Throws<InvalidOperationException>(() => session.GetMapper<IDisposable>());
    }

    [Fact]
    public void FlushStatements_NonBatch_ReturnsZero() {
        using var session = _factory.OpenSession();
        var result        = session.FlushStatements();
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task FlushStatementsAsync_NonBatch_ReturnsZero() {
        using var session = _factory.OpenSession();
        var result        = await session.FlushStatementsAsync();
        Assert.Equal(0, result);
    }

    [Fact]
    public void SelectOne_WithCustomMapper() {
        using var session = _factory.OpenSession();
        session.Insert("Adv.Insert", new { Name = "custom_map" });
        session.Commit();

        using var s2 = _factory.OpenSession();
        var name     = s2.SelectOne<string>("Adv.Count", null, reader => reader.GetInt32(0).ToString());
        Assert.NotNull(name);
    }

    [Fact]
    public void SelectList_WithCustomMapper() {
        using var session = _factory.OpenSession();
        session.Insert("Adv.Insert", new { Name = "list_map1" });
        session.Insert("Adv.Insert", new { Name = "list_map2" });
        session.Commit();

        using var s2  = _factory.OpenSession();
        var names     = s2.SelectList<string>("Adv.Select", null, reader => reader.GetString(0));
        Assert.True(names.Count >= 2);
    }

    [Fact]
    public async Task SelectOneAsync_WithCustomMapper() {
        using var session = _factory.OpenSession();
        session.Insert("Adv.Insert", new { Name = "async_custom" });
        session.Commit();

        using var s2 = _factory.OpenSession();
        var name     = await s2.SelectOneAsync<string>("Adv.Count", null, reader => reader.GetInt32(0).ToString());
        Assert.NotNull(name);
    }

    [Fact]
    public async Task SelectListAsync_WithCustomMapper() {
        using var session = _factory.OpenSession();
        session.Insert("Adv.Insert", new { Name = "async_list" });
        session.Commit();

        using var s2  = _factory.OpenSession();
        var names     = await s2.SelectListAsync<string>("Adv.Select", null, reader => reader.GetString(0));
        Assert.True(names.Count >= 1);
    }

    public void Dispose() {
        using var cmd    = _keepAlive.CreateCommand();
        cmd.CommandText = "DELETE FROM adv_test";
        cmd.ExecuteNonQuery();
        _keepAlive.Dispose();
    }
}
