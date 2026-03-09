namespace NuVatis.QueryBuilder.Tests.Rendering;

using NuVatis.QueryBuilder.Rendering;

public class OracleDialectTests {
    private static readonly TableNode        SCHEMA   = new("hr", "employees");
    private static readonly FieldNode<int>    E_ID     = new(SCHEMA, "employee_id");
    private static readonly FieldNode<string> E_STATUS = new(SCHEMA, "status");
    private static readonly FieldNode<string> E_NAME   = new(SCHEMA, "first_name");

    private readonly ISqlDialect _dialect = new OracleDialect();

    [Fact]
    public void QuoteIdentifier_UsesDoubleQuotes() {
        Assert.Equal("\"employees\"", _dialect.QuoteIdentifier("employees"));
    }

    [Fact]
    public void Placeholder_UsesColonPrefix() {
        Assert.Equal(":p0", _dialect.Placeholder(0));
        Assert.Equal(":p2", _dialect.Placeholder(2));
    }

    [Fact]
    public void ParameterName_ReturnsWithoutColon() {
        // ODP.NET: OracleParameter.ParameterName은 콜론 없이 설정해야 한다
        Assert.Equal("p0", _dialect.ParameterName(0));
        Assert.Equal("p2", _dialect.ParameterName(2));
    }

    [Fact]
    public void Render_SimpleSelect_UsesDoubleQuoteQuoting() {
        var q = new SelectQuery().Select(E_ID).From(SCHEMA);
        var r = _dialect.Render(q);

        Assert.Equal("SELECT \"employee_id\" FROM \"hr\".\"employees\"", r.Sql);
    }

    [Fact]
    public void Render_SelectWithWhere_UsesColonPlaceholders() {
        var q = new SelectQuery().Select(E_ID).From(SCHEMA).Where(E_STATUS.Eq("active"));
        var r = _dialect.Render(q);

        Assert.Equal(
            "SELECT \"employee_id\" FROM \"hr\".\"employees\" WHERE \"status\" = :p0",
            r.Sql);
        Assert.Equal(new object?[] { "active" }, r.Parameters);
    }

    [Fact]
    public void Render_SelectWithLimitAndOrderBy_UsesOffsetFetch() {
        var q = new SelectQuery()
                    .Select(E_ID)
                    .From(SCHEMA)
                    .OrderBy(E_ID.Asc())
                    .Limit(10)
                    .Offset(20);
        var r = _dialect.Render(q);

        Assert.Contains("ORDER BY \"employee_id\" ASC", r.Sql);
        Assert.Contains("OFFSET 20 ROWS FETCH NEXT 10 ROWS ONLY", r.Sql);
        Assert.DoesNotContain("LIMIT", r.Sql);
    }

    [Fact]
    public void Render_SelectWithLimitNoOrderBy_UsesOffsetFetch() {
        // Oracle은 ORDER BY 없는 페이지네이션도 문법상 허용 (결과 순서 비결정)
        var q = new SelectQuery().Select(E_ID).From(SCHEMA).Limit(5).Offset(0);
        var r = _dialect.Render(q);

        Assert.Contains("OFFSET 0 ROWS FETCH NEXT 5 ROWS ONLY", r.Sql);
    }

    [Fact]
    public void Render_Insert_UsesColonPlaceholders() {
        var q = new InsertQuery(SCHEMA).Into(E_ID, E_NAME).WithValues(1, "Alice");
        var r = _dialect.Render(q);

        Assert.Equal(
            "INSERT INTO \"hr\".\"employees\" (\"employee_id\", \"first_name\") VALUES (:p0, :p1)",
            r.Sql);
    }

    [Fact]
    public void Render_Update_UsesColonPlaceholders() {
        var q = new UpdateQuery(SCHEMA).Set(E_STATUS, "inactive").Where(E_ID.Eq(1));
        var r = _dialect.Render(q);

        Assert.Equal(
            "UPDATE \"hr\".\"employees\" SET \"status\" = :p0 WHERE \"employee_id\" = :p1",
            r.Sql);
    }

    [Fact]
    public void Render_Delete_UsesDoubleQuotes() {
        var q = new DeleteQuery(SCHEMA).Where(E_ID.Eq(42));
        var r = _dialect.Render(q);

        Assert.Equal(
            "DELETE FROM \"hr\".\"employees\" WHERE \"employee_id\" = :p0",
            r.Sql);
        Assert.Equal(new object?[] { 42 }, r.Parameters);
    }
}
