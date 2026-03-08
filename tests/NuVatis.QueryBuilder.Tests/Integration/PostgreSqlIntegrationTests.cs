namespace NuVatis.QueryBuilder.Tests.Integration;

using Npgsql;
using Testcontainers.PostgreSql;
using NuVatis.QueryBuilder.Ast;
using NuVatis.QueryBuilder.Dsl;
using NuVatis.QueryBuilder.Rendering;

[Collection("PostgreSQL")]
public sealed class PostgreSqlIntegrationTests : IAsyncLifetime {
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    private NpgsqlConnection? _conn;

    public async Task InitializeAsync() {
        await _container.StartAsync();
        _conn = new NpgsqlConnection(_container.GetConnectionString());
        await _conn.OpenAsync();

        await using var cmd = _conn.CreateCommand();
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

        await using var cmd2 = _conn.CreateCommand();
        cmd2.CommandText = """
            INSERT INTO users (name, status, age) VALUES
                ('Alice', 'active',   25),
                ('Bob',   'inactive', 30),
                ('Carol', 'active',   17)
        """;
        await cmd2.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync() {
        if (_conn != null) await _conn.DisposeAsync();
        await _container.DisposeAsync();
    }

    private static readonly TableNode        U        = new("public", "users");
    private static readonly FieldNode<int>   U_ID     = new(U, "id");
    private static readonly FieldNode<string> U_NAME  = new(U, "name");
    private static readonly FieldNode<string> U_STATUS = new(U, "status");
    private static readonly FieldNode<int>   U_AGE    = new(U, "age");

    private sealed class UserDto {
        public int    Id     { get; set; }
        public string Name   { get; set; } = "";
        public string Status { get; set; } = "";
        public int?   Age    { get; set; }
    }

    private DslContext Ctx => new(new PostgreSqlDialect(), _conn!);

    [Fact]
    public void Fetch_ActiveAdultUsers_ReturnsOneRow() {
        var users = Ctx.Select(U_ID, U_NAME, U_STATUS, U_AGE)
                       .From(U)
                       .Where(U_STATUS.Eq("active").And(U_AGE.Gt(18)))
                       .Fetch<UserDto>();

        Assert.Single(users);
        Assert.Equal("Alice", users[0].Name);
    }

    [Fact]
    public void FetchOne_FirstActive_ReturnsAlice() {
        var user = Ctx.Select(U_ID, U_NAME)
                      .From(U)
                      .Where(U_STATUS.Eq("active"))
                      .OrderBy(U_ID.Asc())
                      .FetchOne<UserDto>();

        Assert.NotNull(user);
        Assert.Equal("Alice", user.Name);
    }

    [Fact]
    public void Execute_Insert_AddsRow() {
        Ctx.InsertInto(U)
           .Columns(U_NAME, U_STATUS)
           .Values("Dave", "active")
           .Execute();

        var count = Ctx.Select(U_ID).From(U).Fetch<UserDto>().Count;
        Assert.Equal(4, count);
    }

    [Fact]
    public void Execute_Update_ChangesStatus() {
        Ctx.Update(U)
           .Set(U_STATUS, "inactive")
           .Where(U_NAME.Eq("Alice"))
           .Execute();

        var alice = Ctx.Select(U_ID, U_NAME, U_STATUS)
                       .From(U)
                       .Where(U_NAME.Eq("Alice"))
                       .FetchOne<UserDto>();

        Assert.Equal("inactive", alice?.Status);
    }

    [Fact]
    public void Execute_Delete_RemovesRow() {
        Ctx.DeleteFrom(U)
           .Where(U_NAME.Eq("Bob"))
           .Execute();

        var remaining = Ctx.Select(U_ID).From(U).Fetch<UserDto>();
        Assert.Equal(2, remaining.Count);
    }

    [Fact]
    public void Fetch_WithIn_ReturnsMatchingRows() {
        var users = Ctx.Select(U_ID, U_NAME)
                       .From(U)
                       .Where(U_ID.In([1, 2]))
                       .Fetch<UserDto>();

        Assert.Equal(2, users.Count);
    }

    [Fact]
    public void Fetch_WithOrderByAndLimit_ReturnsLimitedResult() {
        var users = Ctx.Select(U_ID, U_NAME)
                       .From(U)
                       .OrderBy(U_ID.Desc())
                       .Limit(2)
                       .Fetch<UserDto>();

        Assert.Equal(2, users.Count);
        Assert.Equal("Carol", users[0].Name); // DESC이므로 가장 큰 id 먼저
    }
}
