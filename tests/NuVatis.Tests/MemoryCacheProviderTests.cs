using NuVatis.Cache;
using Xunit;

namespace NuVatis.Tests;

/**
 * MemoryCacheProvider LRU 캐시 테스트.
 *
 * @author 최진호
 * @date   2026-02-26
 */
public class MemoryCacheProviderTests : IDisposable {

    private readonly MemoryCacheProvider _cache = new();

    public void Dispose() {
        _cache.Dispose();
    }

    [Fact]
    public void Put_And_Get() {
        _cache.RegisterNamespace("ns1", new CacheConfig { Size = 100 });
        _cache.Put("ns1", "key1", "value1");
        Assert.Equal("value1", _cache.Get("ns1", "key1"));
    }

    [Fact]
    public void Get_Unregistered_Namespace_Returns_Null() {
        Assert.Null(_cache.Get("unknown", "key1"));
    }

    [Fact]
    public void Put_Unregistered_Namespace_NoOp() {
        _cache.Put("unknown", "key1", "val");
        Assert.Null(_cache.Get("unknown", "key1"));
    }

    [Fact]
    public void Flush_Clears_Namespace() {
        _cache.RegisterNamespace("ns1", new CacheConfig { Size = 100 });
        _cache.Put("ns1", "k1", "v1");
        _cache.Put("ns1", "k2", "v2");
        _cache.Flush("ns1");
        Assert.Null(_cache.Get("ns1", "k1"));
        Assert.Null(_cache.Get("ns1", "k2"));
    }

    [Fact]
    public void Flush_Unregistered_Namespace_NoOp() {
        _cache.Flush("unknown");
    }

    [Fact]
    public void LRU_Eviction() {
        _cache.RegisterNamespace("ns1", new CacheConfig { Size = 3 });
        _cache.Put("ns1", "a", 1);
        _cache.Put("ns1", "b", 2);
        _cache.Put("ns1", "c", 3);
        _cache.Put("ns1", "d", 4);
        Assert.Null(_cache.Get("ns1", "a"));
        Assert.Equal(2, _cache.Get("ns1", "b"));
        Assert.Equal(3, _cache.Get("ns1", "c"));
        Assert.Equal(4, _cache.Get("ns1", "d"));
    }

    [Fact]
    public void Get_Promotes_Entry() {
        _cache.RegisterNamespace("ns1", new CacheConfig { Size = 3 });
        _cache.Put("ns1", "a", 1);
        _cache.Put("ns1", "b", 2);
        _cache.Put("ns1", "c", 3);

        _cache.Get("ns1", "a");

        _cache.Put("ns1", "d", 4);
        Assert.Equal(1, _cache.Get("ns1", "a"));
        Assert.Null(_cache.Get("ns1", "b"));
    }

    [Fact]
    public void Put_Updates_Existing() {
        _cache.RegisterNamespace("ns1", new CacheConfig { Size = 100 });
        _cache.Put("ns1", "k1", "v1");
        _cache.Put("ns1", "k1", "v2");
        Assert.Equal("v2", _cache.Get("ns1", "k1"));
    }

    [Fact]
    public void DefaultSize_Applied_When_ZeroOrNegative() {
        _cache.RegisterNamespace("ns1", new CacheConfig { Size = 0 });
        for (int i = 0; i < 1025; i++) {
            _cache.Put("ns1", $"k{i}", i);
        }
        Assert.Null(_cache.Get("ns1", "k0"));
        Assert.Equal(1024, _cache.Get("ns1", "k1024"));
    }

    [Fact]
    public void FlushInterval_Auto_Flush() {
        _cache.RegisterNamespace("ns1", new CacheConfig {
            Size             = 100,
            FlushIntervalMs  = 50
        });
        _cache.Put("ns1", "k1", "v1");
        Assert.Equal("v1", _cache.Get("ns1", "k1"));

        Thread.Sleep(200);
        Assert.Null(_cache.Get("ns1", "k1"));
    }

    [Fact]
    public void CacheConfig_Defaults() {
        var config = new CacheConfig();
        Assert.Equal(CacheEviction.Lru, config.Eviction);
        Assert.Null(config.FlushIntervalMs);
        Assert.Equal(1024, config.Size);
        Assert.True(config.ReadOnly);
    }

    [Fact]
    public void Multiple_Namespaces_Isolated() {
        _cache.RegisterNamespace("ns1", new CacheConfig { Size = 100 });
        _cache.RegisterNamespace("ns2", new CacheConfig { Size = 100 });
        _cache.Put("ns1", "k", "v1");
        _cache.Put("ns2", "k", "v2");
        Assert.Equal("v1", _cache.Get("ns1", "k"));
        Assert.Equal("v2", _cache.Get("ns2", "k"));
        _cache.Flush("ns1");
        Assert.Null(_cache.Get("ns1", "k"));
        Assert.Equal("v2", _cache.Get("ns2", "k"));
    }
}
