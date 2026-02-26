using Npgsql;
using NuVatis.Configuration;
using NuVatis.PostgreSql;
using NuVatis.Session;
using NuVatis.Statement;
using Testcontainers.PostgreSql;
using Xunit;

using StatementType = NuVatis.Statement.StatementType;

namespace NuVatis.Tests.E2E;

/**
 * Testcontainers 기반 PostgreSQL 다중 버전 E2E 테스트.
 * Docker가 없으면 자동 스킵된다.
 *
 * @author 최진호
 * @date   2026-02-26
 */
[Trait("Category", "Testcontainers")]
[Collection("Testcontainers")]
public class TestcontainersPostgreSqlE2ETests : IAsyncLifetime {
    private PostgreSqlContainer _container = null!;
    private SqlSessionFactory   _factory   = null!;

    private static readonly MappedStatement CreateTable = new() {
        Id = "CreateTable", Namespace = "TC", Type = StatementType.Select,
        SqlSource = """
            CREATE TABLE IF NOT EXISTS tc_users (
                id   SERIAL PRIMARY KEY,
                name TEXT NOT NULL,
                age  INTEGER NOT NULL
            )
            """
    };

    private static readonly MappedStatement InsertUser = new() {
        Id = "Insert", Namespace = "TC", Type = StatementType.Insert,
        SqlSource = "INSERT INTO tc_users (name, age) VALUES (#{Name}, #{Age})"
    };

    private static readonly MappedStatement CountUsers = new() {
        Id = "Count", Namespace = "TC", Type = StatementType.Select,
        SqlSource = "SELECT COUNT(*) FROM tc_users"
    };

    private static readonly MappedStatement DeleteAll = new() {
        Id = "DeleteAll", Namespace = "TC", Type = StatementType.Delete,
        SqlSource = "DELETE FROM tc_users"
    };

    public async Task InitializeAsync() {
        if (!await DockerAvailable()) {
            return;
        }

        _container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("nuvatis_test")
            .WithUsername("test")
            .WithPassword("test")
            .Build();

        await _container.StartAsync();

        var connStr  = _container.GetConnectionString();
        var provider = new PostgreSqlProvider();
        var config   = new NuVatisConfiguration {
            DataSource = new DataSourceConfig {
                ProviderName     = "PostgreSql",
                ConnectionString = connStr
            }
        };

        config.Statements["TC.CreateTable"] = CreateTable;
        config.Statements["TC.Insert"]      = InsertUser;
        config.Statements["TC.Count"]       = CountUsers;
        config.Statements["TC.DeleteAll"]   = DeleteAll;

        _factory = new SqlSessionFactory(config, provider, null);

        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText      = CreateTable.SqlSource;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync() {
        if (_container is not null) {
            await _container.DisposeAsync();
        }
    }

    [SkippableFact]
    public void PG_CRUD_Roundtrip() {
        Skip.If(_factory is null, "Docker 미사용 환경: 테스트 스킵");

        using var session = _factory.OpenSession();
        session.Insert("TC.Insert", new { Name = "TC_User", Age = 30 });
        session.Commit();

        using var session2 = _factory.OpenSession();
        var deleted         = session2.Delete("TC.DeleteAll", null);
        session2.Commit();

        Assert.True(deleted >= 1);
    }

    [SkippableFact]
    public async Task PG_Async_Insert() {
        Skip.If(_factory is null, "Docker 미사용 환경: 테스트 스킵");

        using var session = _factory.OpenSession();
        await session.InsertAsync("TC.Insert", new { Name = "Async_User", Age = 25 });
        await session.CommitAsync();

        using var session2 = _factory.OpenSession();
        session2.Delete("TC.DeleteAll", null);
        session2.Commit();
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
