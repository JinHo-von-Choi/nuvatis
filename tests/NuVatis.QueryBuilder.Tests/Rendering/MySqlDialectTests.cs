namespace NuVatis.QueryBuilder.Tests.Rendering;

using NuVatis.QueryBuilder.Rendering;

public class MySqlDialectTests {
    private static readonly TableNode        U        = new("mydb", "users");
    private static readonly FieldNode<int>    U_ID     = new(U, "id");
    private static readonly FieldNode<string> U_STATUS = new(U, "status");

    private readonly ISqlDialect _dialect = new MySqlDialect();

    [Fact]
    public void QuoteIdentifier_UsesBacktick() {
        Assert.Equal("`users`", _dialect.QuoteIdentifier("users"));
    }

    [Fact]
    public void Placeholder_UsesAtPrefix() {
        Assert.Equal("@p0", _dialect.Placeholder(0));
        Assert.Equal("@p2", _dialect.Placeholder(2));
    }

    [Fact]
    public void Render_SimpleSelect_UsesBacktickQuoting() {
        var q = new SelectQuery().Select(U_ID).From(U);
        var r = _dialect.Render(q);

        Assert.Equal("SELECT `id` FROM `mydb`.`users`", r.Sql);
    }

    [Fact]
    public void Render_SelectWithWhere_UsesAtPlaceholders() {
        var q = new SelectQuery().Select(U_ID).From(U).Where(U_STATUS.Eq("active"));
        var r = _dialect.Render(q);

        Assert.Equal("SELECT `id` FROM `mydb`.`users` WHERE `status` = @p0", r.Sql);
        Assert.Equal(new object?[] { "active" }, r.Parameters);
    }
}
