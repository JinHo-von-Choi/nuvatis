using System.Data.Common;
using Microsoft.Data.Sqlite;
using NuVatis.Configuration;
using NuVatis.Executor;
using NuVatis.Provider;
using NuVatis.Session;
using NuVatis.Statement;
using NuVatis.Transaction;
using Xunit;

namespace NuVatis.Tests.E2E;

/**
 * SqlSession / SimpleExecutor E2E 통합 테스트.
 * SQLite 인메모리 DB로 전체 파이프라인을 검증한다.
 *
 * SELECT 계열은 SimpleExecutor를 직접 사용하여 mapper 함수를 제공한다.
 * (SqlSession의 SELECT는 SG가 생성한 Proxy가 mapper를 주입하는 설계)
 *
 * Write 계열(Insert/Update/Delete)은 SqlSession 경유로 검증한다.
 *
 * @author 최진호
 * @date   2026-02-24
 */
[Trait("Category", "E2E")]
public class SqlSessionE2ETests : IDisposable {

    private readonly SqliteConnection _keepAlive;
    private readonly SqliteProvider _provider;
    private readonly NuVatisConfiguration _config;
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

    private record UserDto(long Id, string Name, int Age);

    private static readonly MappedStatement SelectCount = new() {
        Id = "Count", Namespace = "User", Type = StatementType.Select,
        SqlSource = "SELECT COUNT(*) FROM users"
    };

    private static readonly MappedStatement SelectAll = new() {
        Id = "All", Namespace = "User", Type = StatementType.Select,
        SqlSource = "SELECT id, name, age FROM users ORDER BY id"
    };

    private static readonly MappedStatement SelectById = new() {
        Id = "ById", Namespace = "User", Type = StatementType.Select,
        SqlSource = "SELECT id, name, age FROM users WHERE id = 1"
    };

    private static readonly MappedStatement InsertUser = new() {
        Id = "Insert", Namespace = "User", Type = StatementType.Insert,
        SqlSource = "INSERT INTO users (id, name, age) VALUES (99, 'Test', 20)"
    };

    private static readonly MappedStatement UpdateUser = new() {
        Id = "Update", Namespace = "User", Type = StatementType.Update,
        SqlSource = "UPDATE users SET name = 'Updated' WHERE id = 1"
    };

    private static readonly MappedStatement DeleteUser = new() {
        Id = "Delete", Namespace = "User", Type = StatementType.Delete,
        SqlSource = "DELETE FROM users WHERE id = 99"
    };

