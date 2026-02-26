using MySqlConnector;
using NuVatis.Configuration;
using NuVatis.MySql;
using NuVatis.Session;
using NuVatis.Statement;
using Testcontainers.MySql;
using Xunit;

namespace NuVatis.Tests.E2E;

/**
 * Testcontainers 기반 MySQL 다중 버전 E2E 테스트.
 * Docker가 없으면 자동 스킵된다.
 *
 * @author 최진호
 * @date   2026-02-26
 */
[Trait("Category", "Testcontainers")]
[Collection("Testcontainers")]
public class TestcontainersMySqlE2ETests : IAsyncLifetime {
    private MySqlContainer    _container = null!;
    private SqlSessionFactory _factory   = null!;

    private static readonly MappedStatement InsertUser = new() {
        Id = "Insert", Namespace = "TCM", Type = StatementType.Insert,
        SqlSource = "INSERT INTO tc_items (name, value) VALUES (#{Name}, #{Value})"
    };

    private static readonly MappedStatement DeleteAll = new() {
        Id = "DeleteAll", Namespace = "TCM", Type = StatementType.Delete,
        SqlSource = "DELETE FROM tc_items"
    };

    public async Task InitializeAsync() {
        if (!await DockerAvailable()) {
            return;
        }

        _container = new MySqlBuilder("mysql:8.0")
            .WithDatabase("nuvatis_test")
            .WithUsername("test")
            .WithPassword("test1234")
            .Build();

        await _container.StartAsync();

        var connStr  = _container.GetConnectionString();
        var provider = new MySqlProvider();
        var config   = new NuVatisConfiguration {
            DataSource = new DataSourceConfig {
                ProviderName     = "MySql",
                ConnectionString = connStr
            }
        };

        config.Statements["TCM.Insert"]    = InsertUser;
        config.Statements["TCM.DeleteAll"] = DeleteAll;

        _factory = new SqlSessionFactory(config, provider, null);

        await using var conn = new MySqlConnection(connStr);
        await conn.OpenAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText      = """
            CREATE TABLE IF NOT EXISTS tc_items (
                id    INT AUTO_INCREMENT PRIMARY KEY,
                name  VARCHAR(100) NOT NULL,
                value INT NOT NULL
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
    public void MySQL_CRUD_Roundtrip() {
        Skip.If(_factory is null, "Docker 미사용 환경: 테스트 스킵");

        using var session = _factory.OpenSession();
        session.Insert("TCM.Insert", new { Name = "mysql_item", Value = 42 });
        session.Commit();

        using var session2 = _factory.OpenSession();
        var deleted         = session2.Delete("TCM.DeleteAll", null);
        session2.Commit();

        Assert.True(deleted >= 1);
    }

    [SkippableFact]
    public async Task MySQL_Async_Insert() {
        Skip.If(_factory is null, "Docker 미사용 환경: 테스트 스킵");

        using var session = _factory.OpenSession();
        await session.InsertAsync("TCM.Insert", new { Name = "async_item", Value = 99 });
        await session.CommitAsync();

        using var session2 = _factory.OpenSession();
        session2.Delete("TCM.DeleteAll", null);
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
