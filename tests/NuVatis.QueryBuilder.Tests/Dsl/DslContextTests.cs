namespace NuVatis.QueryBuilder.Tests.Dsl;

using NuVatis.QueryBuilder.Dsl;
using NuVatis.QueryBuilder.Rendering;

public class DslContextTests {
    private static readonly TableNode         U        = new("public", "users");
    private static readonly FieldNode<int>    U_ID     = new(U, "id");
    private static readonly FieldNode<string> U_NAME   = new(U, "name");
    private static readonly FieldNode<string> U_STATUS = new(U, "status");
    private static readonly TableNode         O        = new("public", "orders");
    private static readonly FieldNode<int>    O_UID    = new(O, "user_id");

    private readonly DslContext _ctx = new(new PostgreSqlDialect());

    [Fact]
    public void Select_BuildsCorrectSql() {
        var (sql, pars) = _ctx.Select(U_ID, U_NAME).From(U).ToSql();

        Assert.Equal("SELECT \"id\", \"name\" FROM \"public\".\"users\"", sql);
        Assert.Empty(pars);
    }

    [Fact]
    public void Select_WithWhere_BuildsCorrectSql() {
        var (sql, pars) = _ctx.Select(U_ID)
                               .From(U)
                               .Where(U_STATUS.Eq("active"))
                               .ToSql();

        Assert.Equal("SELECT \"id\" FROM \"public\".\"users\" WHERE \"status\" = $1", sql);
        Assert.Equal(new object?[] { "active" }, pars);
    }

    [Fact]
    public void Select_WithJoin_BuildsCorrectSql() {
        var (sql, _) = _ctx.Select(U_ID)
                            .From(U)
                            .InnerJoin(O).On(U_ID.Eq(O_UID))
                            .ToSql();

        Assert.Contains("INNER JOIN", sql);
    }

    [Fact]
    public void Delete_BuildsCorrectSql() {
        var (sql, pars) = _ctx.DeleteFrom(U).Where(U_ID.Eq(1)).ToSql();

        Assert.Equal("DELETE FROM \"public\".\"users\" WHERE \"id\" = $1", sql);
        Assert.Equal(new object?[] { 1 }, pars);
    }

    [Fact]
    public void Update_BuildsCorrectSql() {
        var (sql, pars) = _ctx.Update(U).Set(U_STATUS, "inactive").Where(U_ID.Eq(1)).ToSql();

        Assert.Equal("UPDATE \"public\".\"users\" SET \"status\" = $1 WHERE \"id\" = $2", sql);
    }

    [Fact]
    public void Insert_BuildsCorrectSql() {
        var (sql, pars) = _ctx.InsertInto(U).Columns(U_ID, U_NAME).Values(1, "Alice").ToSql();

        Assert.Equal("INSERT INTO \"public\".\"users\" (\"id\", \"name\") VALUES ($1, $2)", sql);
    }

    [Fact]
    public void Select_WithoutConnection_ToSql_Works() {
        // DbConnection 없이 ToSql()은 항상 동작해야 함
        var ctx = new DslContext(new PostgreSqlDialect());
        var (sql, _) = ctx.Select(U_ID).From(U).ToSql();
        Assert.NotEmpty(sql);
    }

    [Fact]
    public void Select_WithoutConnection_Fetch_Throws() {
        // DbConnection 없이 Fetch() 호출 시 예외
        var ctx = new DslContext(new PostgreSqlDialect());
        Assert.Throws<InvalidOperationException>(() =>
            ctx.Select(U_ID).From(U).Fetch<object>());
    }
}
