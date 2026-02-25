using System.Data.Common;
using NuVatis.Configuration;
using NuVatis.PostgreSql;
using NuVatis.Session;
using Npgsql;
using Xunit;

using NuVatis.Statement;
using StatementType = NuVatis.Statement.StatementType;

namespace NuVatis.Tests.E2E;

/**
 * PostgreSQL 실제 연동 E2E 테스트.
 * 로컬 PostgreSQL 인스턴스(nuvatis_test DB)에 대해 전체 CRUD 파이프라인을 검증한다.
 *
 * 환경 변수: PGHOST, PGPORT, PGUSER, PGDATABASE 또는 기본값 사용.
 * CI 환경에서 PG 미사용 시 자동 스킵.
 *
 * @author 최진호
 * @date   2026-02-24
 */
[Trait("Category", "E2E")]
public class PostgreSqlE2ETests : IAsyncLifetime {

    private const string TestConnStr =
        "Host=localhost;Port=35432;Username=bee;Database=nuvatis_test;";

    private SqlSessionFactory? _factory;
    private bool _available;

    public class PgUser {
        public int Id       { get; set; }
        public string Name  { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int Age      { get; set; }
    }

    public async Task InitializeAsync() {
        try {
            await using var conn = new NpgsqlConnection(TestConnStr);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                DROP TABLE IF EXISTS nuvatis_test_users;
                CREATE TABLE nuvatis_test_users (
                    id    SERIAL PRIMARY KEY,
                    name  TEXT NOT NULL,
                    email TEXT NOT NULL,
                    age   INTEGER NOT NULL
                );
                INSERT INTO nuvatis_test_users (name, email, age) VALUES ('Alice', 'alice@pg.com', 30);
                INSERT INTO nuvatis_test_users (name, email, age) VALUES ('Bob', 'bob@pg.com', 25);
                INSERT INTO nuvatis_test_users (name, email, age) VALUES ('Charlie', 'charlie@pg.com', 35);
            """;
            await cmd.ExecuteNonQueryAsync();

            var provider = new PostgreSqlProvider();
            var config   = new NuVatisConfiguration {
                DataSource = new DataSourceConfig {
                    ProviderName     = "PostgreSql",
                    ConnectionString = TestConnStr
                },
                Statements = {
                    ["PgUser.GetByName"] = new MappedStatement {
                        Id = "GetByName", Namespace = "PgUser",
                        Type      = StatementType.Select,
                        SqlSource = "SELECT id, name, email, age FROM nuvatis_test_users WHERE name = #{Name}"
                    },
                    ["PgUser.GetAll"] = new MappedStatement {
                        Id = "GetAll", Namespace = "PgUser",
                        Type      = StatementType.Select,
                        SqlSource = "SELECT id, name, email, age FROM nuvatis_test_users ORDER BY id"
                    },
                    ["PgUser.Count"] = new MappedStatement {
                        Id = "Count", Namespace = "PgUser",
                        Type      = StatementType.Select,
                        SqlSource = "SELECT COUNT(*)::bigint FROM nuvatis_test_users"
                    },
                    ["PgUser.Insert"] = new MappedStatement {
                        Id = "Insert", Namespace = "PgUser",
                        Type      = StatementType.Insert,
                        SqlSource = "INSERT INTO nuvatis_test_users (name, email, age) VALUES (#{Name}, #{Email}, #{Age})"
                    },
                    ["PgUser.Update"] = new MappedStatement {
                        Id = "Update", Namespace = "PgUser",
                        Type      = StatementType.Update,
                        SqlSource = "UPDATE nuvatis_test_users SET age = #{Age} WHERE name = #{Name}"
                    },
                    ["PgUser.Delete"] = new MappedStatement {
                        Id = "Delete", Namespace = "PgUser",
                        Type      = StatementType.Delete,
                        SqlSource = "DELETE FROM nuvatis_test_users WHERE name = #{Name}"
                    }
                }
            };

            _factory   = new SqlSessionFactory(config, provider);
            _available = true;
        } catch {
            _available = false;
        }
    }

    public async Task DisposeAsync() {
        if (!_available) return;
        try {
            await using var conn = new NpgsqlConnection(TestConnStr);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DROP TABLE IF EXISTS nuvatis_test_users;";
            await cmd.ExecuteNonQueryAsync();
        } catch { }
    }

    private void SkipIfUnavailable() {
        Skip.If(!_available, "PostgreSQL 미접속: 테스트 스킵");
    }

    [SkippableFact]
    public void PG_SelectOne_Scalar() {
        SkipIfUnavailable();
        using var session = _factory!.OpenSession(autoCommit: true);
        var count = session.SelectOne<long>("PgUser.Count");
        Assert.Equal(3L, count);
    }

    [SkippableFact]
    public void PG_SelectOne_ComplexType() {
        SkipIfUnavailable();
        using var session = _factory!.OpenSession(autoCommit: true);
        var user = session.SelectOne<PgUser>("PgUser.GetByName", new { Name = "Alice" });

        Assert.NotNull(user);
        Assert.Equal("Alice", user!.Name);
        Assert.Equal("alice@pg.com", user.Email);
        Assert.Equal(30, user.Age);
    }

    [SkippableFact]
    public void PG_SelectList() {
        SkipIfUnavailable();
        using var session = _factory!.OpenSession(autoCommit: true);
        var users = session.SelectList<PgUser>("PgUser.GetAll");

        Assert.Equal(3, users.Count);
        Assert.Equal("Alice", users[0].Name);
        Assert.Equal("Bob", users[1].Name);
        Assert.Equal("Charlie", users[2].Name);
    }

    [SkippableFact]
    public void PG_Insert_Commit_Verify() {
        SkipIfUnavailable();
        using var session = _factory!.OpenSession(autoCommit: false);
        var affected = session.Insert("PgUser.Insert", new { Name = "David", Email = "david@pg.com", Age = 28 });
        Assert.Equal(1, affected);
        session.Commit();

        using var verify = _factory!.OpenSession(autoCommit: true);
        var count = verify.SelectOne<long>("PgUser.Count");
        Assert.Equal(4L, count);

        var david = verify.SelectOne<PgUser>("PgUser.GetByName", new { Name = "David" });
        Assert.NotNull(david);
        Assert.Equal("david@pg.com", david!.Email);
    }

    [SkippableFact]
    public void PG_Update_Commit_Verify() {
        SkipIfUnavailable();
        using var session = _factory!.OpenSession(autoCommit: false);
        var affected = session.Update("PgUser.Update", new { Name = "Bob", Age = 99 });
        Assert.Equal(1, affected);
        session.Commit();

        using var verify = _factory!.OpenSession(autoCommit: true);
        var bob = verify.SelectOne<PgUser>("PgUser.GetByName", new { Name = "Bob" });
        Assert.Equal(99, bob!.Age);
    }

    [SkippableFact]
    public void PG_Rollback_NoChange() {
        SkipIfUnavailable();
        using var session = _factory!.OpenSession(autoCommit: false);
        session.Insert("PgUser.Insert", new { Name = "Temp", Email = "t@t.com", Age = 1 });
        session.Rollback();

        using var verify = _factory!.OpenSession(autoCommit: true);
        var count = verify.SelectOne<long>("PgUser.Count");
        Assert.Equal(3L, count);
    }

    [SkippableFact]
    public async Task PG_FullCrudAsync() {
        SkipIfUnavailable();

        await using var s1 = _factory!.OpenSession(autoCommit: false);
        await s1.InsertAsync("PgUser.Insert", new { Name = "Eve", Email = "eve@pg.com", Age = 22 });
        await s1.CommitAsync();

        await using var s2 = _factory!.OpenSession(autoCommit: true);
        var eve = await s2.SelectOneAsync<PgUser>("PgUser.GetByName", new { Name = "Eve" });
        Assert.NotNull(eve);
        Assert.Equal(22, eve!.Age);

        await using var s3 = _factory!.OpenSession(autoCommit: false);
        await s3.UpdateAsync("PgUser.Update", new { Name = "Eve", Age = 23 });
        await s3.CommitAsync();

        await using var s4 = _factory!.OpenSession(autoCommit: true);
        var eveUpdated = await s4.SelectOneAsync<PgUser>("PgUser.GetByName", new { Name = "Eve" });
        Assert.Equal(23, eveUpdated!.Age);

        await using var s5 = _factory!.OpenSession(autoCommit: false);
        await s5.DeleteAsync("PgUser.Delete", new { Name = "Eve" });
        await s5.CommitAsync();

        await using var s6 = _factory!.OpenSession(autoCommit: true);
        var count = await s6.SelectOneAsync<long>("PgUser.Count");
        Assert.Equal(3L, count);
    }
}
