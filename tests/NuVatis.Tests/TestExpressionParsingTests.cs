using NuVatis.DynamicSql;
using Xunit;

namespace NuVatis.Tests;

/**
 * TestExpressionEvaluator의 파싱 경계 사례.
 * 종전 구현은 문자열 분할(Split) 기반이라 리터럴 안의 논리 연산자,
 * 혼합 대소문자, 괄호를 처리하지 못했다.
 */
public class TestExpressionParsingTests {

    private sealed class P {
        public string     Name    { get; set; } = string.Empty;
        public int        Age     { get; set; }
        public string?    Nick    { get; set; }
        public string[]   Tags    { get; set; } = Array.Empty<string>();
        public Nested?    Nested  { get; set; }
    }

    private sealed class Nested {
        public string City { get; set; } = string.Empty;
    }

    // ── 문자열 리터럴 안의 논리 연산자 ──────────────────────────────────

    [Fact]
    public void Literal_ContainingOr_IsNotSplit() {
        var p = new P { Name = "a or b", Age = 1 };
        Assert.True(TestExpressionEvaluator.Evaluate("Name == 'a or b'", p));
    }

    [Fact]
    public void Literal_ContainingAnd_IsNotSplit() {
        var p = new P { Name = "rock and roll", Age = 1 };
        Assert.True(TestExpressionEvaluator.Evaluate("Name == 'rock and roll'", p));
    }

    [Fact]
    public void Literal_ContainingOr_FalseCase_Works() {
        var p = new P { Name = "x", Age = 1 };
        Assert.False(TestExpressionEvaluator.Evaluate("Name == 'a or b'", p));
    }

    [Fact]
    public void Literal_ContainingComparisonOperator_IsNotSplit() {
        var p = new P { Name = "a!=b", Age = 1 };
        Assert.True(TestExpressionEvaluator.Evaluate("Name == 'a!=b'", p));
    }

    [Fact]
    public void Literal_CombinedWithRealOperator_StillEvaluates() {
        var p = new P { Name = "x or y", Age = 30 };
        Assert.True(TestExpressionEvaluator.Evaluate("Age > 18 and Name == 'x or y'", p));
    }

    // ── 대소문자 ────────────────────────────────────────────────────────

    [Fact]
    public void MixedCase_Or_IsRecognized() {
        var p = new P { Name = "x", Age = 5 };
        Assert.True(TestExpressionEvaluator.Evaluate("Age > 1 Or Age > 100", p));
    }

    [Fact]
    public void MixedCase_And_IsRecognized() {
        var p = new P { Name = "x", Age = 5 };
        Assert.False(TestExpressionEvaluator.Evaluate("Age > 1 And Age > 100", p));
    }

    [Fact]
    public void MixedCase_AndTrue_IsTrue() {
        var p = new P { Name = "x", Age = 5 };
        Assert.True(TestExpressionEvaluator.Evaluate("Age > 1 and Age > 2", p));
    }

    [Fact]
    public void Uppercase_OR_IsRecognized() {
        var p = new P { Name = "x", Age = 5 };
        Assert.True(TestExpressionEvaluator.Evaluate("Age > 100 OR Age > 2", p));
    }

    // ── 괄호 ───────────────────────────────────────────────────────────

    [Fact]
    public void Parenthesis_OverridingOrPrecedence() {
        var p = new P { Name = "x", Age = 5 };
        Assert.True(TestExpressionEvaluator.Evaluate("(Age > 100 or Age > 1) and Age > 2", p));
    }

    [Fact]
    public void Parenthesis_GroupingTakesPrecedence() {
        var p = new P { Name = "x", Age = 5 };
        // (true and true) or false → 구버전은 괄호를 무시해 선행 or가 걸러져 false가 되었다
        Assert.True(TestExpressionEvaluator.Evaluate("(Age > 1 and Age > 2) or Age > 100", p));
    }

    [Fact]
    public void Parenthesis_SimpleGrouping() {
        var p = new P { Name = "x", Age = 5 };
        Assert.True(TestExpressionEvaluator.Evaluate("(Age > 1)", p));
    }

    [Fact]
    public void Parenthesis_ContainingOr_DoesNotSplit() {
        var p = new P { Name = "x or y", Age = 5 };
        Assert.True(TestExpressionEvaluator.Evaluate("(Name == 'x or y')", p));
    }

    [Fact]
    public void Parenthesis_Nested() {
        var p = new P { Name = "x", Age = 5 };
        Assert.True(TestExpressionEvaluator.Evaluate("((Age > 1) and (Age > 2))", p));
    }

    // ── 식별자 경계 (Order가 or로 오인되면 안 됨) ─────────────────────────

    [Fact]
    public void PropertyName_ContainingOrSubstring_IsNotSplit() {
        var p = new P { Name = "abc", Age = 5 };
        // "Order" 라는 프로퍼티가 없다면 null → false. " or " 로 잘리지 않아야 한다.
        Assert.False(TestExpressionEvaluator.Evaluate("Order == 1", p));
    }

    [Fact]
    public void NestedProperty_WithLogicalOperators() {
        var p = new P { Name = "x", Age = 5, Nested = new Nested { City = "seoul" } };
        Assert.True(TestExpressionEvaluator.Evaluate("Nested.City == 'seoul' and Age > 1", p));
    }

    // ── 종전 동작 유지 확인 (빈 조각 = 무시) ──────────────────────────────

    [Fact]
    public void TrailingOperator_BlankOperandIsIgnored() {
        var p = new P { Name = "abc", Age = 5 };
        // 분리 후 조각이 1개("Name and"가 아니라)라 논리 연산 미적용 → "Name and" 프로퍼티 조회 → false.
        // 종전 Split(RemoveEmptyEntries) 동작과 동일하게 false가 나와야 한다(호환성).
        Assert.False(TestExpressionEvaluator.Evaluate("Name and ", p));
    }

    [Fact]
    public void LeadingOperator_BlankOperandIsIgnored() {
        var p = new P { Name = "abc", Age = 5 };
        // 분리 후 조각이 1개 → "or Name" 전체를 프로퍼티 경로로 조회 → false. 종전과 동일.
        Assert.False(TestExpressionEvaluator.Evaluate(" or Name", p));
    }
}
