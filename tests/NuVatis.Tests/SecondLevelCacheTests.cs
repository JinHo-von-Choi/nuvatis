using System.Data.Common;
using Microsoft.Data.Sqlite;
using NuVatis.Cache;
using NuVatis.Configuration;
using NuVatis.Executor;
using NuVatis.Provider;
using NuVatis.Session;
using NuVatis.Statement;
using NuVatis.Transaction;
using StatementType = NuVatis.Statement.StatementType;

namespace NuVatis.Tests;

/**
 * Second-Level Cache (Phase 6.4 A-4) 테스트.
 * MemoryCacheProvider, 캐시 히트/미스, Write 후 무효화, Thread Safety, LRU Eviction을 검증한다.
 *
 * @author 최진호
 * @date   2026-02-25
 */
public class SecondLevelCacheTests : IDisposable {

    private readonly SqliteConnection   _keepAlive;
    private readonly SqliteProvider     _provider;
    private readonly MemoryCacheProvider _cache;

    private const string CachedNs = "Stats";

    private static readonly MappedStatement SelectCached = new() {
        Id        = "GetCount",
        Namespace = CachedNs,
        Type      = StatementType.Select,
        SqlSource = "SELECT COUNT(*) FROM items",
        UseCache  = true
    };

    private static readonly MappedStatement SelectNonCached = new() {
        Id        = "GetCountNoCache",
        Namespace = CachedNs,
        Type      = StatementType.Select,
        SqlSource = "SELECT COUNT(*) FROM items",
        UseCache  = false
    };

    private static readonly MappedStatement InsertItem = new() {
        Id        = "Add",
        Namespace = CachedNs,
        Type      = StatementType.Insert,
        SqlSource = "INSERT INTO items (name) VALUES ('new')"
    };

    private static readonly MappedStatement SelectWithParam = new() {
        Id        = "GetByName",
        Namespace = CachedNs,
        Type      = StatementType.Select,
        SqlSource = "SELECT COUNT(*) FROM items WHERE name = #{Name}",
        UseCache  = true
    };

    private sealed class SqliteProvider : IDbProvider {
        private readonly string _connStr;
        public SqliteProvider(string connStr) { _connStr = connStr; }
        public string Name => "Sqlite";
        public DbConnection CreateConnection(string connectionString) => new SqliteConnection(_connStr);
        public string ParameterPrefix => "@";
        public string GetParameterName(int index) => $"@p{index}";
        public string WrapIdentifier(string name) => $"\"{name}\"";
    }

