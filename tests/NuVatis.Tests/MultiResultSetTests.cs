using System.Data.Common;
using Microsoft.Data.Sqlite;
using NuVatis.Configuration;
using NuVatis.Executor;
using NuVatis.Interceptor;
using NuVatis.Mapping;
using NuVatis.Provider;
using NuVatis.Session;
using NuVatis.Statement;
using NuVatis.Transaction;
using StatementType = NuVatis.Statement.StatementType;

namespace NuVatis.Tests;

/**
 * Multi-ResultSet (Phase 6.4 A-3) 테스트.
 * SQLite 인메모리 DB로 ResultSetGroup, SelectMultiple 동작을 검증한다.
 *
 * @author 최진호
 * @date   2026-02-25
 */
public class MultiResultSetTests : IDisposable {

    private readonly SqliteConnection    _keepAlive;
    private readonly SqliteProvider      _provider;
    private readonly NuVatisConfiguration  _config;

    private sealed class SqliteProvider : IDbProvider {
        private readonly string _connStr;
        public SqliteProvider(string connStr) { _connStr = connStr; }
        public string Name => "Sqlite";
        public DbConnection CreateConnection(string connectionString) => new SqliteConnection(_connStr);
        public string ParameterPrefix => "@";
        public string GetParameterName(int index) => $"@p{index}";
        public string WrapIdentifier(string name) => $"\"{name}\"";
    }

    private static readonly MappedStatement MultiSelect = new() {
        Id        = "Overview",
        Namespace = "Dashboard",
        Type      = StatementType.Select,
        SqlSource = "SELECT COUNT(*) FROM users; SELECT id, name FROM users ORDER BY id; SELECT AVG(age) FROM users"
    };

    private static readonly MappedStatement SingleResultSet = new() {
        Id        = "UserCount",
        Namespace = "Dashboard",
        Type      = StatementType.Select,
        SqlSource = "SELECT COUNT(*) FROM users"
    };

    private static readonly MappedStatement EmptyMulti = new() {
        Id        = "Empty",
        Namespace = "Dashboard",
        Type      = StatementType.Select,
        SqlSource = "SELECT id FROM users WHERE id = -999; SELECT name FROM users WHERE id = -999"
    };

    public MultiResultSetTests() {
        _keepAlive = new SqliteConnection("Data Source=MultiResultSetTests;Mode=Memory;Cache=Shared");
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

        _provider = new SqliteProvider("Data Source=MultiResultSetTests;Mode=Memory;Cache=Shared");
        _config   = new NuVatisConfiguration {
            DataSource = new DataSourceConfig {
                ProviderName     = "Sqlite",
                ConnectionString = "Data Source=MultiResultSetTests;Mode=Memory;Cache=Shared"
            },
            Statements = new Dictionary<string, MappedStatement> {
                [MultiSelect.FullId]     = MultiSelect,
                [SingleResultSet.FullId] = SingleResultSet,
                [EmptyMulti.FullId]      = EmptyMulti
            }
        };
    }

    /** --- ResultSetGroup 저수준 테스트 --- */

    [Fact]
    public void Executor_SelectMultiple_ReturnsResultSetGroup() {
        var transaction = new AdoTransaction(_provider, _config.DataSource.ConnectionString, autoCommit: true);
        var executor    = new SimpleExecutor(transaction);

        using (executor)
        using (var results = executor.SelectMultiple(MultiSelect, MultiSelect.SqlSource, Array.Empty<DbParameter>())) {
            Assert.NotNull(results);
            Assert.Equal(0, results.CurrentResultSetIndex);
        }
    }

    [Fact]
    public void ResultSetGroup_Read_ReturnsSingleValue() {
        var transaction = new AdoTransaction(_provider, _config.DataSource.ConnectionString, autoCommit: true);
        var executor    = new SimpleExecutor(transaction);

        using (executor)
        using (var results = executor.SelectMultiple(MultiSelect, MultiSelect.SqlSource, Array.Empty<DbParameter>())) {
            var count = results.Read<long>();
            Assert.Equal(3L, count);
        }
    }

    [Fact]
    public void ResultSetGroup_ReadList_ReturnsAllRows() {
        var transaction = new AdoTransaction(_provider, _config.DataSource.ConnectionString, autoCommit: true);
        var executor    = new SimpleExecutor(transaction);

        using (executor)
        using (var results = executor.SelectMultiple(MultiSelect, MultiSelect.SqlSource, Array.Empty<DbParameter>())) {
            var count = results.Read<long>();
            Assert.Equal(3L, count);

            var users = results.ReadList<UserIdName>();
            Assert.Equal(3, users.Count);
            Assert.Equal("Alice", users[0].Name);
            Assert.Equal("Bob", users[1].Name);
            Assert.Equal("Charlie", users[2].Name);
        }
    }

