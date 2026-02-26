using System.Data.Common;
using Microsoft.Data.Sqlite;
using NuVatis.Provider;
using NuVatis.Session;
using Xunit;

namespace NuVatis.Tests;

/**
 * SqlSessionFactoryBuilder 테스트.
 *
 * @author 최진호
 * @date   2026-02-26
 */
public class SqlSessionFactoryBuilderTests {

    private sealed class TestProvider : IDbProvider {
        public string Name => "Test";
        public DbConnection CreateConnection(string connectionString) => new SqliteConnection(connectionString);
        public string ParameterPrefix              => "@";
        public string GetParameterName(int index)  => $"@p{index}";
        public string WrapIdentifier(string name)  => $"\"{name}\"";
    }

    [Fact]
    public void Build_Without_Provider_Throws() {
        var builder = new SqlSessionFactoryBuilder();
        builder.ConnectionString("Data Source=:memory:");

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Build_Without_ConnectionString_Throws() {
        var builder = new SqlSessionFactoryBuilder();
        builder.UseProvider(new TestProvider());

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Build_Success() {
        var factory = new SqlSessionFactoryBuilder()
            .UseProvider(new TestProvider())
            .ConnectionString("Data Source=:memory:")
            .Build();

        Assert.NotNull(factory);
        Assert.NotNull(factory.Configuration);
    }

    [Fact]
    public void Build_WithXmlConfig_Success() {
        var factory = new SqlSessionFactoryBuilder()
            .UseProvider(new TestProvider())
            .ConnectionString("Data Source=:memory:")
            .AddXmlConfiguration("dummy.xml")
            .Build();

        Assert.NotNull(factory);
    }

    [Fact]
    public void Build_Overload_WithPath() {
        var factory = new SqlSessionFactoryBuilder()
            .UseProvider(new TestProvider())
            .ConnectionString("Data Source=:memory:")
            .Build("dummy.xml");

        Assert.NotNull(factory);
    }

    [Fact]
    public void UseLoggerFactory_Fluent() {
        var builder = new SqlSessionFactoryBuilder()
            .UseProvider(new TestProvider())
            .ConnectionString("Data Source=:memory:")
            .UseLoggerFactory(null!);

        Assert.NotNull(builder);
    }
}
