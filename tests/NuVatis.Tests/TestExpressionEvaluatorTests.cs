using NuVatis.DynamicSql;
using Xunit;

namespace NuVatis.Tests;

/**
 * TestExpressionEvaluator 단위 테스트.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public class TestExpressionEvaluatorTests {

    private class TestParam {
        public string? Name { get; set; }
        public int     Age  { get; set; }
        public string? Type { get; set; }
        public List<int>? Ids { get; set; }
    }

    [Fact]
    public void NullCheck_True() {
        var param = new TestParam { Name = "hello" };
        Assert.True(TestExpressionEvaluator.Evaluate("Name != null", param));
    }

    [Fact]
    public void NullCheck_False() {
        var param = new TestParam { Name = null };
        Assert.False(TestExpressionEvaluator.Evaluate("Name != null", param));
    }

    [Fact]
    public void StringEquality() {
        var param = new TestParam { Type = "admin" };
        Assert.True(TestExpressionEvaluator.Evaluate("Type == 'admin'", param));
    }

    [Fact]
    public void NumericComparison_GreaterThan() {
        var param = new TestParam { Age = 25 };
        Assert.True(TestExpressionEvaluator.Evaluate("Age > 0", param));
    }

    [Fact]
    public void NumericComparison_NotGreater() {
        var param = new TestParam { Age = 0 };
        Assert.False(TestExpressionEvaluator.Evaluate("Age > 0", param));
    }

    [Fact]
    public void NumericEquality() {
        var param = new TestParam { Age = 5 };
        Assert.True(TestExpressionEvaluator.Evaluate("Age == 5", param));
    }

    [Fact]
    public void AndExpression_AllTrue() {
        var param = new TestParam { Name = "x", Age = 5 };
        Assert.True(TestExpressionEvaluator.Evaluate("Name != null and Age > 0", param));
    }

    [Fact]
    public void AndExpression_OneFalse() {
        var param = new TestParam { Name = null, Age = 5 };
        Assert.False(TestExpressionEvaluator.Evaluate("Name != null and Age > 0", param));
    }

    [Fact]
    public void ListSizeCheck() {
        var param = new TestParam { Ids = new List<int> { 1, 2, 3 } };
        Assert.True(TestExpressionEvaluator.Evaluate("Ids != null and Ids.size > 0", param));
    }

    [Fact]
    public void EmptyStringCheck() {
        var param = new TestParam { Name = "" };
        Assert.False(TestExpressionEvaluator.Evaluate("Name != null and Name != ''", param));
    }

    [Fact]
    public void NullParameter_ReturnsFalse() {
        Assert.False(TestExpressionEvaluator.Evaluate("Name != null", null));
    }

    [Fact]
    public void TruthyCheck_NonNullProperty() {
        var param = new TestParam { Name = "hello" };
        Assert.True(TestExpressionEvaluator.Evaluate("Name", param));
    }

    [Fact]
    public void TruthyCheck_NullProperty() {
        var param = new TestParam { Name = null };
        Assert.False(TestExpressionEvaluator.Evaluate("Name", param));
    }

    [Fact]
    public void OrExpression() {
        var param = new TestParam { Name = null, Age = 10 };
        Assert.True(TestExpressionEvaluator.Evaluate("Name != null or Age > 0", param));
    }
}