    [Fact]
    public void ResultSetGroup_MultipleReads_Sequential() {
        var transaction = new AdoTransaction(_provider, _config.DataSource.ConnectionString, autoCommit: true);
        var executor    = new SimpleExecutor(transaction);

        using (executor)
        using (var results = executor.SelectMultiple(MultiSelect, MultiSelect.SqlSource, Array.Empty<DbParameter>())) {
            var count = results.Read<long>();
            Assert.Equal(3L, count);
            Assert.Equal(1, results.CurrentResultSetIndex);

            var users = results.ReadList<UserIdName>();
            Assert.Equal(3, users.Count);
            Assert.Equal(2, results.CurrentResultSetIndex);

            var avgAge = results.Read<long>();
            Assert.Equal(30L, avgAge);
            Assert.Equal(3, results.CurrentResultSetIndex);
        }
    }

    [Fact]
    public void ResultSetGroup_EmptyResults() {
        var transaction = new AdoTransaction(_provider, _config.DataSource.ConnectionString, autoCommit: true);
        var executor    = new SimpleExecutor(transaction);

        using (executor)
        using (var results = executor.SelectMultiple(EmptyMulti, EmptyMulti.SqlSource, Array.Empty<DbParameter>())) {
            var ids = results.ReadList<long>();
            Assert.Empty(ids);

            var names = results.ReadList<string>();
            Assert.Empty(names);
        }
    }

    /** --- SqlSession 통합 테스트 --- */

    [Fact]
    public void SqlSession_SelectMultiple_Works() {
        var factory = new SqlSessionFactory(_config, _provider);
        using var session = factory.OpenReadOnlySession();

        using var results = session.SelectMultiple(MultiSelect.FullId);
        var count = results.Read<long>();
        Assert.Equal(3L, count);

        var users = results.ReadList<UserIdName>();
        Assert.Equal(3, users.Count);
    }

    [Fact]
    public async Task SqlSession_SelectMultipleAsync_Works() {
        var factory = new SqlSessionFactory(_config, _provider);
        using var session = factory.OpenReadOnlySession();

        await using var results = await session.SelectMultipleAsync(MultiSelect.FullId);
        var count = await results.ReadAsync<long>();
        Assert.Equal(3L, count);

        var users = await results.ReadListAsync<UserIdName>();
        Assert.Equal(3, users.Count);
    }

    [Fact]
    public void SqlSession_SelectMultiple_WithInterceptors() {
        var factory = new SqlSessionFactory(_config, _provider);
        var log     = new MultiResultInterceptorLog();
        factory.AddInterceptor(log);

        using var session = factory.OpenReadOnlySession();

        using var results = session.SelectMultiple(MultiSelect.FullId);
        results.Read<long>();

        Assert.True(log.BeforeCalled);
        Assert.True(log.AfterCalled);
    }

    [Fact]
    public void ResultSetGroup_Dispose_CleansUpResources() {
        var transaction = new AdoTransaction(_provider, _config.DataSource.ConnectionString, autoCommit: true);
        var executor    = new SimpleExecutor(transaction);

        ResultSetGroup? group;
        using (executor) {
            group = executor.SelectMultiple(SingleResultSet, SingleResultSet.SqlSource, Array.Empty<DbParameter>());
            group.Dispose();
        }

        Assert.Throws<ObjectDisposedException>(() => group.Read<long>());
    }

    [Fact]
    public async Task ResultSetGroup_DisposeAsync_CleansUpResources() {
        var transaction = new AdoTransaction(_provider, _config.DataSource.ConnectionString, autoCommit: true);
        var executor    = new SimpleExecutor(transaction);

        ResultSetGroup group;
        await using (executor) {
            group = await executor.SelectMultipleAsync(SingleResultSet, SingleResultSet.SqlSource, Array.Empty<DbParameter>());
            await group.DisposeAsync();
        }

        Assert.Throws<ObjectDisposedException>(() => group.Read<long>());
    }

    public void Dispose() {
        _keepAlive.Dispose();
    }

    public class UserIdName {
        public long Id     { get; set; }
        public string Name { get; set; } = "";
    }

    private sealed class MultiResultInterceptorLog : ISqlInterceptor {
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
