namespace NuVatis.QueryBuilder.Tests.Integration;

using Npgsql;
using Testcontainers.PostgreSql;
using NuVatis.QueryBuilder.Ast;
using NuVatis.QueryBuilder.Dsl;
using NuVatis.QueryBuilder.Rendering;

// ---------------------------------------------------------------------------
// Base — 공통 컨테이너 수명주기, 스키마/시드, 노드 정의
// ---------------------------------------------------------------------------
public abstract class PostgreSqlTestBase : IAsyncLifetime {
    protected readonly PostgreSqlContainer Container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    protected NpgsqlConnection? Conn;
    protected DslContext?       Ctx;

    protected static readonly TableNode             U        = new("public", "users");
    protected static readonly FieldNode<int>        U_ID     = new(U, "id");
    protected static readonly FieldNode<string>     U_NAME   = new(U, "name");
    protected static readonly FieldNode<string>     U_STATUS = new(U, "status");
    protected static readonly FieldNode<int>        U_AGE    = new(U, "age");

    public virtual async Task InitializeAsync() {
        await Container.StartAsync();
        Conn = new NpgsqlConnection(Container.GetConnectionString());
        await Conn.OpenAsync();
        Ctx  = new DslContext(new PostgreSqlDialect(), Conn);
        await CreateSchemaAsync();
        await SeedDataAsync();
    }

    public virtual async Task DisposeAsync() {
        if (Conn != null) await Conn.DisposeAsync();
        await Container.DisposeAsync();
    }

    private async Task CreateSchemaAsync() {
        await using var cmd = Conn!.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE users (
                id         SERIAL       PRIMARY KEY,
                name       VARCHAR(100) NOT NULL,
                status     VARCHAR(20)  NOT NULL DEFAULT 'active',
                age        INT,
                created_at TIMESTAMPTZ  NOT NULL DEFAULT NOW()
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
public sealed class PostgreSqlReadTests : PostgreSqlTestBase {
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
    public void FetchOne_FirstActive_ReturnsAlice() {
        var user = Ctx!.Select(U_ID, U_NAME)
                       .From(U)
                       .Where(U_STATUS.Eq("active"))
                       .OrderBy(U_ID.Asc())
                       .FetchOne<UserDto>();

        Assert.NotNull(user);
        Assert.Equal("Alice", user.Name);
    }

    [Fact]
    public void Fetch_WithIn_ReturnsMatchingRows() {
        var users = Ctx!.Select(U_ID, U_NAME)
                        .From(U)
                        .Where(U_ID.In([1, 2]))
                        .Fetch<UserDto>();

        Assert.Equal(2, users.Count);
    }

    [Fact]
    public void Fetch_WithOrderByAndLimit_ReturnsLimitedResult() {
        var users = Ctx!.Select(U_ID, U_NAME)
                        .From(U)
                        .OrderBy(U_ID.Desc())
                        .Limit(2)
                        .Fetch<UserDto>();

        Assert.Equal(2, users.Count);
        Assert.Equal("Carol", users[0].Name); // DESC이므로 가장 큰 id 먼저
    }
}

// ---------------------------------------------------------------------------
// Insert — 독립 컨테이너 (상태 오염 없음)
// ---------------------------------------------------------------------------
[Trait("Category", "Integration")]
public sealed class PostgreSqlInsertTests : PostgreSqlTestBase {
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
// Update — 독립 컨테이너 (상태 오염 없음)
// ---------------------------------------------------------------------------
[Trait("Category", "Integration")]
public sealed class PostgreSqlUpdateTests : PostgreSqlTestBase {
    [Fact]
    public void Execute_Update_ChangesStatus() {
        Ctx!.Update(U)
            .Set(U_STATUS, "inactive")
            .Where(U_NAME.Eq("Alice"))
            .Execute();

        var alice = Ctx.Select(U_ID, U_NAME, U_STATUS)
                       .From(U)
                       .Where(U_NAME.Eq("Alice"))
                       .FetchOne<UserDto>();

        Assert.Equal("inactive", alice?.Status);
    }
}

// ---------------------------------------------------------------------------
// Delete — 독립 컨테이너 (상태 오염 없음)
// ---------------------------------------------------------------------------
[Trait("Category", "Integration")]
public sealed class PostgreSqlDeleteTests : PostgreSqlTestBase {
    [Fact]
    public void Execute_Delete_RemovesRow() {
        Ctx!.DeleteFrom(U)
            .Where(U_NAME.Eq("Bob"))
            .Execute();

        var remaining = Ctx.Select(U_ID).From(U).Fetch<UserDto>();
        Assert.Equal(2, remaining.Count);
    }
}
