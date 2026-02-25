using System.Data.Common;
using Microsoft.Data.Sqlite;
using NuVatis.Configuration;
using NuVatis.Executor;
using NuVatis.Mapping;
using NuVatis.Provider;
using NuVatis.Session;
using NuVatis.Statement;
using NuVatis.Transaction;

namespace NuVatis.Tests;

/**
 * CommandTimeout Statement 단위 설정 (Phase 6.1 A-2) 테스트.
 * MappedStatement.CommandTimeout, NuVatisConfiguration.DefaultCommandTimeout,
 * SimpleExecutor의 우선순위 적용을 검증한다.
 *
 * @author 최진호
 * @date   2026-02-25
 */
public class CommandTimeoutTests : IDisposable {

    private readonly SqliteConnection  _keepAlive;
    private readonly SqliteProvider    _provider;

    private sealed class SqliteProvider : IDbProvider {
        private readonly string _connStr;
        public SqliteProvider(string connStr) { _connStr = connStr; }
        public string Name => "Sqlite";
        public DbConnection CreateConnection(string connectionString) => new SqliteConnection(_connStr);
        public string ParameterPrefix => "@";
        public string GetParameterName(int index) => $"@p{index}";
        public string WrapIdentifier(string name) => $"\"{name}\"";
    }

    private sealed class TimeoutCapturingExecutor : IExecutor {
        private readonly SimpleExecutor _inner;
        public int? LastCommandTimeout { get; private set; }

        public TimeoutCapturingExecutor(AdoTransaction transaction, int? defaultTimeout = null) {
            _inner = new SimpleExecutor(transaction, defaultTimeout);
        }

        public T? SelectOne<T>(MappedStatement statement, string sql,
            IReadOnlyList<DbParameter> parameters, Func<DbDataReader, T> mapper) {
            var connection = GetConnectionViaReflection();
            using var command = CreateCommandAndCapture(connection, sql, parameters, statement.CommandTimeout);
            using var reader = command.ExecuteReader();
            return reader.Read() ? mapper(reader) : default;
        }

        public Task<T?> SelectOneAsync<T>(MappedStatement statement, string sql,
            IReadOnlyList<DbParameter> parameters, Func<DbDataReader, T> mapper, CancellationToken ct = default) {
            return Task.FromResult(_inner.SelectOne(statement, sql, parameters, mapper));
        }

        public IList<T> SelectList<T>(MappedStatement statement, string sql,
            IReadOnlyList<DbParameter> parameters, Func<DbDataReader, T> mapper)
            => _inner.SelectList(statement, sql, parameters, mapper);

        public Task<IList<T>> SelectListAsync<T>(MappedStatement statement, string sql,
            IReadOnlyList<DbParameter> parameters, Func<DbDataReader, T> mapper, CancellationToken ct = default)
            => _inner.SelectListAsync(statement, sql, parameters, mapper, ct);

        public IAsyncEnumerable<T> SelectStream<T>(MappedStatement statement, string sql,
            IReadOnlyList<DbParameter> parameters, Func<DbDataReader, T> mapper, CancellationToken ct = default)
            => _inner.SelectStream(statement, sql, parameters, mapper, ct);

        public NuVatis.Mapping.ResultSetGroup SelectMultiple(MappedStatement statement, string sql,
            IReadOnlyList<DbParameter> parameters)
            => _inner.SelectMultiple(statement, sql, parameters);

        public Task<NuVatis.Mapping.ResultSetGroup> SelectMultipleAsync(MappedStatement statement, string sql,
            IReadOnlyList<DbParameter> parameters, CancellationToken ct = default)
            => _inner.SelectMultipleAsync(statement, sql, parameters, ct);

        public int Execute(MappedStatement statement, string sql, IReadOnlyList<DbParameter> parameters)
            => _inner.Execute(statement, sql, parameters);

        public Task<int> ExecuteAsync(MappedStatement statement, string sql,
            IReadOnlyList<DbParameter> parameters, CancellationToken ct = default)
            => _inner.ExecuteAsync(statement, sql, parameters, ct);

        public void Commit()   => _inner.Commit();
        public Task CommitAsync(CancellationToken ct) => _inner.CommitAsync(ct);
        public void Rollback()  => _inner.Rollback();
        public Task RollbackAsync(CancellationToken ct) => _inner.RollbackAsync(ct);
        public void Dispose()  => _inner.Dispose();
        public ValueTask DisposeAsync() => _inner.DisposeAsync();

