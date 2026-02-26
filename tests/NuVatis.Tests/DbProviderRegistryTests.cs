using System.Data.Common;
using Microsoft.Data.Sqlite;
using NuVatis.Provider;
using Xunit;

namespace NuVatis.Tests;

/**
 * DbProviderRegistry 테스트.
 *
 * @author 최진호
 * @date   2026-02-26
 */
public class DbProviderRegistryTests {

    private sealed class FakeProvider : IDbProvider {
        public string Name { get; }
        public FakeProvider(string name) { Name = name; }
        public DbConnection CreateConnection(string connectionString) => new SqliteConnection(connectionString);
        public string ParameterPrefix              => "@";
        public string GetParameterName(int index)  => $"@p{index}";
        public string WrapIdentifier(string name)  => $"\"{name}\"";
    }

    [Fact]
    public void Register_And_Get() {
        var registry = new DbProviderRegistry();
        var provider = new FakeProvider("TestDb");
        registry.Register(provider);

        var resolved = registry.Get("TestDb");
        Assert.Same(provider, resolved);
    }

    [Fact]
    public void Get_CaseInsensitive() {
        var registry = new DbProviderRegistry();
        registry.Register(new FakeProvider("TestDb"));

        var resolved = registry.Get("testdb");
        Assert.NotNull(resolved);
    }

    [Fact]
    public void Register_Duplicate_Throws() {
        var registry = new DbProviderRegistry();
        registry.Register(new FakeProvider("Dup"));

        Assert.Throws<InvalidOperationException>(() =>
            registry.Register(new FakeProvider("Dup")));
    }

    [Fact]
    public void Get_NotFound_Throws() {
        var registry = new DbProviderRegistry();
        Assert.Throws<InvalidOperationException>(() => registry.Get("Unknown"));
    }
}
