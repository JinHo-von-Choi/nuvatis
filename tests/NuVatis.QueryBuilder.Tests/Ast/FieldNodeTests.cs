namespace NuVatis.QueryBuilder.Tests.Ast;

public class FieldNodeTests {
    private static readonly TableNode T = new("public", "users");

    [Fact]
    public void FieldNode_Eq_CreatesBinaryCondition() {
        var field = new FieldNode<int>(T, "id");
        var cond  = field.Eq(42);

        var binary = Assert.IsType<BinaryCondition>(cond);
        Assert.Equal("=", binary.Operator);
        Assert.Equal(42,  binary.Value);
    }

    [Fact]
    public void FieldNode_IsNull_CreatesIsNullCondition() {
        var field = new FieldNode<string?>(T, "email");
        var cond  = field.IsNull();

        Assert.IsType<IsNullCondition>(cond);
    }

    [Fact]
    public void FieldNode_In_CreatesInCondition() {
        var field = new FieldNode<int>(T, "id");
        var cond  = field.In([1, 2, 3]);

        var inCond = Assert.IsType<InCondition>(cond);
        Assert.Equal(3, inCond.Values.Count);
    }

    [Fact]
    public void ConditionNode_And_CreatesAndCondition() {
        var f1   = new FieldNode<string>(T, "status");
        var f2   = new FieldNode<int>(T, "age");
        var cond = f1.Eq("active").And(f2.Gt(18));

        Assert.IsType<AndCondition>(cond);
    }

    [Fact]
    public void ConditionNode_Or_CreatesOrCondition() {
        var f1   = new FieldNode<string>(T, "status");
        var cond = f1.Eq("active").Or(f1.Eq("pending"));

        Assert.IsType<OrCondition>(cond);
    }

    [Fact]
    public void ConditionNode_Not_CreatesNotCondition() {
        var field = new FieldNode<bool>(T, "is_active");
        var cond  = field.Eq(true).Not();

        Assert.IsType<NotCondition>(cond);
    }

    [Fact]
    public void FieldNode_Ne_CreatesBinaryConditionWithNotEqual() {
        var field = new FieldNode<string>(T, "status");
        var cond  = field.Ne("deleted");

        var binary = Assert.IsType<BinaryCondition>(cond);
        Assert.Equal("<>", binary.Operator);
    }

    [Fact]
    public void FieldNode_Like_CreatesBinaryConditionWithLike() {
        var field = new FieldNode<string>(T, "name");
        var cond  = field.Like("%alice%");

        var binary = Assert.IsType<BinaryCondition>(cond);
        Assert.Equal("LIKE", binary.Operator);
        Assert.Equal("%alice%", binary.Value);
    }
}