    public SecondLevelCacheTests() {
        _keepAlive = new SqliteConnection("Data Source=CacheTests;Mode=Memory;Cache=Shared");
        _keepAlive.Open();

        using var cmd = _keepAlive.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS items (
                id   INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL
            );
            DELETE FROM items;
            INSERT INTO items (name) VALUES ('a');
            INSERT INTO items (name) VALUES ('b');
            INSERT INTO items (name) VALUES ('c');
        """;
        cmd.ExecuteNonQuery();

        _provider = new SqliteProvider("Data Source=CacheTests;Mode=Memory;Cache=Shared");
        _cache    = new MemoryCacheProvider();
        _cache.RegisterNamespace(CachedNs, new CacheConfig { Size = 100 });
    }

    private NuVatisConfiguration CreateConfig() {
        return new NuVatisConfiguration {
            DataSource = new DataSourceConfig {
                ProviderName     = "Sqlite",
                ConnectionString = "Data Source=CacheTests;Mode=Memory;Cache=Shared"
            },
            Statements = new Dictionary<string, MappedStatement> {
                [SelectCached.FullId]    = SelectCached,
                [SelectNonCached.FullId] = SelectNonCached,
                [InsertItem.FullId]      = InsertItem,
                [SelectWithParam.FullId] = SelectWithParam
            },
            CacheProvider = _cache
        };
    }

    /** --- 캐시 히트/미스 --- */

    [Fact]
    public void SelectOne_CacheHit_SkipsExecutor() {
        var config  = CreateConfig();
        var factory = new SqlSessionFactory(config, _provider);

        long firstResult;
        using (var session = factory.OpenReadOnlySession()) {
            firstResult = session.SelectOne<long>(SelectCached.FullId);
            Assert.Equal(3L, firstResult);
        }

        using var cmd = _keepAlive.CreateCommand();
        cmd.CommandText = "INSERT INTO items (name) VALUES ('d')";
        cmd.ExecuteNonQuery();

        using (var session = factory.OpenReadOnlySession()) {
            var secondResult = session.SelectOne<long>(SelectCached.FullId);
            Assert.Equal(3L, secondResult);
        }
    }

    [Fact]
    public void SelectList_CacheHit_ReturnsCachedList() {
        var config  = CreateConfig();
        var factory = new SqlSessionFactory(config, _provider);

        IList<long> firstResult;
        using (var session = factory.OpenReadOnlySession()) {
            firstResult = session.SelectList<long>(SelectCached.FullId);
            Assert.Single(firstResult);
            Assert.Equal(3L, firstResult[0]);
        }

        using (var session = factory.OpenReadOnlySession()) {
            var secondResult = session.SelectList<long>(SelectCached.FullId);
            Assert.Equal(firstResult, secondResult);
        }
    }

    [Fact]
    public void SelectOne_NonCached_AlwaysHitsDb() {
        var config  = CreateConfig();
        var factory = new SqlSessionFactory(config, _provider);

        using (var session = factory.OpenReadOnlySession()) {
            session.SelectOne<long>(SelectNonCached.FullId);
        }

        using var cmd = _keepAlive.CreateCommand();
        cmd.CommandText = "INSERT INTO items (name) VALUES ('e')";
        cmd.ExecuteNonQuery();

        using (var session = factory.OpenReadOnlySession()) {
            var count = session.SelectOne<long>(SelectNonCached.FullId);
            Assert.True(count > 3L);
        }
    }

    [Fact]
    public async Task SelectOneAsync_CacheHit_SkipsExecutor() {
        var config  = CreateConfig();
        var factory = new SqlSessionFactory(config, _provider);

        using (var session = factory.OpenReadOnlySession()) {
            var first = await session.SelectOneAsync<long>(SelectCached.FullId);
            Assert.Equal(3L, first);
        }

        using var cmd = _keepAlive.CreateCommand();
        cmd.CommandText = "INSERT INTO items (name) VALUES ('f')";
        cmd.ExecuteNonQuery();

        using (var session = factory.OpenReadOnlySession()) {
            var second = await session.SelectOneAsync<long>(SelectCached.FullId);
            Assert.Equal(3L, second);
        }
    }

    /** --- Write 후 캐시 무효화 --- */

    [Fact]
    public void Insert_FlushesNamespaceCache() {
        var config  = CreateConfig();
        var factory = new SqlSessionFactory(config, _provider);

        using (var session = factory.OpenReadOnlySession()) {
            var first = session.SelectOne<long>(SelectCached.FullId);
            Assert.Equal(3L, first);
        }

        using (var session = factory.OpenSession()) {
            session.Insert(InsertItem.FullId);
            session.Commit();
        }

        using (var session = factory.OpenReadOnlySession()) {
            var afterInsert = session.SelectOne<long>(SelectCached.FullId);
            Assert.Equal(4L, afterInsert);
        }
    }

    [Fact]
    public async Task InsertAsync_FlushesNamespaceCache() {
        var config  = CreateConfig();
        var factory = new SqlSessionFactory(config, _provider);

        using (var session = factory.OpenReadOnlySession()) {
            await session.SelectOneAsync<long>(SelectCached.FullId);
        }

        await using (var session = factory.OpenSession()) {
            await session.InsertAsync(InsertItem.FullId);
            await session.CommitAsync();
        }

        using (var session = factory.OpenReadOnlySession()) {
            var afterInsert = await session.SelectOneAsync<long>(SelectCached.FullId);
            Assert.True(afterInsert > 3L);
        }
    }

    /** --- 파라미터별 캐시 키 분리 --- */

    [Fact]
    public void DifferentParameters_DifferentCacheKeys() {
        var config  = CreateConfig();
        var factory = new SqlSessionFactory(config, _provider);

        using (var session = factory.OpenReadOnlySession()) {
            var countA = session.SelectOne<long>(SelectWithParam.FullId, new { Name = "a" });
            Assert.Equal(1L, countA);

            var countB = session.SelectOne<long>(SelectWithParam.FullId, new { Name = "b" });
            Assert.Equal(1L, countB);

            var countZ = session.SelectOne<long>(SelectWithParam.FullId, new { Name = "z" });
            Assert.Equal(0L, countZ);
        }
    }

    /** --- MemoryCacheProvider LRU Eviction --- */

    [Fact]
    public void MemoryCacheProvider_LruEviction_RemovesOldestEntry() {
        var smallCache = new MemoryCacheProvider();
        smallCache.RegisterNamespace("test", new CacheConfig { Size = 2 });

        smallCache.Put("test", "key1", "value1");
        smallCache.Put("test", "key2", "value2");

        Assert.Equal("value1", smallCache.Get("test", "key1"));
        Assert.Equal("value2", smallCache.Get("test", "key2"));

        smallCache.Put("test", "key3", "value3");

        Assert.Null(smallCache.Get("test", "key1"));
        Assert.Equal("value2", smallCache.Get("test", "key2"));
        Assert.Equal("value3", smallCache.Get("test", "key3"));

        smallCache.Dispose();
    }

    [Fact]
    public void MemoryCacheProvider_LruEviction_AccessRefreshesEntry() {
        var smallCache = new MemoryCacheProvider();
        smallCache.RegisterNamespace("test", new CacheConfig { Size = 2 });

        smallCache.Put("test", "key1", "value1");
        smallCache.Put("test", "key2", "value2");

        smallCache.Get("test", "key1");

        smallCache.Put("test", "key3", "value3");

        Assert.Equal("value1", smallCache.Get("test", "key1"));
        Assert.Null(smallCache.Get("test", "key2"));
        Assert.Equal("value3", smallCache.Get("test", "key3"));

        smallCache.Dispose();
    }

    /** --- Flush --- */

    [Fact]
    public void MemoryCacheProvider_Flush_ClearsNamespace() {
        _cache.Put(CachedNs, "k1", "v1");
        _cache.Put(CachedNs, "k2", "v2");

        Assert.Equal("v1", _cache.Get(CachedNs, "k1"));

        _cache.Flush(CachedNs);

        Assert.Null(_cache.Get(CachedNs, "k1"));
        Assert.Null(_cache.Get(CachedNs, "k2"));
    }

    /** --- Thread Safety --- */

    [Fact]
    public void MemoryCacheProvider_ConcurrentAccess_ThreadSafe() {
        var threadSafeCache = new MemoryCacheProvider();
        threadSafeCache.RegisterNamespace("concurrent", new CacheConfig { Size = 1000 });

        var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(() => {
            for (int j = 0; j < 100; j++) {
                var key = $"key_{i}_{j}";
                threadSafeCache.Put("concurrent", key, $"value_{i}_{j}");
                threadSafeCache.Get("concurrent", key);
            }
        }));

        Task.WaitAll(tasks.ToArray());

        threadSafeCache.Dispose();
    }

    /** --- CacheKey 유틸리티 --- */

    [Fact]
    public void CacheKey_NullParameter_UsesStatementId() {
        var key = CacheKey.Generate("Stats.GetCount", null);
        Assert.Equal("Stats.GetCount", key);
    }

    [Fact]
    public void CacheKey_SameParameters_SameKey() {
        var key1 = CacheKey.Generate("Stats.Get", new { Name = "test" });
        var key2 = CacheKey.Generate("Stats.Get", new { Name = "test" });
        Assert.Equal(key1, key2);
    }

    [Fact]
    public void CacheKey_DifferentParameters_DifferentKey() {
        var key1 = CacheKey.Generate("Stats.Get", new { Name = "a" });
        var key2 = CacheKey.Generate("Stats.Get", new { Name = "b" });
        Assert.NotEqual(key1, key2);
    }

    public void Dispose() {
        _cache.Dispose();
        _keepAlive.Dispose();
    }
}
