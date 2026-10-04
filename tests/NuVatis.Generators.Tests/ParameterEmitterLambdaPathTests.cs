using System.Collections.Immutable;
using NuVatis.Generators.Emitters;
using NuVatis.Generators.Models;
using Xunit;

namespace NuVatis.Generators.Tests;

/**
 * 제품이 실제로 방출하는 경로(EmitBuildSqlStaticMethod / EmitDynamicBuilderLambda)의
 * ${} 문자열 치환 동작을 고정한다.
 *
 * 배경: paramTypeMap 기반 컴파일 타임 최적화(SqlIdentifier 타입 판별 → 가드 생략)는
 * 폐기 예정 경로(EmitBuildSqlMethod)에만 남아 있다. 제품 경로는 paramTypeMap을 받지
 * 않으며 ${} 에 항상 런타임 타입 가드를 방출한다. 이 차이는 안전성에는 영향이 없지만
 * (가드가 더 엄격하다) 성능 특성이 다르므로, 실제 동작을 명시적으로 고정한다.
 */
public class ParameterEmitterLambdaPathTests {

    // ParsedStatement(Id, StatementType, ResultMapId?, ResultType?, ParameterType?, RootNode, Timeout?)
    private static ParsedStatement SubstitutionStatement(
        string id, string paramName = "SortColumn", IReadOnlyList<ParsedSqlNode>? prefix = null) {

        var children = ImmutableArray.CreateBuilder<ParsedSqlNode>();
        if (prefix is not null) children.AddRange(prefix);
        children.Add(new ParameterNode(paramName, IsStringSubstitution: true));

        return new ParsedStatement(
            id, "Select", null, null, null,
            new MixedNode(children.ToImmutable()));
    }

    [Fact]
    public void StringSubstitution_Always_EmitsRuntimeGuard() {
        var stmt = SubstitutionStatement("SelectOrdered");

        var code = ParameterEmitter.EmitBuildSqlStaticMethod(stmt, "@");

        Assert.Contains("is not NuVatis.Core.Sql.SqlIdentifier", code);
        Assert.Contains("throw new System.InvalidOperationException", code);
        Assert.Contains("치환에는 SqlIdentifier 타입이 필요합니다", code);
    }

    [Fact]
    public void StringSubstitution_UsesLambdaScopedBuffer() {
        // 제품 경로는 __sb_ / __params_ / __idx_ 로컬 변수를 사용한다
        // (구 경로의 sb / parameters / paramIndex / dbFactory 와 다르다)
        var stmt = SubstitutionStatement("SelectOrdered");

        var code = ParameterEmitter.EmitBuildSqlStaticMethod(stmt, "@");

        Assert.Contains("__sb_", code);
        Assert.Contains("__params_", code);
        Assert.Contains("__idx_", code);
        Assert.DoesNotContain("dbFactory.CreateParameter()", code);
    }

    [Fact]
    public void StringSubstitution_HasNoDbFactoryDependency() {
        // EmitBuildSqlStaticMethod 시그니처는 dbFactory를 받지 않는다 —
        // NuVatis.Binding.ParameterBinder.CreateParameter로 생성한다
        var stmt = SubstitutionStatement("SelectOrdered");

        var code = ParameterEmitter.EmitBuildSqlStaticMethod(stmt, "@");

        Assert.DoesNotContain("dbFactory", code);
    }

    [Fact]
    public void StringSubstitution_Guard_IsEmittedInLambdaPathToo() {
        // EmitDynamicBuilderLambda(레지스트리 경로)도 동일한 가드를 내야 한다
        var node = new ParameterNode("SortColumn", IsStringSubstitution: true);

        var lambda = ParameterEmitter.EmitDynamicBuilderLambda(
            new MixedNode(ImmutableArray.Create<ParsedSqlNode>(
                new TextNode("SELECT * FROM t ORDER BY "), node)), "@");

        Assert.Contains("is not NuVatis.Core.Sql.SqlIdentifier", lambda);
        Assert.Contains("System.InvalidOperationException", lambda);
    }

    [Fact]
    public void StringSubstitution_EmitsLocalHelperGetprop() {
        // 두 제품 경로 모두 __getprop_ 지역 함수를 방출한다
        var stmt = SubstitutionStatement("SelectOrdered");

        var code = ParameterEmitter.EmitBuildSqlStaticMethod(stmt, "@");

        Assert.Contains("static object? __getprop_(", code);
        Assert.Contains("BindingFlags.IgnoreCase", code);
    }

    [Fact]
    public void RegularParameter_EmitsDbParameterBinding() {
        var root = new MixedNode(ImmutableArray.Create<ParsedSqlNode>(
            new TextNode("SELECT * FROM t WHERE id = "),
            new ParameterNode("Id", IsStringSubstitution: false)));
        var stmt = new ParsedStatement("GetById", "Select", null, null, null, root);

        var code = ParameterEmitter.EmitBuildSqlStaticMethod(stmt, "@");

        Assert.Contains("__params_", code);
        Assert.Contains("ParameterBinder.CreateParameter", code);
    }

    [Fact]
    public void BothProductPaths_EmitIdenticalGuardSemantics() {
        // 두 제품 경로의 가드 의미가 같음을 확인 — 출력 문자열은 달라도
        // "SqlIdentifier 타입이 아니면 예외"라는 계약은 동일해야 한다
        var node = new ParameterNode("SortColumn", IsStringSubstitution: true);
        var root = new MixedNode(ImmutableArray.Create<ParsedSqlNode>(node));
        var stmt = new ParsedStatement("S", "Select", null, null, null, root);

        var staticPath = ParameterEmitter.EmitBuildSqlStaticMethod(stmt, "@");
        var lambdaPath = ParameterEmitter.EmitDynamicBuilderLambda(root, "@");

        foreach (var (label, code) in new[] { ("static", staticPath), ("lambda", lambdaPath) }) {
            Assert.Contains("is not NuVatis.Core.Sql.SqlIdentifier", code);
            Assert.Contains("System.InvalidOperationException", code);
        }
    }
}
