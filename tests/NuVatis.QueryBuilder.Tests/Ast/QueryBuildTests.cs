namespace NuVatis.QueryBuilder.Tests.Ast;

public class QueryBuildTests {
    private static readonly TableNode             U        = new("public", "users");
    private static readonly FieldNode<int>         U_ID     = new(U, "id");
    private static readonly FieldNode<string>      U_NAME   = new(U, "name");
    private static readonly FieldNode<string>      U_STATUS = new(U, "status");
    private static readonly TableNode             O        = new("public", "orders");
    private static readonly FieldNode<int>         O_UID    = new(O, "user_id");

    [Fact]
    public void SelectQuery_WithWhereAndOrder_HasCorrectNodes() {
        var q = new SelectQuery()
                    .Select(U_ID, U_NAME)
                    .From(U)
                    .Where(U_STATUS.Eq("active"))
                    .OrderBy(U_ID.Desc())
                    .Limit(10)
                    .Offset(20);

        Assert.Equal(2,  q.Fields.Count);
        Assert.Equal(U,  q.FromTable);
        Assert.NotNull(q.WhereCondition);
        Assert.Equal(1,  q.OrderByFields.Count);
        Assert.Equal(10, q.LimitValue);
        Assert.Equal(20, q.OffsetValue);
    }

    [Fact]
    public void SelectQuery_WithInnerJoin_HasJoinClause() {
        var q = new SelectQuery()
                    .Select(U_ID)
                    .From(U)
                    .InnerJoin(O).On(U_ID.Eq(O_UID));

        Assert.Single(q.Joins);
        Assert.Equal(JoinType.Inner, q.Joins[0].Type);
        Assert.NotNull(q.Joins[0].On);
    }

    [Fact]
    public void SelectQuery_WithLeftJoin_HasLeftJoinClause() {
        var q = new SelectQuery()
                    .Select(U_ID)
                    .From(U)
                    .LeftJoin(O).On(U_ID.Eq(O_UID));

        Assert.Equal(JoinType.Left, q.Joins[0].Type);
    }

    [Fact]
    public void SelectQuery_NoFields_HasEmptyFields() {
        var q = new SelectQuery().From(U);
        Assert.Empty(q.Fields);
    }

    [Fact]
    public void InsertQuery_HasTableAndColumnsAndValues() {
        var q = new InsertQuery(U)
                    .Into(U_ID, U_NAME)
                    .WithValues(1, "Alice");

        Assert.Equal(U, q.Table);
        Assert.Equal(2, q.Columns.Count);
        Assert.Equal(2, q.Values.Count);
    }

    [Fact]
    public void UpdateQuery_HasSetsAndWhere() {
        var q = new UpdateQuery(U)
                    .Set(U_STATUS, "inactive")
                    .Where(U_ID.Eq(1));

        Assert.Equal(U, q.Table);
        Assert.Single(q.Sets);
        Assert.NotNull(q.WhereCondition);
    }

    [Fact]
    public void DeleteQuery_HasWhere() {
        var q = new DeleteQuery(U).Where(U_ID.Eq(42));

        Assert.Equal(U, q.Table);
        Assert.NotNull(q.WhereCondition);
    }
}
