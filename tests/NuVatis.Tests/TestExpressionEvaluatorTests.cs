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

    [Fact]
    public void WhitespaceExpression_ReturnsTrue() {
        Assert.True(TestExpressionEvaluator.Evaluate("  ", new TestParam()));
    }

    [Fact]
    public void NullExpression_ReturnsTrue() {
        Assert.True(TestExpressionEvaluator.Evaluate(null, new TestParam()));
    }

    [Fact]
    public void LessThan_Comparison() {
        var param = new TestParam { Age = 5 };
        Assert.True(TestExpressionEvaluator.Evaluate("Age < 10", param));
        Assert.False(TestExpressionEvaluator.Evaluate("Age < 3", param));
    }

    [Fact]
    public void GreaterThanOrEqual_Comparison() {
        var param = new TestParam { Age = 10 };
        Assert.True(TestExpressionEvaluator.Evaluate("Age >= 10", param));
        Assert.True(TestExpressionEvaluator.Evaluate("Age >= 5", param));
        Assert.False(TestExpressionEvaluator.Evaluate("Age >= 15", param));
    }

    [Fact]
    public void LessThanOrEqual_Comparison() {
        var param = new TestParam { Age = 10 };
        Assert.True(TestExpressionEvaluator.Evaluate("Age <= 10", param));
        Assert.True(TestExpressionEvaluator.Evaluate("Age <= 15", param));
        Assert.False(TestExpressionEvaluator.Evaluate("Age <= 5", param));
    }

    [Fact]
    public void DoubleQuotedString() {
        var param = new TestParam { Type = "admin" };
        Assert.True(TestExpressionEvaluator.Evaluate("Type == \"admin\"", param));
    }

    [Fact]
    public void EmptyStringLiteral_SingleQuote() {
        var param = new TestParam { Name = "" };
        Assert.True(TestExpressionEvaluator.Evaluate("Name == ''", param));
    }

    [Fact]
    public void EmptyStringLiteral_DoubleQuote() {
        var param = new TestParam { Name = "" };
        Assert.True(TestExpressionEvaluator.Evaluate("Name == \"\"", param));
    }

    [Fact]
    public void BooleanParse() {
        var param = new { Active = true };
        Assert.True(TestExpressionEvaluator.Evaluate("Active == true", param));
    }

    [Fact]
    public void DecimalComparison() {
        var param = new { Price = 19.99m };
        Assert.True(TestExpressionEvaluator.Evaluate("Price > 10.0", param));
        Assert.False(TestExpressionEvaluator.Evaluate("Price < 10.0", param));
    }

    [Fact]
    public void NullEqualsNull() {
        var param = new TestParam { Name = null };
        Assert.True(TestExpressionEvaluator.Evaluate("Name == null", param));
    }

    [Fact]
    public void NotNull_Comparison() {
        var param = new TestParam { Name = "x" };
        Assert.False(TestExpressionEvaluator.Evaluate("Name == null", param));
    }

    [Fact]
    public void NonExistentProperty_Truthiness() {
        var param = new TestParam { Name = "x" };
        Assert.False(TestExpressionEvaluator.Evaluate("NonExistent", param));
    }

    [Fact]
    public void Collection_Truthiness_NonEmpty() {
        var param = new TestParam { Ids = new List<int> { 1 } };
        Assert.True(TestExpressionEvaluator.Evaluate("Ids", param));
    }

    [Fact]
    public void Collection_Truthiness_Empty() {
        var param = new TestParam { Ids = new List<int>() };
        Assert.False(TestExpressionEvaluator.Evaluate("Ids", param));
    }

    [Fact]
    public void GetPropertyValue_DottedPath() {
        var param = new { Address = new { City = "Seoul" } };
        var val   = TestExpressionEvaluator.GetPropertyValue(param, "Address.City");
        Assert.Equal("Seoul", val);
    }

    [Fact]
    public void GetPropertyValue_NullObj() {
        Assert.Null(TestExpressionEvaluator.GetPropertyValue(null, "Name"));
    }

    [Fact]
    public void GetPropertyValue_NullIntermediate() {
        var param = new { Address = (object?)null };
        Assert.Null(TestExpressionEvaluator.GetPropertyValue(param, "Address.City"));
    }

    [Fact]
    public void GetPropertyValue_Length_Alias() {
        var param = new { Text = "hello" };
        var val   = TestExpressionEvaluator.GetPropertyValue(param, "Text.length");
        Assert.Equal(5, val);
    }

    [Fact]
    public void OR_UpperCase() {
        var param = new TestParam { Name = null, Age = 5 };
        Assert.True(TestExpressionEvaluator.Evaluate("Name != null OR Age > 0", param));
    }

    [Fact]
    public void AND_UpperCase() {
        var param = new TestParam { Name = "x", Age = 5 };
        Assert.True(TestExpressionEvaluator.Evaluate("Name != null AND Age > 0", param));
    }

    [Fact]
    public void Inequality_NotEqual() {
        var param = new TestParam { Age = 5 };
        Assert.True(TestExpressionEvaluator.Evaluate("Age != 10", param));
        Assert.False(TestExpressionEvaluator.Evaluate("Age != 5", param));
    }

    [Fact]
    public void StringComparison_GreaterThan() {
        var param = new TestParam { Name = "hello" };
        Assert.True(TestExpressionEvaluator.Evaluate("Name > 'abc'", param));
        Assert.False(TestExpressionEvaluator.Evaluate("Name > 'zzz'", param));
    }
}