    public SqlSessionE2ETests() {
        _keepAlive = new SqliteConnection("Data Source=E2ETests;Mode=Memory;Cache=Shared");
        _keepAlive.Open();

        using var cmd = _keepAlive.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS users (
                id   INTEGER PRIMARY KEY,
                name TEXT    NOT NULL,
                age  INTEGER NOT NULL
            );
            DELETE FROM users;
            INSERT INTO users (id, name, age) VALUES (1, 'Alice', 30);
            INSERT INTO users (id, name, age) VALUES (2, 'Bob', 25);
            INSERT INTO users (id, name, age) VALUES (3, 'Charlie', 35);
        """;
        cmd.ExecuteNonQuery();

        _provider = new SqliteProvider("Data Source=E2ETests;Mode=Memory;Cache=Shared");
        _config   = new NuVatisConfiguration {
            DataSource = new DataSourceConfig {
                ProviderName     = "Sqlite",
                ConnectionString = "Data Source=E2ETests;Mode=Memory;Cache=Shared"
            },
            Statements = {
                [InsertUser.FullId] = InsertUser,
                [UpdateUser.FullId] = UpdateUser,
                [DeleteUser.FullId] = DeleteUser
            }
        };
        _factory = new SqlSessionFactory(_config, _provider);
    }

    private SimpleExecutor CreateExecutor(bool autoCommit = true) {
        var tx = new AdoTransaction(_provider, _config.DataSource.ConnectionString, autoCommit);
        return new SimpleExecutor(tx);
    }

    private static long ReadScalar(DbDataReader reader) => reader.GetInt64(0);

    private static UserDto ReadUser(DbDataReader reader) =>
        new(reader.GetInt64(0), reader.GetString(1), reader.GetInt32(2));

    [Fact]
    public void Executor_SelectOne_ReturnsScalar() {
        using var executor = CreateExecutor();
        var count = executor.SelectOne(SelectCount, SelectCount.SqlSource,
            Array.Empty<DbParameter>(), ReadScalar);
        Assert.Equal(3L, count);
    }

    [Fact]
    public async Task Executor_SelectOneAsync_ReturnsScalar() {
        await using var executor = CreateExecutor();
        var count = await executor.SelectOneAsync(SelectCount, SelectCount.SqlSource,
            Array.Empty<DbParameter>(), ReadScalar);
        Assert.Equal(3L, count);
    }

    [Fact]
    public void Executor_SelectOne_ReturnsDto() {
        using var executor = CreateExecutor();
        var user = executor.SelectOne(SelectById, SelectById.SqlSource,
            Array.Empty<DbParameter>(), ReadUser);

        Assert.NotNull(user);
        Assert.Equal(1L, user!.Id);
        Assert.Equal("Alice", user.Name);
        Assert.Equal(30, user.Age);
    }

    [Fact]
    public void Executor_SelectList_ReturnsAllRows() {
        using var executor = CreateExecutor();
        var users = executor.SelectList(SelectAll, SelectAll.SqlSource,
            Array.Empty<DbParameter>(), ReadUser);

        Assert.Equal(3, users.Count);
        Assert.Equal("Alice", users[0].Name);
        Assert.Equal("Bob", users[1].Name);
        Assert.Equal("Charlie", users[2].Name);
    }

    [Fact]
    public async Task Executor_SelectListAsync_ReturnsAllRows() {
        await using var executor = CreateExecutor();
        var users = await executor.SelectListAsync(SelectAll, SelectAll.SqlSource,
            Array.Empty<DbParameter>(), ReadUser);

        Assert.Equal(3, users.Count);
        Assert.Equal(25, users[1].Age);
    }

    [Fact]
    public void Session_Insert_ReturnsAffectedRows() {
        using var session = _factory.OpenSession(autoCommit: true);
        var affected = session.Insert("User.Insert");
        Assert.Equal(1, affected);
    }

    [Fact]
    public async Task Session_InsertAsync_ReturnsAffectedRows() {
        await using var session = _factory.OpenSession(autoCommit: true);
        var affected = await session.InsertAsync("User.Insert");
        Assert.Equal(1, affected);
    }

    [Fact]
    public void Session_Update_ReturnsAffectedRows() {
        using var session = _factory.OpenSession(autoCommit: true);
        var affected = session.Update("User.Update");
        Assert.Equal(1, affected);
    }

    [Fact]
    public void Session_Delete_ReturnsAffectedRows() {
        using var session = _factory.OpenSession(autoCommit: true);
        session.Insert("User.Insert");
        var affected = session.Delete("User.Delete");
        Assert.Equal(1, affected);
    }

    [Fact]
    public void Session_Commit_PersistsData() {
        using var session = _factory.OpenSession(autoCommit: false);
        session.Insert("User.Insert");
        session.Commit();

        using var executor = CreateExecutor();
        var count = executor.SelectOne(SelectCount, SelectCount.SqlSource,
            Array.Empty<DbParameter>(), ReadScalar);
        Assert.Equal(4L, count);
    }

    [Fact]
    public void Session_Rollback_DiscardsData() {
        using var session = _factory.OpenSession(autoCommit: false);
        session.Insert("User.Insert");
        session.Rollback();

        using var executor = CreateExecutor();
        var count = executor.SelectOne(SelectCount, SelectCount.SqlSource,
            Array.Empty<DbParameter>(), ReadScalar);
        Assert.Equal(3L, count);
    }

    [Fact]
    public async Task Session_CommitAsync_PersistsData() {
        await using var session = _factory.OpenSession(autoCommit: false);
        await session.InsertAsync("User.Insert");
        await session.CommitAsync();

        await using var executor = CreateExecutor();
        var count = await executor.SelectOneAsync(SelectCount, SelectCount.SqlSource,
            Array.Empty<DbParameter>(), ReadScalar);
        Assert.Equal(4L, count);
    }

    [Fact]
    public async Task Session_ExecuteInTransaction_CommitsOnSuccess() {
        await using var session = _factory.OpenSession(autoCommit: false);
        await session.ExecuteInTransactionAsync(async () => {
            await session.InsertAsync("User.Insert");
        });

        await using var executor = CreateExecutor();
        var count = await executor.SelectOneAsync(SelectCount, SelectCount.SqlSource,
            Array.Empty<DbParameter>(), ReadScalar);
        Assert.Equal(4L, count);
    }

    [Fact]
    public async Task Session_ExecuteInTransaction_RollbacksOnException() {
        await using var session = _factory.OpenSession(autoCommit: false);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => {
            await session.ExecuteInTransactionAsync(async () => {
                await session.InsertAsync("User.Insert");
                throw new InvalidOperationException("Simulated failure");
            });
        });

        await using var executor = CreateExecutor();
        var count = await executor.SelectOneAsync(SelectCount, SelectCount.SqlSource,
            Array.Empty<DbParameter>(), ReadScalar);
        Assert.Equal(3L, count);
    }

    [Fact]
    public void Session_UnknownStatement_Throws() {
        using var session = _factory.OpenSession(autoCommit: true);
        Assert.Throws<InvalidOperationException>(() =>
            session.Insert("NonExistent.Statement"));
    }

    [Fact]
    public void Session_DisposedSession_Throws() {
        var session = _factory.OpenSession(autoCommit: true);
        session.Dispose();
        Assert.Throws<ObjectDisposedException>(() =>
            session.Insert("User.Insert"));
    }

    [Fact]
    public void Session_GetMapper_WithoutFactory_Throws() {
        using var session = _factory.OpenSession(autoCommit: true);
        Assert.Throws<InvalidOperationException>(() =>
            session.GetMapper<IDisposable>());
    }

    [Fact]
    public void Executor_Transaction_Commit_And_Verify() {
        var tx       = new AdoTransaction(_provider, _config.DataSource.ConnectionString, autoCommit: false);
        using var exe = new SimpleExecutor(tx);

        var affected = exe.Execute(InsertUser, InsertUser.SqlSource, Array.Empty<DbParameter>());
        Assert.Equal(1, affected);
        exe.Commit();

        using var verifier = CreateExecutor();
        var count = verifier.SelectOne(SelectCount, SelectCount.SqlSource,
            Array.Empty<DbParameter>(), ReadScalar);
        Assert.Equal(4L, count);
    }

    [Fact]
    public void Executor_Transaction_Rollback_And_Verify() {
        var tx       = new AdoTransaction(_provider, _config.DataSource.ConnectionString, autoCommit: false);
        using var exe = new SimpleExecutor(tx);

        exe.Execute(InsertUser, InsertUser.SqlSource, Array.Empty<DbParameter>());
        exe.Rollback();

        using var verifier = CreateExecutor();
        var count = verifier.SelectOne(SelectCount, SelectCount.SqlSource,
            Array.Empty<DbParameter>(), ReadScalar);
        Assert.Equal(3L, count);
    }

    public void Dispose() {
        _keepAlive.Dispose();
    }
}
