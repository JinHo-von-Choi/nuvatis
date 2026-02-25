using System.Data.Common;
using Microsoft.Data.Sqlite;
using NuVatis.Configuration;
using NuVatis.Provider;
using NuVatis.Session;
using NuVatis.Statement;
using Xunit;

namespace NuVatis.Tests.E2E;

/**
 * SG 전체 루프 E2E 테스트.
 * SqlSession.SelectOne/SelectList가 ColumnMapper + ParameterBinder를 통해
 * 실제 DB에서 데이터를 읽고 매핑하는 전체 파이프라인을 검증한다.
 *
 * @author 최진호
 * @date   2026-02-24
 */
[Trait("Category", "E2E")]
public class FullPipelineE2ETests : IDisposable {

    private readonly SqliteConnection _keepAlive;
    private readonly SqlSessionFactory _factory;

    private sealed class SqliteProvider : IDbProvider {
        private readonly string _connStr;
        public SqliteProvider(string connStr) { _connStr = connStr; }
        public string Name => "Sqlite";
        public DbConnection CreateConnection(string connectionString) => new SqliteConnection(_connStr);
        public string ParameterPrefix => "@";
        public string GetParameterName(int index) => $"@p{index}";
        public string WrapIdentifier(string name) => $"\"{name}\"";
    }

    public class UserDto {
        public long Id       { get; set; }
        public string Name   { get; set; } = string.Empty;
        public string Email  { get; set; } = string.Empty;
        public int Age       { get; set; }
    }

    public class SearchParam {
        public string Name { get; set; } = string.Empty;
    }

    public class InsertParam {
        public string Name  { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int Age      { get; set; }
    }