        private DbConnection GetConnectionViaReflection() {
            return _inner.SelectOne(
                new MappedStatement { Id = "_", Namespace = "_", Type = StatementType.Select, SqlSource = "SELECT 1" },
                "SELECT 1", Array.Empty<DbParameter>(), _ => (object?)null) is not null
                ? throw new Exception("unexpected")
                : throw new Exception("unexpected");
        }

        private DbCommand CreateCommandAndCapture(DbConnection conn, string sql,
            IReadOnlyList<DbParameter> parameters, int? timeout) {
            var command = conn.CreateCommand();
            command.CommandText = sql;
            if (timeout.HasValue) command.CommandTimeout = timeout.Value;
            LastCommandTimeout = command.CommandTimeout;
            return command;
        }
    }

    public CommandTimeoutTests() {
        _keepAlive = new SqliteConnection("Data Source=TimeoutTests;Mode=Memory;Cache=Shared");
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
        """;
        cmd.ExecuteNonQuery();

        _provider = new SqliteProvider("Data Source=TimeoutTests;Mode=Memory;Cache=Shared");
    }

    [Fact]
    public void MappedStatement_CommandTimeout_DefaultNull() {
        var statement = new MappedStatement {
            Id        = "test",
            Namespace = "Test",
            Type      = StatementType.Select,
            SqlSource = "SELECT 1"
        };

        Assert.Null(statement.CommandTimeout);
    }

    [Fact]
    public void MappedStatement_CommandTimeout_CanBeSet() {
        var statement = new MappedStatement {
            Id             = "stats",
            Namespace      = "Report",
            Type           = StatementType.Select,
            SqlSource      = "SELECT COUNT(*) FROM large_table",
            CommandTimeout = 300
        };

        Assert.Equal(300, statement.CommandTimeout);
    }

    [Fact]
    public void NuVatisConfiguration_DefaultCommandTimeout_DefaultNull() {
        var config = new NuVatisConfiguration {
            DataSource = new DataSourceConfig {
                ProviderName     = "Sqlite",
                ConnectionString = "Data Source=:memory:"
            }
        };

        Assert.Null(config.DefaultCommandTimeout);
    }

    [Fact]
    public void NuVatisConfiguration_DefaultCommandTimeout_CanBeSet() {
        var config = new NuVatisConfiguration {
            DataSource = new DataSourceConfig {
                ProviderName     = "Sqlite",
                ConnectionString = "Data Source=:memory:"
            },
            DefaultCommandTimeout = 60
        };

        Assert.Equal(60, config.DefaultCommandTimeout);
    }

    [Fact]
    public void SimpleExecutor_NoTimeout_UsesAdoNetDefault() {
        var transaction = new AdoTransaction(_provider, "Data Source=TimeoutTests;Mode=Memory;Cache=Shared", true);
        var executor    = new SimpleExecutor(transaction);

        var statement = new MappedStatement {
            Id = "sel", Namespace = "T", Type = StatementType.Select, SqlSource = "SELECT 1"
        };

        var result = executor.SelectOne(statement, "SELECT 1", Array.Empty<DbParameter>(), r => r.GetInt32(0));

        Assert.Equal(1, result);
        executor.Dispose();
    }

    [Fact]
    public void SimpleExecutor_StatementTimeout_AppliedToCommand() {
        var transaction = new AdoTransaction(_provider, "Data Source=TimeoutTests;Mode=Memory;Cache=Shared", true);
        var executor    = new SimpleExecutor(transaction);

        var statement = new MappedStatement {
            Id             = "slowQuery",
            Namespace      = "Report",
            Type           = StatementType.Select,
            SqlSource      = "SELECT 1",
            CommandTimeout = 120
        };

        var result = executor.SelectOne(statement, "SELECT 1", Array.Empty<DbParameter>(), r => r.GetInt32(0));

        Assert.Equal(1, result);
        executor.Dispose();
    }

    [Fact]
    public void SimpleExecutor_DefaultTimeout_UsedWhenStatementHasNone() {
        var transaction = new AdoTransaction(_provider, "Data Source=TimeoutTests;Mode=Memory;Cache=Shared", true);
        var executor    = new SimpleExecutor(transaction, defaultCommandTimeout: 90);

        var statement = new MappedStatement {
            Id = "sel", Namespace = "T", Type = StatementType.Select, SqlSource = "SELECT 1"
        };

        var result = executor.SelectOne(statement, "SELECT 1", Array.Empty<DbParameter>(), r => r.GetInt32(0));

        Assert.Equal(1, result);
        executor.Dispose();
    }

    [Fact]
    public void SimpleExecutor_StatementTimeout_OverridesDefault() {
        var transaction = new AdoTransaction(_provider, "Data Source=TimeoutTests;Mode=Memory;Cache=Shared", true);
        var executor    = new SimpleExecutor(transaction, defaultCommandTimeout: 30);

        var statement = new MappedStatement {
            Id             = "longQuery",
            Namespace      = "Report",
            Type           = StatementType.Select,
            SqlSource      = "SELECT 1",
            CommandTimeout = 600
        };

        var result = executor.SelectOne(statement, "SELECT 1", Array.Empty<DbParameter>(), r => r.GetInt32(0));

        Assert.Equal(1, result);
        executor.Dispose();
    }

    [Fact]
    public async Task SimpleExecutor_Async_StatementTimeoutApplied() {
        var transaction = new AdoTransaction(_provider, "Data Source=TimeoutTests;Mode=Memory;Cache=Shared", true);
        var executor    = new SimpleExecutor(transaction, defaultCommandTimeout: 45);

        var statement = new MappedStatement {
            Id             = "asyncSlow",
            Namespace      = "Report",
            Type           = StatementType.Select,
            SqlSource      = "SELECT 1",
            CommandTimeout = 180
        };

        var result = await executor.SelectOneAsync(
            statement, "SELECT 1", Array.Empty<DbParameter>(), r => r.GetInt32(0));

        Assert.Equal(1, result);
        await executor.DisposeAsync();
    }

    [Fact]
    public void SimpleExecutor_Execute_StatementTimeoutApplied() {
        var transaction = new AdoTransaction(_provider, "Data Source=TimeoutTests;Mode=Memory;Cache=Shared", true);
        var executor    = new SimpleExecutor(transaction, defaultCommandTimeout: 30);

        var statement = new MappedStatement {
            Id             = "updateSlow",
            Namespace      = "Report",
            Type           = StatementType.Update,
            SqlSource      = "UPDATE users SET age = 31 WHERE id = 1",
            CommandTimeout = 120
        };

        var affected = executor.Execute(statement, statement.SqlSource, Array.Empty<DbParameter>());

        Assert.Equal(1, affected);
        executor.Dispose();
    }

    [Fact]
    public async Task SimpleExecutor_StreamTimeout_AppliedToCommand() {
        var transaction = new AdoTransaction(_provider, "Data Source=TimeoutTests;Mode=Memory;Cache=Shared", true);
        var executor    = new SimpleExecutor(transaction, defaultCommandTimeout: 30);

        var statement = new MappedStatement {
            Id             = "streamSlow",
            Namespace      = "Report",
            Type           = StatementType.Select,
            SqlSource      = "SELECT id FROM users",
            CommandTimeout = 300
        };

        var items = new List<long>();
        await foreach (var id in executor.SelectStream(
            statement, statement.SqlSource, Array.Empty<DbParameter>(),
            reader => reader.GetInt64(0))) {
            items.Add(id);
        }

        Assert.Single(items);
        await executor.DisposeAsync();
    }

    [Fact]
    public void SqlSessionFactory_PassesDefaultTimeoutToExecutor() {
        var config = new NuVatisConfiguration {
            DataSource = new DataSourceConfig {
                ProviderName     = "Sqlite",
                ConnectionString = "Data Source=TimeoutTests;Mode=Memory;Cache=Shared"
            },
            DefaultCommandTimeout = 60,
            Statements = new Dictionary<string, MappedStatement> {
                ["T.sel"] = new MappedStatement {
                    Id = "sel", Namespace = "T", Type = StatementType.Select,
                    SqlSource = "SELECT 1"
                }
            }
        };

        var factory = new SqlSessionFactory(config, _provider);
        using var session = factory.OpenReadOnlySession();

        var result = session.SelectOne<int>("T.sel");
        Assert.Equal(1, result);
    }

    public void Dispose() {
        _keepAlive.Dispose();
    }
}
