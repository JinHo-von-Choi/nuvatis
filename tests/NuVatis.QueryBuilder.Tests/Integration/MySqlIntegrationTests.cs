namespace NuVatis.QueryBuilder.Tests.Integration;

using MySqlConnector;
using Testcontainers.MySql;
using NuVatis.QueryBuilder.Ast;
using NuVatis.QueryBuilder.Dsl;
using NuVatis.QueryBuilder.Rendering;

// ---------------------------------------------------------------------------
// Base — 공통 컨테이너 수명주기, 스키마/시드, 노드 정의
// ---------------------------------------------------------------------------
public abstract class MySqlTestBase : IAsyncLifetime {
    protected readonly MySqlContainer Container = new MySqlBuilder()
        .WithImage("mysql:8.0")
        .Build();

    protected MySqlConnection? Conn;
    protected DslContext?      Ctx;

    protected static readonly TableNode         U        = new("test", "users");
    protected static readonly FieldNode<int>    U_ID     = new(U, "id");
    protected static readonly FieldNode<string> U_NAME   = new(U, "name");
    protected static readonly FieldNode<string> U_STATUS = new(U, "status");
    protected static readonly FieldNode<int>    U_AGE    = new(U, "age");

    public virtual async Task InitializeAsync() {
        await Container.StartAsync();
        Conn = new MySqlConnection(Container.GetConnectionString());
        await Conn.OpenAsync();
        Ctx  = new DslContext(new MySqlDialect(), Conn);
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
                id     INT          NOT NULL AUTO_INCREMENT PRIMARY KEY,
                name   VARCHAR(100) NOT NULL,
                status VARCHAR(20)  NOT NULL DEFAULT 'active',
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
// Read — 조회 전용 테스트
// ---------------------------------------------------------------------------
[Trait("Category", "Integration")]
public sealed class MySqlReadTests : MySqlTestBase {
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
    public void Fetch_WithOrderByAndLimit_ReturnsLimitedResult() {
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
public sealed class MySqlInsertTests : MySqlTestBase {
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
public sealed class MySqlUpdateTests : MySqlTestBase {
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
public sealed class MySqlDeleteTests : MySqlTestBase {
    [Fact]
    public void Execute_Delete_RemovesRow() {
        Ctx!.DeleteFrom(U).Where(U_NAME.Eq("Bob")).Execute();
        var remaining = Ctx.Select(U_ID).From(U).Fetch<UserDto>();
        Assert.Equal(2, remaining.Count);
    }
}
