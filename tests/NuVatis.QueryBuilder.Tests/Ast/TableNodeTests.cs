namespace NuVatis.QueryBuilder.Tests.Ast;

public class TableNodeTests {
    [Fact]
    public void TableNode_WithSchema_HasCorrectProperties() {
        var table = new TableNode("public", "users");

        Assert.Equal("public", table.Schema);
        Assert.Equal("users",  table.Name);
        Assert.Null(table.Alias);
    }

    [Fact]
    public void TableNode_WithAlias_HasAlias() {
        var table = new TableNode("public", "users").As("u");

        Assert.Equal("u", table.Alias);
    }

    [Fact]
    public void SortField_Desc_HasDescOrder() {
        var table = new TableNode("public", "users");
        var field = new FieldNode<int>(table, "id");
        var sort  = field.Desc();

        Assert.Equal(SortOrder.Desc, sort.Order);
    }
}
