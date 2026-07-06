namespace NuVatis.QueryBuilder.Tests.Integration;

using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using NuVatis.QueryBuilder.Ast;
using NuVatis.QueryBuilder.Dsl;
using NuVatis.QueryBuilder.Rendering;

// ---------------------------------------------------------------------------
// 컨테이너를 컬렉션 수명으로 공유 — 클래스당 기동을 제거한다.
// ---------------------------------------------------------------------------
public sealed class SqlServerContainerFixture : IAsyncLifetime {
    public MsSqlContainer Container { get; } = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public Task InitializeAsync() => Container.StartAsync();
    public Task DisposeAsync()    => Container.DisposeAsync().AsTask();
}

[CollectionDefinition("sqlserver-integration")]
public sealed class SqlServerIntegrationCollection
    : ICollectionFixture<SqlServerContainerFixture> { }

// ---------------------------------------------------------------------------
// Base — 공통 스키마/시드, 노드 정의
// ---------------------------------------------------------------------------
public abstract class SqlServerTestBase : IAsyncLifetime {
    private readonly SqlServerContainerFixture _fixture;

    protected SqlConnection? Conn;
    protected DslContext?    Ctx;

    protected static readonly TableNode         U        = new("dbo", "users");
    protected static readonly FieldNode<int>    U_ID     = new(U, "id");
    protected static readonly FieldNode<string> U_NAME   = new(U, "name");
    protected static readonly FieldNode<string> U_STATUS = new(U, "status");
    protected static readonly FieldNode<int>    U_AGE    = new(U, "age");

    protected SqlServerTestBase(SqlServerContainerFixture fixture) {
        _fixture = fixture;
    }

    public virtual async Task InitializeAsync() {
        Conn = new SqlConnection(_fixture.Container.GetConnectionString());
        await Conn.OpenAsync();
        Ctx  = new DslContext(new SqlServerDialect(), Conn);
        await ResetSchemaAsync();
        await SeedDataAsync();
    }

    public virtual async Task DisposeAsync() {
        if (Conn != null) await Conn.DisposeAsync();
    }

    private async Task ResetSchemaAsync() {
        await using var drop = Conn!.CreateCommand();
        drop.CommandText = "DROP TABLE IF EXISTS users";
        await drop.ExecuteNonQueryAsync();

        await using var cmd = Conn!.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE users (
                id     INT           NOT NULL IDENTITY(1,1) PRIMARY KEY,
                name   NVARCHAR(100) NOT NULL,
                status NVARCHAR(20)  NOT NULL DEFAULT 'active',
                age    INT
            )
        """;
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task SeedDataAsync() {
        await using var cmd = Conn!.CreateCommand();
        cmd.CommandText = """
            INSERT INTO users (name, status, age) VALUES
                ('Alice', 'active',   25),
                ('Bob',   'inactive', 30),
                ('Carol', 'active',   17)
        """;
        await cmd.ExecuteNonQueryAsync();
    }

    protected sealed class UserDto {
        public int    Id     { get; set; }
        public string Name   { get; set; } = "";
        public string Status { get; set; } = "";
        public int?   Age    { get; set; }
    }
}

// ---------------------------------------------------------------------------
// Read — 조회 전용 테스트 (공유 시드 데이터 불변)
// ---------------------------------------------------------------------------
[Trait("Category", "Integration")]
[Collection("sqlserver-integration")]
public sealed class SqlServerReadTests : SqlServerTestBase {
    public SqlServerReadTests(SqlServerContainerFixture fixture) : base(fixture) { }

    [Fact]
    public void Fetch_ActiveAdultUsers_ReturnsOneRow() {
        var users = Ctx!.Select(U_ID, U_NAME, U_STATUS, U_AGE)
                        .From(U)
                        .Where(U_STATUS.Eq("active").And(U_AGE.Gt(18)))
                        .Fetch<UserDto>();

        Assert.Single(users);
        Assert.Equal("Alice", users[0].Name);
    }

    [Fact]
    public void Fetch_WithOrderByAndLimit_UsesOffsetFetch() {
        // SqlServer는 ORDER BY 없이 OFFSET/FETCH 불가 — 반드시 OrderBy 포함
        var users = Ctx!.Select(U_ID, U_NAME)
                        .From(U)
                        .OrderBy(U_ID.Desc())
                        .Limit(2)
                        .Fetch<UserDto>();

        Assert.Equal(2, users.Count);
    }
}

// ---------------------------------------------------------------------------
// Insert — 독립 컨테이너 (상태 오염 없음)
// ---------------------------------------------------------------------------
[Trait("Category", "Integration")]
[Collection("sqlserver-integration")]
public sealed class SqlServerInsertTests : SqlServerTestBase {
    public SqlServerInsertTests(SqlServerContainerFixture fixture) : base(fixture) { }

    [Fact]
    public void Execute_Insert_AddsRow() {
        Ctx!.InsertInto(U)
            .Columns(U_NAME, U_STATUS)
            .Values("Dave", "active")
            .Execute();

        var count = Ctx.Select(U_ID).From(U).Fetch<UserDto>().Count;
        Assert.Equal(4, count);
    }
}

// ---------------------------------------------------------------------------
// Delete — 독립 컨테이너 (상태 오염 없음)
// ---------------------------------------------------------------------------
[Trait("Category", "Integration")]
[Collection("sqlserver-integration")]
public sealed class SqlServerDeleteTests : SqlServerTestBase {
    public SqlServerDeleteTests(SqlServerContainerFixture fixture) : base(fixture) { }

    [Fact]
    public void Execute_Delete_RemovesRow() {
        Ctx!.DeleteFrom(U)
            .Where(U_NAME.Eq("Bob"))
            .Execute();

        var remaining = Ctx.Select(U_ID).From(U).Fetch<UserDto>();
        Assert.Equal(2, remaining.Count);
    }
}
