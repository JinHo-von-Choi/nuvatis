using NuVatis.Mapping;

namespace NuVatis.Tests;

/**
 * Lazy Loading 관련 테스트.
 * LazyValue, FetchType, Association/Collection FetchType 검증.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public class LazyLoadingTests {

    [Fact]
    public void LazyValue_DoesNotLoadUntilAccess() {
        var loaded = false;
        var lazy   = new LazyValue<string>(() => {
            loaded = true;
            return "loaded";
        });

        Assert.False(loaded);
        Assert.False(lazy.IsLoaded);
    }

    [Fact]
    public void LazyValue_LoadsOnFirstAccess() {
        var callCount = 0;
        var lazy      = new LazyValue<int>(() => {
            callCount++;
            return 42;
        });

        var value = lazy.Value;

        Assert.Equal(42, value);
        Assert.True(lazy.IsLoaded);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void LazyValue_CachesResult() {
        var callCount = 0;
        var lazy      = new LazyValue<string>(() => {
            callCount++;
            return "cached";
        });

        _ = lazy.Value;
        _ = lazy.Value;
        _ = lazy.Value;

        Assert.Equal(1, callCount);
    }

    [Fact]
    public void LazyValue_ImplicitConversion() {
        var lazy    = new LazyValue<int>(() => 100);
        int result = lazy;

        Assert.Equal(100, result);
    }

    [Fact]
    public void LazyValue_ToString_NotLoaded() {
        var lazy = new LazyValue<string>(() => "test");

        Assert.Equal("[Not Loaded]", lazy.ToString());
    }

    [Fact]
    public void LazyValue_ToString_Loaded() {
        var lazy = new LazyValue<string>(() => "hello");
        _ = lazy.Value;

        Assert.Equal("hello", lazy.ToString());
    }

    [Fact]
    public void LazyValue_NullResult() {
        var lazy = new LazyValue<string?>(() => null);

        Assert.Null(lazy.Value);
        Assert.True(lazy.IsLoaded);
    }

    [Fact]
    public void LazyValue_ThreadSafe() {
        var callCount = 0;
        var lazy      = new LazyValue<int>(() => {
            Interlocked.Increment(ref callCount);
            Thread.Sleep(10);
            return 99;
        });

        var results = new int[10];
        Parallel.For(0, 10, i => {
            results[i] = lazy.Value;
        });

        Assert.All(results, r => Assert.Equal(99, r));
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void AssociationMapping_DefaultFetchType_IsEager() {
        var mapping = new AssociationMapping {
            Property = "Department"
        };

        Assert.Equal(FetchType.Eager, mapping.FetchType);
    }

    [Fact]
    public void AssociationMapping_LazyFetchType() {
        var mapping = new AssociationMapping {
            Property  = "Department",
            Select    = "DeptMapper.findById",
            Column    = "dept_id",
            FetchType = FetchType.Lazy
        };

        Assert.Equal(FetchType.Lazy, mapping.FetchType);
        Assert.Equal("DeptMapper.findById", mapping.Select);
    }

    [Fact]
    public void CollectionMapping_DefaultFetchType_IsEager() {
        var mapping = new CollectionMapping {
            Property = "Orders"
        };

        Assert.Equal(FetchType.Eager, mapping.FetchType);
    }

    [Fact]
    public void CollectionMapping_LazyFetchType() {
        var mapping = new CollectionMapping {
            Property  = "Orders",
            Select    = "OrderMapper.findByUserId",
            Column    = "user_id",
            FetchType = FetchType.Lazy
        };

        Assert.Equal(FetchType.Lazy, mapping.FetchType);
        Assert.Equal("OrderMapper.findByUserId", mapping.Select);
    }
}
