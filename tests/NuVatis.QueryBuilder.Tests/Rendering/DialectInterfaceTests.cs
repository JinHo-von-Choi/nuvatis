namespace NuVatis.QueryBuilder.Tests.Rendering;

using NuVatis.QueryBuilder.Rendering;

public class DialectInterfaceTests {
    [Fact]
    public void RenderedSql_HasSqlAndParameters() {
        var rendered = new RenderedSql("SELECT 1", new object?[] { "a", 42 });

        Assert.Equal("SELECT 1", rendered.Sql);
        Assert.Equal(2, rendered.Parameters.Count);
    }
}
