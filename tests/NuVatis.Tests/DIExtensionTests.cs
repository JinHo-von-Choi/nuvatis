using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using NuVatis.Extensions.DependencyInjection;
using NuVatis.Interceptor;
using NuVatis.Provider;
using NuVatis.Session;
using NuVatis.Statement;
using Xunit;

namespace NuVatis.Tests;

/**
 * DI 확장 메서드 통합 테스트.
 *
 * @author 최진호
 * @date   2026-02-26
 */
public class DIExtensionTests {

    private sealed class FakeProvider : IDbProvider {
        public string Name                              => "Fake";
        public DbConnection CreateConnection(string cs) => new SqliteConnection(cs);
        public string ParameterPrefix                   => "@";
        public string GetParameterName(int index)       => $"@p{index}";
        public string WrapIdentifier(string name)       => $"\"{name}\"";
    }

    [Fact]
    public void AddNuVatis_Registers_Factory_And_Session() {
        var services = new ServiceCollection();
        services.AddNuVatis(options => {
            options.ConnectionString = "Data Source=:memory:";
            options.Provider         = new FakeProvider();
        });

        var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetService<ISqlSessionFactory>());

        using var scope  = sp.CreateScope();
        var session = scope.ServiceProvider.GetService<ISqlSession>();
        Assert.NotNull(session);
    }

    [Fact]
    public void AddNuVatis_Without_ConnectionString_Throws() {
        var services = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(() =>
            services.AddNuVatis(options => {
                options.Provider = new FakeProvider();
            }));
    }

    [Fact]
    public void AddNuVatis_Without_Provider_Throws() {
        var services = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(() =>
            services.AddNuVatis(options => {
                options.ConnectionString = "Data Source=:memory:";
            }));
    }

    [Fact]
    public void AddNuVatis_WithAutoCommit() {
        var services = new ServiceCollection();
        services.AddNuVatis(options => {
            options.ConnectionString  = "Data Source=:memory:";
            options.Provider          = new FakeProvider();
            options.DefaultAutoCommit = true;
        });

        var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetService<ISqlSessionFactory>());
    }

    [Fact]
    public void AddNuVatis_WithInterceptor() {
        var services = new ServiceCollection();
        services.AddNuVatis(options => {
            options.ConnectionString = "Data Source=:memory:";
            options.Provider         = new FakeProvider();
            options.AddInterceptor(new NuVatis.Extensions.OpenTelemetry.OpenTelemetryInterceptor());
        });

        var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetService<ISqlSessionFactory>());
    }

    [Fact]
    public void AddInterceptor_Null_Throws() {
        var options = new NuVatisOptions();
        Assert.Throws<ArgumentNullException>(() => options.AddInterceptor(null!));
    }

    [Fact]
    public void RegisterMappers_Sets_Action() {
        var options = new NuVatisOptions();
        Assert.Null(options.RegistryAction);
        options.RegisterMappers((factory, register) => { });
        Assert.NotNull(options.RegistryAction);
    }

    [Fact]
    public void RegisterAttributeStatements_Sets_Action() {
        var options = new NuVatisOptions();
        Assert.Null(options.StatementRegistryAction);
        options.RegisterAttributeStatements(stmts => { });
        Assert.NotNull(options.StatementRegistryAction);
    }

    [Fact]
    public void AddNuVatis_WithStatementRegistry() {
        var services = new ServiceCollection();
        services.AddNuVatis(options => {
            options.ConnectionString = "Data Source=:memory:";
            options.Provider         = new FakeProvider();
            options.RegisterAttributeStatements(stmts => {
                stmts["test.selectAll"] = new MappedStatement {
                    Id        = "selectAll",
                    Namespace = "test",
                    Type      = StatementType.Select,
                    SqlSource = "SELECT 1"
                };
            });
        });

        var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetService<ISqlSessionFactory>());
    }

    [Fact]
    public void AddNuVatis_WithMapperRegistry() {
        var services = new ServiceCollection();
        services.AddNuVatis(options => {
            options.ConnectionString = "Data Source=:memory:";
            options.Provider         = new FakeProvider();
            options.RegisterMappers((factory, register) => {
                register(typeof(string), session => "test-mapper");
            });
        });

        var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetService<ISqlSessionFactory>());
    }

    [Fact]
    public void HealthCheck_Extension_Registers() {
        var services = new ServiceCollection();
        services.AddNuVatis(options => {
            options.ConnectionString = "Data Source=:memory:";
            options.Provider         = new FakeProvider();
        });
        services.AddHealthChecks().AddNuVatis("test-hc");

        var sp = services.BuildServiceProvider();
        Assert.NotNull(sp);
    }
}
