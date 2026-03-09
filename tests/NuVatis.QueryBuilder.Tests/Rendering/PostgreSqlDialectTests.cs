namespace NuVatis.QueryBuilder.Tests.Rendering;

using NuVatis.QueryBuilder.Rendering;

public class PostgreSqlDialectTests {
    private static readonly TableNode             U        = new("public", "users");
    private static readonly FieldNode<int>         U_ID     = new(U, "id");
    private static readonly FieldNode<string>      U_NAME   = new(U, "name");
    private static readonly FieldNode<string>      U_STATUS = new(U, "status");
    private static readonly FieldNode<int>         U_AGE    = new(U, "age");
    private static readonly TableNode             O        = new("public", "orders");
    private static readonly FieldNode<int>         O_UID    = new(O, "user_id");

    private readonly ISqlDialect _dialect = new PostgreSqlDialect();

    [Fact]
    public void Render_SimpleSelect_CorrectSql() {
        var q = new SelectQuery().Select(U_ID, U_NAME).From(U);
        var r = _dialect.Render(q);

        Assert.Equal("SELECT \"id\", \"name\" FROM \"public\".\"users\"", r.Sql);
        Assert.Empty(r.Parameters);
    }

    [Fact]
    public void Render_SelectWithWhere_BindsParameters() {
        var q = new SelectQuery()
                    .Select(U_ID)
                    .From(U)
                    .Where(U_STATUS.Eq("active").And(U_AGE.Gt(18)));
        var r = _dialect.Render(q);

        Assert.Equal(
            "SELECT \"id\" FROM \"public\".\"users\" WHERE \"status\" = $1 AND \"age\" > $2",
            r.Sql);
        Assert.Equal(new object?[] { "active", 18 }, r.Parameters);
    }

    [Fact]
    public void Render_SelectWithLimit_AppendsLimitOffset() {
        var q = new SelectQuery().Select(U_ID).From(U).Limit(10).Offset(20);
        var r = _dialect.Render(q);

        Assert.Contains("LIMIT 10 OFFSET 20", r.Sql);
    }

    [Fact]
    public void Render_SelectWithOrderBy_RendersCorrectly() {
        var q = new SelectQuery().Select(U_ID).From(U).OrderBy(U_NAME.Asc(), U_ID.Desc());
        var r = _dialect.Render(q);

        Assert.Contains("ORDER BY \"name\" ASC, \"id\" DESC", r.Sql);
    }

    [Fact]
    public void Render_SelectWithInnerJoin_RendersJoin() {
        var q = new SelectQuery()
                    .Select(U_ID)
                    .From(U)
                    .InnerJoin(O).On(U_ID.Eq(O_UID));
        var r = _dialect.Render(q);

        Assert.Contains("INNER JOIN \"public\".\"orders\" ON \"id\" = \"user_id\"", r.Sql);
    }

    [Fact]
    public void Render_SelectWithIn_RendersInClause() {
        var q = new SelectQuery().Select(U_ID).From(U).Where(U_ID.In([1, 2, 3]));
        var r = _dialect.Render(q);

        Assert.Contains("IN ($1, $2, $3)", r.Sql);
        Assert.Equal(new object?[] { 1, 2, 3 }, r.Parameters);
    }

    [Fact]
    public void Render_SelectWithIsNull_RendersIsNull() {
        var q = new SelectQuery().Select(U_ID).From(U).Where(U_NAME.IsNull());
        var r = _dialect.Render(q);

        Assert.Contains("IS NULL", r.Sql);
    }

    [Fact]
    public void Render_SelectWithIsNotNull_RendersIsNotNull() {
        var q = new SelectQuery().Select(U_ID).From(U).Where(U_NAME.IsNotNull());
        var r = _dialect.Render(q);

        Assert.Contains("IS NOT NULL", r.Sql);
    }

    [Fact]
    public void Render_Delete_RendersCorrectly() {
        var q = new DeleteQuery(U).Where(U_ID.Eq(42));
        var r = _dialect.Render(q);

        Assert.Equal("DELETE FROM \"public\".\"users\" WHERE \"id\" = $1", r.Sql);
        Assert.Equal(new object?[] { 42 }, r.Parameters);
    }

    [Fact]
    public void Render_Update_RendersCorrectly() {
        var q = new UpdateQuery(U).Set(U_STATUS, "inactive").Where(U_ID.Eq(1));
        var r = _dialect.Render(q);

        Assert.Equal(
            "UPDATE \"public\".\"users\" SET \"status\" = $1 WHERE \"id\" = $2",
            r.Sql);
    }

    [Fact]
    public void Render_Insert_RendersCorrectly() {
        var q = new InsertQuery(U).Into(U_ID, U_NAME).WithValues(1, "Alice");
        var r = _dialect.Render(q);

        Assert.Equal(
            "INSERT INTO \"public\".\"users\" (\"id\", \"name\") VALUES ($1, $2)",
            r.Sql);
        Assert.Equal(new object?[] { 1, "Alice" }, r.Parameters);
    }

    [Fact]
    public void QuoteIdentifier_WrapsWithDoubleQuotes() {
        Assert.Equal("\"users\"", _dialect.QuoteIdentifier("users"));
    }

    [Fact]
    public void Placeholder_ReturnsPositional() {
        Assert.Equal("$1", _dialect.Placeholder(0));
        Assert.Equal("$3", _dialect.Placeholder(2));
    }
}
