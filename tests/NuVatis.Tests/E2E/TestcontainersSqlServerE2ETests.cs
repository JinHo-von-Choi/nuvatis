using Microsoft.Data.SqlClient;
using NuVatis.Configuration;
using NuVatis.Session;
using NuVatis.SqlServer;
using NuVatis.Statement;
using Testcontainers.MsSql;
using Xunit;

using StatementType = NuVatis.Statement.StatementType;

namespace NuVatis.Tests.E2E;

/**
 * Testcontainers 기반 SQL Server E2E 테스트.
 * Docker가 없으면 자동 스킵된다.
 *
 * @author 최진호
 * @date   2026-03-05
 */
[Trait("Category", "Testcontainers")]
[Collection("Testcontainers")]
public class TestcontainersSqlServerE2ETests : IAsyncLifetime {
    private MsSqlContainer    _container = null!;
    private SqlSessionFactory _factory   = null!;

    private const string Ns = "TCSQL";

    private static readonly MappedStatement InsertUser = new() {
        Id = "Insert", Namespace = Ns, Type = StatementType.Insert,
        SqlSource = "INSERT INTO tc_users (name, age) VALUES (#{Name}, #{Age})"
    };

    private static readonly MappedStatement CountUsers = new() {
        Id = "Count", Namespace = Ns, Type = StatementType.Select,
        SqlSource = "SELECT COUNT(*) FROM tc_users"
    };

    private static readonly MappedStatement DeleteAll = new() {
        Id = "DeleteAll", Namespace = Ns, Type = StatementType.Delete,
        SqlSource = "DELETE FROM tc_users"
    };

    private static readonly MappedStatement UpdateAge = new() {
        Id = "UpdateAge", Namespace = Ns, Type = StatementType.Update,
        SqlSource = "UPDATE tc_users SET age = #{Age} WHERE name = #{Name}"
    };

    public async Task InitializeAsync() {
        if (!await DockerAvailable()) {
            return;
        }

        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();

        await _container.StartAsync();

        var connStr  = _container.GetConnectionString();
        var provider = new SqlServerProvider();
        var config   = new NuVatisConfiguration {
            DataSource = new DataSourceConfig {
                ProviderName     = "SqlServer",
                ConnectionString = connStr
            }
        };

        config.Statements[$"{Ns}.Insert"]    = InsertUser;
        config.Statements[$"{Ns}.Count"]     = CountUsers;
        config.Statements[$"{Ns}.DeleteAll"] = DeleteAll;
        config.Statements[$"{Ns}.UpdateAge"] = UpdateAge;

        _factory = new SqlSessionFactory(config, provider, null);

        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText      = """
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='tc_users' AND xtype='U')
            CREATE TABLE tc_users (
                id   INT IDENTITY(1,1) PRIMARY KEY,
                name NVARCHAR(100) NOT NULL,
                age  INT NOT NULL
            )
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync() {
        if (_container is not null) {
            await _container.DisposeAsync();
        }
    }

    [SkippableFact]
    public void MSSQL_Insert_And_Count() {
        Skip.If(_factory is null, "Docker 미사용 환경: 테스트 스킵");

        using var session = _factory.OpenSession();
        session.Insert($"{Ns}.Insert", new { Name = "Alice", Age = 30 });
        session.Commit();

        using var session2 = _factory.OpenSession();
        var count           = session2.SelectOne<int>($"{Ns}.Count", null);
        session2.Rollback();

        Assert.True(count >= 1);

        using var cleanup = _factory.OpenSession();
        cleanup.Delete($"{Ns}.DeleteAll", null);
        cleanup.Commit();
    }

    [SkippableFact]
    public async Task MSSQL_Async_Insert() {
        Skip.If(_factory is null, "Docker 미사용 환경: 테스트 스킵");

        using var session = _factory.OpenSession();
        await session.InsertAsync($"{Ns}.Insert", new { Name = "Bob", Age = 25 });
        await session.CommitAsync();

        using var cleanup = _factory.OpenSession();
        cleanup.Delete($"{Ns}.DeleteAll", null);
        cleanup.Commit();
    }

    [SkippableFact]
    public void MSSQL_Update_ModifiesRow() {
        Skip.If(_factory is null, "Docker 미사용 환경: 테스트 스킵");

        using var setup = _factory.OpenSession();
        setup.Insert($"{Ns}.Insert", new { Name = "Charlie", Age = 20 });
        setup.Commit();

        using var session = _factory.OpenSession();
        var rows           = session.Update($"{Ns}.UpdateAge", new { Name = "Charlie", Age = 35 });
        session.Commit();

        Assert.Equal(1, rows);

        using var cleanup = _factory.OpenSession();
        cleanup.Delete($"{Ns}.DeleteAll", null);
        cleanup.Commit();
    }

    [SkippableFact]
    public void MSSQL_Transaction_Rollback_DoesNotPersist() {
        Skip.If(_factory is null, "Docker 미사용 환경: 테스트 스킵");

        using var session = _factory.OpenSession();
        session.Insert($"{Ns}.Insert", new { Name = "RollbackUser", Age = 99 });
        session.Rollback();

        using var verify = _factory.OpenSession();
        var count         = verify.SelectOne<int>($"{Ns}.Count", null);
        verify.Rollback();

        Assert.Equal(0, count);
    }

    [SkippableFact]
    public void MSSQL_Delete_ReturnsAffectedRows() {
        Skip.If(_factory is null, "Docker 미사용 환경: 테스트 스킵");

        using var setup = _factory.OpenSession();
        setup.Insert($"{Ns}.Insert", new { Name = "DeleteMe", Age = 1 });
        setup.Commit();

        using var session = _factory.OpenSession();
        var deleted        = session.Delete($"{Ns}.DeleteAll", null);
        session.Commit();

        Assert.True(deleted >= 1);
    }

    private static async Task<bool> DockerAvailable() {
        try {
            var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                FileName               = "docker",
                Arguments              = "info",
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            });
            if (process is null) return false;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        } catch {
            return false;
        }
    }
}
