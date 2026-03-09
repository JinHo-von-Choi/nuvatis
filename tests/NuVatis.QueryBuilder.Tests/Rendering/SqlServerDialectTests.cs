namespace NuVatis.QueryBuilder.Tests.Rendering;

using NuVatis.QueryBuilder.Rendering;

public class SqlServerDialectTests {
    private static readonly TableNode        DB       = new("dbo", "users");
    private static readonly FieldNode<int>    U_ID     = new(DB, "id");
    private static readonly FieldNode<string> U_STATUS = new(DB, "status");
    private static readonly FieldNode<string> U_NAME   = new(DB, "name");

    private readonly ISqlDialect _dialect = new SqlServerDialect();

    [Fact]
    public void QuoteIdentifier_UsesBrackets() {
        Assert.Equal("[users]", _dialect.QuoteIdentifier("users"));
    }

    [Fact]
    public void Placeholder_UsesAtPrefix() {
        Assert.Equal("@p0", _dialect.Placeholder(0));
        Assert.Equal("@p2", _dialect.Placeholder(2));
    }

    [Fact]
    public void ParameterName_MatchesPlaceholder() {
        Assert.Equal("@p0", _dialect.ParameterName(0));
    }

    [Fact]
    public void Render_SimpleSelect_UsesBracketQuoting() {
        var q = new SelectQuery().Select(U_ID).From(DB);
        var r = _dialect.Render(q);

        Assert.Equal("SELECT [id] FROM [dbo].[users]", r.Sql);
    }

    [Fact]
    public void Render_SelectWithWhere_UsesAtPlaceholders() {
        var q = new SelectQuery().Select(U_ID).From(DB).Where(U_STATUS.Eq("active"));
        var r = _dialect.Render(q);

        Assert.Equal("SELECT [id] FROM [dbo].[users] WHERE [status] = @p0", r.Sql);
        Assert.Equal(new object?[] { "active" }, r.Parameters);
    }

    [Fact]
    public void Render_SelectWithLimitAndOrderBy_UsesOffsetFetch() {
        var q = new SelectQuery()
                    .Select(U_ID)
                    .From(DB)
                    .OrderBy(U_ID.Asc())
                    .Limit(10)
                    .Offset(20);
        var r = _dialect.Render(q);

        Assert.Contains("ORDER BY [id] ASC", r.Sql);
        Assert.Contains("OFFSET 20 ROWS FETCH NEXT 10 ROWS ONLY", r.Sql);
        Assert.DoesNotContain("LIMIT", r.Sql);
    }

    [Fact]
    public void Render_SelectWithLimitButNoOrderBy_Throws() {
        var q = new SelectQuery().Select(U_ID).From(DB).Limit(10);

        var ex = Assert.Throws<InvalidOperationException>(() => _dialect.Render(q));
        Assert.Contains("ORDER BY", ex.Message);
    }

    [Fact]
    public void Render_SelectWithLimitDefaultOffset_UsesZero() {
        var q = new SelectQuery()
                    .Select(U_ID)
                    .From(DB)
                    .OrderBy(U_NAME.Asc())
                    .Limit(5);
        var r = _dialect.Render(q);

        Assert.Contains("OFFSET 0 ROWS FETCH NEXT 5 ROWS ONLY", r.Sql);
    }

    [Fact]
    public void Render_Insert_UsesBrackets() {
        var q = new InsertQuery(DB).Into(U_ID, U_NAME).WithValues(1, "Alice");
        var r = _dialect.Render(q);

        Assert.Equal("INSERT INTO [dbo].[users] ([id], [name]) VALUES (@p0, @p1)", r.Sql);
    }

    [Fact]
    public void Render_Update_UsesBrackets() {
        var q = new UpdateQuery(DB).Set(U_STATUS, "inactive").Where(U_ID.Eq(1));
        var r = _dialect.Render(q);

        Assert.Equal("UPDATE [dbo].[users] SET [status] = @p0 WHERE [id] = @p1", r.Sql);
    }

    [Fact]
    public void Render_Delete_UsesBrackets() {
        var q = new DeleteQuery(DB).Where(U_ID.Eq(42));
        var r = _dialect.Render(q);

        Assert.Equal("DELETE FROM [dbo].[users] WHERE [id] = @p0", r.Sql);
        Assert.Equal(new object?[] { 42 }, r.Parameters);
    }
}
