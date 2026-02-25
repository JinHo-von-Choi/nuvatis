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

namespace NuVatis.Tests;

/**
 * IAsyncEnumerable<T> 스트리밍 조회 (Phase 6.1 A-1) 테스트.
 * SQLite 인메모리 DB로 SelectStream의 전체 동작을 검증한다.
 *
 * @author 최진호
 * @date   2026-02-25
 */
public class StreamingTests : IDisposable {

    private readonly SqliteConnection  _keepAlive;
    private readonly SqliteProvider    _provider;
    private readonly NuVatisConfiguration _config;

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

    private static readonly MappedStatement SelectAll = new() {
        Id        = "All",
        Namespace = "User",
        Type      = StatementType.Select,
        SqlSource = "SELECT id, name, age FROM users ORDER BY id"
    };

    private static readonly MappedStatement SelectCount = new() {
        Id        = "Count",
        Namespace = "User",
        Type      = StatementType.Select,
        SqlSource = "SELECT COUNT(*) FROM users"
    };

    public StreamingTests() {
        _keepAlive = new SqliteConnection("Data Source=StreamingTests;Mode=Memory;Cache=Shared");
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
            INSERT INTO users (id, name, age) VALUES (4, 'Diana', 28);
            INSERT INTO users (id, name, age) VALUES (5, 'Eve', 22);
        """;
        cmd.ExecuteNonQuery();

        _provider = new SqliteProvider("Data Source=StreamingTests;Mode=Memory;Cache=Shared");
        _config   = new NuVatisConfiguration {
            DataSource = new DataSourceConfig {
                ProviderName     = "Sqlite",
                ConnectionString = "Data Source=StreamingTests;Mode=Memory;Cache=Shared"
            },
            Statements = new Dictionary<string, MappedStatement> {
                [SelectAll.FullId]   = SelectAll,
                [SelectCount.FullId] = SelectCount
            }
        };
    }

    [Fact]
    public async Task SelectStream_ReturnsAllRows() {
        var transaction = new AdoTransaction(_provider, _config.DataSource.ConnectionString, autoCommit: true);
        var executor    = new SimpleExecutor(transaction);

        await using (executor) {
            var items = new List<long>();
            await foreach (var id in executor.SelectStream(
                SelectAll, SelectAll.SqlSource, Array.Empty<DbParameter>(),
                reader => reader.GetInt64(0))) {
                items.Add(id);
            }

            Assert.Equal(5, items.Count);
            Assert.Equal(new long[] { 1, 2, 3, 4, 5 }, items);
        }
    }

    [Fact]
    public async Task SelectStream_ResultsMatchSelectList() {
        var transaction = new AdoTransaction(_provider, _config.DataSource.ConnectionString, autoCommit: true);
        var executor    = new SimpleExecutor(transaction);

        await using (executor) {
            var listResult = await executor.SelectListAsync(
                SelectAll, SelectAll.SqlSource, Array.Empty<DbParameter>(),
                reader => new UserDto(reader.GetInt64(0), reader.GetString(1), reader.GetInt32(2)));

            var streamResult = new List<UserDto>();
            await foreach (var user in executor.SelectStream(
                SelectAll, SelectAll.SqlSource, Array.Empty<DbParameter>(),
                reader => new UserDto(reader.GetInt64(0), reader.GetString(1), reader.GetInt32(2)))) {
                streamResult.Add(user);
            }

            Assert.Equal(listResult.Count, streamResult.Count);
            for (var i = 0; i < listResult.Count; i++) {
                Assert.Equal(listResult[i].Id, streamResult[i].Id);
                Assert.Equal(listResult[i].Name, streamResult[i].Name);
                Assert.Equal(listResult[i].Age, streamResult[i].Age);
            }
        }
    }

    [Fact]
    public async Task SelectStream_EmptyResult_YieldsNothing() {
        var emptySelect = new MappedStatement {
            Id        = "Empty",
            Namespace = "User",
            Type      = StatementType.Select,
            SqlSource = "SELECT id, name, age FROM users WHERE id = -999"
        };

        var transaction = new AdoTransaction(_provider, _config.DataSource.ConnectionString, autoCommit: true);
        var executor    = new SimpleExecutor(transaction);

        await using (executor) {
            var count = 0;
            await foreach (var _ in executor.SelectStream(
                emptySelect, emptySelect.SqlSource, Array.Empty<DbParameter>(),
                reader => reader.GetInt64(0))) {
                count++;
            }

            Assert.Equal(0, count);
        }
    }

    [Fact]
    public async Task SelectStream_EarlyBreak_CleansUpResources() {
        var transaction = new AdoTransaction(_provider, _config.DataSource.ConnectionString, autoCommit: true);
        var executor    = new SimpleExecutor(transaction);

        await using (executor) {
            var collected = new List<long>();
            await foreach (var id in executor.SelectStream(
                SelectAll, SelectAll.SqlSource, Array.Empty<DbParameter>(),
                reader => reader.GetInt64(0))) {
                collected.Add(id);
                if (collected.Count == 2) break;
            }

            Assert.Equal(2, collected.Count);
            Assert.Equal(new long[] { 1, 2 }, collected);
        }
    }

    [Fact]
    public async Task SelectStream_Cancellation_StopsEnumeration() {
        var transaction = new AdoTransaction(_provider, _config.DataSource.ConnectionString, autoCommit: true);
        var executor    = new SimpleExecutor(transaction);

        await using (executor) {
            var cts       = new CancellationTokenSource();
            var collected = new List<long>();

            var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => {
                await foreach (var id in executor.SelectStream(
                    SelectAll, SelectAll.SqlSource, Array.Empty<DbParameter>(),
                    reader => reader.GetInt64(0), cts.Token)) {
                    collected.Add(id);
                    if (collected.Count == 2) cts.Cancel();
                }
            });

            Assert.IsAssignableFrom<OperationCanceledException>(ex);
            Assert.Equal(2, collected.Count);
        }
    }

    [Fact]
    public async Task SqlSession_SelectStream_WithInterceptors() {
        var factory  = new SqlSessionFactory(_config, _provider);
        var pipeline = new InterceptorPipeline();
        var log      = new StreamInterceptorLog();
        pipeline.Add(log);
        factory.AddInterceptor(log);

        using var session = factory.OpenReadOnlySession();

        var results = new List<long>();
        await foreach (var id in session.SelectStream<long>(SelectCount.FullId)) {
            results.Add(id);
        }

        Assert.Single(results);
        Assert.True(log.BeforeCalled, "Before interceptor should be called");
        Assert.True(log.AfterCalled, "After interceptor should be called");
        Assert.True(log.AfterElapsedMs >= 0, "Elapsed time should be set");
    }

    [Fact]
    public async Task SqlSession_SelectStream_EarlyBreak_StillCallsAfterInterceptor() {
        var factory = new SqlSessionFactory(_config, _provider);
        var log     = new StreamInterceptorLog();
        factory.AddInterceptor(log);

        using var session = factory.OpenReadOnlySession();

        await foreach (var id in session.SelectStream<long>(SelectAll.FullId)) {
            break;
        }

        Assert.True(log.BeforeCalled, "Before interceptor should be called");
        Assert.True(log.AfterCalled, "After interceptor should be called even on early break");
    }

    [Fact]
    public async Task SqlSession_SelectStream_LargeDataset() {
        using var cmd = _keepAlive.CreateCommand();
        cmd.CommandText = "DELETE FROM users; ";
        var sb = new System.Text.StringBuilder(cmd.CommandText);
        for (var i = 1; i <= 10000; i++) {
            sb.Append($"INSERT INTO users (id, name, age) VALUES ({i}, 'User{i}', {20 + i % 50});");
        }
        cmd.CommandText = sb.ToString();
        cmd.ExecuteNonQuery();

        var factory = new SqlSessionFactory(_config, _provider);
        using var session = factory.OpenReadOnlySession();

        var count = 0;
        await foreach (var _ in session.SelectStream<long>(SelectCount.FullId)) {
            count++;
        }

        Assert.Equal(1, count);

        var rowCount = 0;
        await foreach (var _ in session.SelectStream<long>(SelectAll.FullId)) {
            rowCount++;
        }

        Assert.Equal(10000, rowCount);
    }

    public void Dispose() {
        _keepAlive.Dispose();
    }

    private sealed class StreamInterceptorLog : ISqlInterceptor {
        public bool BeforeCalled  { get; private set; }
        public bool AfterCalled   { get; private set; }
        public long AfterElapsedMs { get; private set; } = -1;

        public void BeforeExecute(InterceptorContext context)  => BeforeCalled = true;
        public void AfterExecute(InterceptorContext context) {
            AfterCalled   = true;
            AfterElapsedMs = context.ElapsedMilliseconds;
        }
        public Task BeforeExecuteAsync(InterceptorContext context, CancellationToken ct) {
            BeforeCalled = true;
            return Task.CompletedTask;
        }
        public Task AfterExecuteAsync(InterceptorContext context, CancellationToken ct) {
            AfterCalled    = true;
            AfterElapsedMs = context.ElapsedMilliseconds;
            return Task.CompletedTask;
        }
    }
}
