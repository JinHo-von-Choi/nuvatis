using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NuVatis.Configuration;
using NuVatis.Extensions.DependencyInjection;
using NuVatis.Provider;
using NuVatis.Session;
using NuVatis.Statement;

namespace NuVatis.Tests;

/**
 * NuVatisHealthCheck (Phase 6.3 C-3) 테스트.
 * SQLite 인메모리 DB로 Health Check 동작을 검증한다.
 *
 * @author 최진호
 * @date   2026-02-25
 */
public class HealthCheckTests : IDisposable {

    private readonly SqliteConnection _keepAlive;

    private sealed class SqliteProvider : IDbProvider {
        private readonly string _connStr;
        public SqliteProvider(string connStr) { _connStr = connStr; }
        public string Name => "Sqlite";
        public DbConnection CreateConnection(string connectionString) => new SqliteConnection(_connStr);
        public string ParameterPrefix => "@";
        public string GetParameterName(int index) => $"@p{index}";
        public string WrapIdentifier(string name) => $"\"{name}\"";
    }

    public HealthCheckTests() {
        _keepAlive = new SqliteConnection("Data Source=HealthCheckTests;Mode=Memory;Cache=Shared");
        _keepAlive.Open();
    }

    private ISqlSessionFactory CreateFactory() {
        var provider = new SqliteProvider("Data Source=HealthCheckTests;Mode=Memory;Cache=Shared");
        var config   = new NuVatisConfiguration {
            DataSource = new DataSourceConfig {
                ProviderName     = "Sqlite",
                ConnectionString = "Data Source=HealthCheckTests;Mode=Memory;Cache=Shared"
            },
            Statements = new Dictionary<string, MappedStatement>()
        };

        var healthStatement = new MappedStatement {
            Id        = "__nuvatis_health",
            Namespace = "",
            Type      = StatementType.Select,
            SqlSource = "SELECT 1"
        };
        config.Statements[healthStatement.FullId] = healthStatement;

        return new SqlSessionFactory(config, provider);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDbHealthy_ReturnsHealthy() {
        var factory     = CreateFactory();
        var healthCheck = new NuVatisHealthCheck(factory);
        var context     = new HealthCheckContext {
            Registration = new HealthCheckRegistration("nuvatis", healthCheck, null, null)
        };

        var result = await healthCheck.CheckHealthAsync(context);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("healthy", result.Description!.ToLowerInvariant());
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDbDown_ReturnsUnhealthy() {
        var provider = new SqliteProvider("Data Source=nonexistent_db_xyzzy;Mode=Memory;Cache=Shared");
        var config   = new NuVatisConfiguration {
            DataSource = new DataSourceConfig {
                ProviderName     = "Sqlite",
                ConnectionString = "Data Source=nonexistent_db_xyzzy;Mode=Memory;Cache=Shared"
            },
            Statements = new Dictionary<string, MappedStatement>()
        };

        var badStatement = new MappedStatement {
            Id        = "__nuvatis_health",
            Namespace = "",
            Type      = StatementType.Select,
            SqlSource = "SELECT * FROM nonexistent_table_xyz"
        };
        config.Statements[badStatement.FullId] = badStatement;

        var factory     = new SqlSessionFactory(config, provider);
        var healthCheck = new NuVatisHealthCheck(factory);
        var context     = new HealthCheckContext {
            Registration = new HealthCheckRegistration("nuvatis", healthCheck, HealthStatus.Unhealthy, null)
        };

        var result = await healthCheck.CheckHealthAsync(context);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    public void Dispose() {
        _keepAlive.Dispose();
    }
}