    public FullPipelineE2ETests() {
        var connStr = "Data Source=FullPipelineTests;Mode=Memory;Cache=Shared";
        _keepAlive  = new SqliteConnection(connStr);
        _keepAlive.Open();

        using var cmd = _keepAlive.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS users (
                id    INTEGER PRIMARY KEY AUTOINCREMENT,
                name  TEXT    NOT NULL,
                email TEXT    NOT NULL,
                age   INTEGER NOT NULL
            );
            DELETE FROM users;
            INSERT INTO users (id, name, email, age) VALUES (1, 'Alice', 'alice@test.com', 30);
            INSERT INTO users (id, name, email, age) VALUES (2, 'Bob', 'bob@test.com', 25);
            INSERT INTO users (id, name, email, age) VALUES (3, 'Charlie', 'charlie@test.com', 35);
        """;
        cmd.ExecuteNonQuery();

        var provider = new SqliteProvider(connStr);
        var config   = new NuVatisConfiguration {
            DataSource = new DataSourceConfig {
                ProviderName     = "Sqlite",
                ConnectionString = connStr
            },
            Statements = {
                ["User.GetById"] = new MappedStatement {
                    Id = "GetById", Namespace = "User",
                    Type      = StatementType.Select,
                    SqlSource = "SELECT id, name, email, age FROM users WHERE id = #{Id}"
                },
                ["User.GetByName"] = new MappedStatement {
                    Id = "GetByName", Namespace = "User",
                    Type      = StatementType.Select,
                    SqlSource = "SELECT id, name, email, age FROM users WHERE name = #{Name}"
                },
                ["User.GetAll"] = new MappedStatement {
                    Id = "GetAll", Namespace = "User",
                    Type      = StatementType.Select,
                    SqlSource = "SELECT id, name, email, age FROM users ORDER BY id"
                },
                ["User.Count"] = new MappedStatement {
                    Id = "Count", Namespace = "User",
                    Type      = StatementType.Select,
                    SqlSource = "SELECT COUNT(*) FROM users"
                },
                ["User.Insert"] = new MappedStatement {
                    Id = "Insert", Namespace = "User",
                    Type      = StatementType.Insert,
                    SqlSource = "INSERT INTO users (name, email, age) VALUES (#{Name}, #{Email}, #{Age})"
                },
                ["User.UpdateAge"] = new MappedStatement {
                    Id = "UpdateAge", Namespace = "User",
                    Type      = StatementType.Update,
                    SqlSource = "UPDATE users SET age = #{Age} WHERE name = #{Name}"
                },
                ["User.DeleteByName"] = new MappedStatement {
                    Id = "DeleteByName", Namespace = "User",
                    Type      = StatementType.Delete,
                    SqlSource = "DELETE FROM users WHERE name = #{Name}"
                }
            }
        };

        _factory = new SqlSessionFactory(config, provider);
    }

    [Fact]
    public void SelectOne_Scalar_ReturnsCount() {
        using var session = _factory.OpenSession(autoCommit: true);
        var count = session.SelectOne<long>("User.Count");
        Assert.Equal(3L, count);
    }

    [Fact]
    public async Task SelectOneAsync_Scalar_ReturnsCount() {
        await using var session = _factory.OpenSession(autoCommit: true);
        var count = await session.SelectOneAsync<long>("User.Count");
        Assert.Equal(3L, count);
    }

    [Fact]
    public void SelectOne_ComplexType_WithParameterBinding() {
        using var session = _factory.OpenSession(autoCommit: true);
        var user = session.SelectOne<UserDto>("User.GetById", new { Id = 1 });

        Assert.NotNull(user);
        Assert.Equal(1L, user!.Id);
        Assert.Equal("Alice", user.Name);
        Assert.Equal("alice@test.com", user.Email);
        Assert.Equal(30, user.Age);
    }

    [Fact]
    public void SelectOne_ComplexType_StringParam() {
        using var session = _factory.OpenSession(autoCommit: true);
        var user = session.SelectOne<UserDto>("User.GetByName", new SearchParam { Name = "Bob" });

        Assert.NotNull(user);
        Assert.Equal(2L, user!.Id);
        Assert.Equal("Bob", user.Name);
        Assert.Equal(25, user.Age);
    }

    [Fact]
    public void SelectOne_NoMatch_ReturnsNull() {
        using var session = _factory.OpenSession(autoCommit: true);
        var user = session.SelectOne<UserDto>("User.GetByName", new SearchParam { Name = "Nobody" });
        Assert.Null(user);
    }

    [Fact]
    public void SelectList_ReturnsAllRows() {
        using var session = _factory.OpenSession(autoCommit: true);
        var users = session.SelectList<UserDto>("User.GetAll");

        Assert.Equal(3, users.Count);
        Assert.Equal("Alice", users[0].Name);
        Assert.Equal("Bob", users[1].Name);
        Assert.Equal("Charlie", users[2].Name);
    }

    [Fact]
    public async Task SelectListAsync_ReturnsAllRows() {
        await using var session = _factory.OpenSession(autoCommit: true);
        var users = await session.SelectListAsync<UserDto>("User.GetAll");

        Assert.Equal(3, users.Count);
        Assert.Equal(30, users[0].Age);
        Assert.Equal(25, users[1].Age);
        Assert.Equal(35, users[2].Age);
    }

    [Fact]
    public void Insert_WithParameterBinding_PersistsData() {
        using var session = _factory.OpenSession(autoCommit: false);
        var affected = session.Insert("User.Insert", new InsertParam {
            Name  = "David",
            Email = "david@test.com",
            Age   = 28
        });
        Assert.Equal(1, affected);
        session.Commit();

        using var verify = _factory.OpenSession(autoCommit: true);
        var count = verify.SelectOne<long>("User.Count");
        Assert.Equal(4L, count);

        var david = verify.SelectOne<UserDto>("User.GetByName", new SearchParam { Name = "David" });
        Assert.NotNull(david);
        Assert.Equal("david@test.com", david!.Email);
        Assert.Equal(28, david.Age);
    }

    [Fact]
    public void Update_WithParameterBinding_ModifiesData() {
        using var session = _factory.OpenSession(autoCommit: false);
        var affected = session.Update("User.UpdateAge", new { Name = "Alice", Age = 31 });
        Assert.Equal(1, affected);
        session.Commit();

        using var verify = _factory.OpenSession(autoCommit: true);
        var alice = verify.SelectOne<UserDto>("User.GetByName", new SearchParam { Name = "Alice" });
        Assert.NotNull(alice);
        Assert.Equal(31, alice!.Age);
    }

    [Fact]
    public void Delete_WithParameterBinding_RemovesData() {
        using var session = _factory.OpenSession(autoCommit: false);
        session.Insert("User.Insert", new InsertParam { Name = "Temp", Email = "t@t.com", Age = 1 });
        session.Commit();

        using var session2 = _factory.OpenSession(autoCommit: false);
        var affected = session2.Delete("User.DeleteByName", new SearchParam { Name = "Temp" });
        Assert.Equal(1, affected);
        session2.Commit();

        using var verify = _factory.OpenSession(autoCommit: true);
        var temp = verify.SelectOne<UserDto>("User.GetByName", new SearchParam { Name = "Temp" });
        Assert.Null(temp);
    }

    [Fact]
    public void FullCrudCycle_InsertSelectUpdateDelete() {
        using var session = _factory.OpenSession(autoCommit: false);

        session.Insert("User.Insert", new InsertParam {
            Name = "Eve", Email = "eve@test.com", Age = 22
        });
        session.Commit();

        using var s2 = _factory.OpenSession(autoCommit: true);
        var eve = s2.SelectOne<UserDto>("User.GetByName", new SearchParam { Name = "Eve" });
        Assert.NotNull(eve);
        Assert.Equal(22, eve!.Age);

        using var s3 = _factory.OpenSession(autoCommit: false);
        s3.Update("User.UpdateAge", new { Name = "Eve", Age = 23 });
        s3.Commit();

        using var s4 = _factory.OpenSession(autoCommit: true);
        var eveUpdated = s4.SelectOne<UserDto>("User.GetByName", new SearchParam { Name = "Eve" });
        Assert.Equal(23, eveUpdated!.Age);

        using var s5 = _factory.OpenSession(autoCommit: false);
        s5.Delete("User.DeleteByName", new SearchParam { Name = "Eve" });
        s5.Commit();

        using var s6 = _factory.OpenSession(autoCommit: true);
        var count = s6.SelectOne<long>("User.Count");
        Assert.Equal(3L, count);
    }

    [Fact]
    public async Task FullCrudCycleAsync() {
        await using var session = _factory.OpenSession(autoCommit: false);

        await session.InsertAsync("User.Insert", new InsertParam {
            Name = "Frank", Email = "frank@test.com", Age = 40
        });
        await session.CommitAsync();

        await using var s2 = _factory.OpenSession(autoCommit: true);
        var frank = await s2.SelectOneAsync<UserDto>("User.GetByName", new SearchParam { Name = "Frank" });
        Assert.NotNull(frank);
        Assert.Equal(40, frank!.Age);

        await using var s3 = _factory.OpenSession(autoCommit: false);
        await s3.DeleteAsync("User.DeleteByName", new SearchParam { Name = "Frank" });
        await s3.CommitAsync();

        await using var s4 = _factory.OpenSession(autoCommit: true);
        var count = await s4.SelectOneAsync<long>("User.Count");
        Assert.Equal(3L, count);
    }

    public void Dispose() {
        _keepAlive.Dispose();
    }
}
