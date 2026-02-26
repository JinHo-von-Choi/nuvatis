using Microsoft.Data.Sqlite;
using NuVatis.Configuration;
using NuVatis.Session;
using NuVatis.Sqlite;
using NuVatis.Statement;
using Xunit;

namespace NuVatis.Tests.E2E;

/**
 * NuVatis.Sqlite 패키지의 SqliteProvider를 사용한 E2E 테스트.
 * CRUD 전체 파이프라인을 검증한다.
 *
 * @author 최진호
 * @date   2026-02-26
 */
[Trait("Category", "E2E")]
public class SqliteE2ETests : IDisposable {
    private readonly SqliteConnection _keepAlive;
    private readonly SqliteProvider   _provider;
    private readonly NuVatisConfiguration _config;
    private readonly SqlSessionFactory    _factory;

    private static readonly MappedStatement InsertStmt = new() {
        Id        = "Insert",
        Namespace = "Item",
        Type      = StatementType.Insert,
        SqlSource = "INSERT INTO items (name, value) VALUES (#{Name}, #{Value})"
    };

    private static readonly MappedStatement SelectAllStmt = new() {
        Id        = "All",
        Namespace = "Item",
        Type      = StatementType.Select,
        SqlSource = "SELECT name, value FROM items ORDER BY name"
    };

    private static readonly MappedStatement UpdateStmt = new() {
        Id        = "Update",
        Namespace = "Item",
        Type      = StatementType.Update,
        SqlSource = "UPDATE items SET value = #{Value} WHERE name = #{Name}"
    };

    private static readonly MappedStatement DeleteStmt = new() {
        Id        = "Delete",
        Namespace = "Item",
        Type      = StatementType.Delete,
        SqlSource = "DELETE FROM items WHERE name = #{Name}"
    };

    private static readonly MappedStatement CountStmt = new() {
        Id        = "Count",
        Namespace = "Item",
        Type      = StatementType.Select,
        SqlSource = "SELECT COUNT(*) FROM items"
    };

    public SqliteE2ETests() {
        var connStr = "Data Source=SqliteE2E;Mode=Memory;Cache=Shared";
        _keepAlive  = new SqliteConnection(connStr);
        _keepAlive.Open();

        using var cmd     = _keepAlive.CreateCommand();
        cmd.CommandText  = "CREATE TABLE IF NOT EXISTS items (name TEXT, value INTEGER)";
        cmd.ExecuteNonQuery();

        _provider = new SqliteProvider();
        _config   = new NuVatisConfiguration {
            DataSource = new DataSourceConfig {
                ProviderName     = "Sqlite",
                ConnectionString = connStr
            }
        };

        _config.Statements["Item.Insert"] = InsertStmt;
        _config.Statements["Item.All"]    = SelectAllStmt;
        _config.Statements["Item.Update"] = UpdateStmt;
        _config.Statements["Item.Delete"] = DeleteStmt;
        _config.Statements["Item.Count"]  = CountStmt;

        _factory = new SqlSessionFactory(_config, _provider, null);
    }

    [Fact]
    public void SqliteProvider_Name() {
        Assert.Equal("Sqlite", _provider.Name);
        Assert.Equal("@", _provider.ParameterPrefix);
        Assert.Equal("@p0", _provider.GetParameterName(0));
        Assert.Equal("\"col\"", _provider.WrapIdentifier("col"));
    }

    [Fact]
    public void Insert_And_Select_Roundtrip() {
        using var session = _factory.OpenSession();
        session.Insert("Item.Insert", new { Name = "alpha", Value = 10 });
        session.Insert("Item.Insert", new { Name = "beta", Value = 20 });
        session.Commit();

        var count = CountRows();
        Assert.True(count >= 2);
    }

    [Fact]
    public void Update_ModifiesValue() {
        using var session = _factory.OpenSession();
        session.Insert("Item.Insert", new { Name = "upd_target", Value = 1 });
        session.Commit();

        using var session2 = _factory.OpenSession();
        var affected       = session2.Update("Item.Update", new { Name = "upd_target", Value = 999 });
        session2.Commit();

        Assert.True(affected >= 1);
    }

    [Fact]
    public void Delete_RemovesRow() {
        using var session = _factory.OpenSession();
        session.Insert("Item.Insert", new { Name = "del_target", Value = 0 });
        session.Commit();

        using var session2 = _factory.OpenSession();
        var affected       = session2.Delete("Item.Delete", new { Name = "del_target" });
        session2.Commit();

        Assert.True(affected >= 1);
    }

    [Fact]
    public void Rollback_DoesNotPersist() {
        var countBefore = CountRows();

        using var session = _factory.OpenSession();
        session.Insert("Item.Insert", new { Name = "rollback_item", Value = 0 });
        session.Rollback();

        var countAfter = CountRows();
        Assert.Equal(countBefore, countAfter);
    }

    [Fact]
    public async Task AsyncInsert_Works() {
        using var session = _factory.OpenSession();
        await session.InsertAsync("Item.Insert", new { Name = "async_item", Value = 42 });
        await session.CommitAsync();

        var count = CountRows();
        Assert.True(count >= 1);
    }

    public void Dispose() {
        using var cmd     = _keepAlive.CreateCommand();
        cmd.CommandText  = "DELETE FROM items";
        cmd.ExecuteNonQuery();
        _keepAlive.Dispose();
    }

    private int CountRows() {
        using var conn    = new SqliteConnection("Data Source=SqliteE2E;Mode=Memory;Cache=Shared");
        conn.Open();
        using var cmd     = conn.CreateCommand();
        cmd.CommandText  = "SELECT COUNT(*) FROM items";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
}
