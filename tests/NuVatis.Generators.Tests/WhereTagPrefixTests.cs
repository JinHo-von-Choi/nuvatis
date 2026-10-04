using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NuVatis.Generators.Emitters;
using NuVatis.Generators.Models;
using Xunit;

namespace NuVatis.Generators.Tests;

/**
 * &lt;where&gt; 태그의 논리 연산자 접두사 처리를 검증한다.
 *
 * 배경: 두 제품 경로 모두 누적된 내용의 "맨 앞"에서 AND/OR 접두사를 한 번만 제거한다
 * (ParameterEmitter.EmitWhereNode, EmitLambdaNode 의 __wc_ 블록).
 * 조건 사이에 구분 공백이 없으면 조건이 2개 이상 동시에 참일 때
 * "WHERE name = @p0AND age >= @p1" 처럼 파싱 불가한 SQL이 생성된다.
 *
 * 이 테스트는 방출된 코드를 실제로 컴파일·실행해 최종 SQL 문자열을 확인한다.
 * 문자열 복사본이 아니라 진짜 생성 텍스트를 실행하므로 복사 오차가 개입하지 않는다.
 * 정적 경로(EmitBuildSqlStaticMethod)와 람다 경로(EmitDynamicBuilderLambda) 모두를 태운다.
 */
public class WhereTagPrefixTests {

    private sealed class SearchParam {
        public string? Name   { get; set; }
        public int?    MinAge { get; set; }
        public string? Status { get; set; }
    }

    private static IfNode Cond(string test, string prefix, string prop) {
        return new IfNode(test, ImmutableArray.Create<ParsedSqlNode>(
            new MixedNode(ImmutableArray.Create<ParsedSqlNode>(
                new TextNode(prefix + prop + " = "),
                new ParameterNode(prop, false)))));
    }

    private static ParsedStatement StatementWith(params IfNode[] conds) {
        var children = ImmutableArray.Create<ParsedSqlNode>(
            new TextNode("SELECT * FROM orders\n"),
            new WhereNode(ImmutableArray.Create<ParsedSqlNode>(conds)));

        return new ParsedStatement("Search", "Select", null, null, null, new MixedNode(children));
    }

    // ─────────────────────────────────────────────
    // 실행 하네스
    // ─────────────────────────────────────────────

    private static (string Static, string Lambda) RunBothPaths(ParsedStatement stmt, object? param) {
        var staticMethod = ParameterEmitter.EmitBuildSqlStaticMethod(stmt, "@");
        var lambda       = ParameterEmitter.EmitDynamicBuilderLambda(stmt.RootNode, "@");

        var wrapper = new StringBuilder();
        wrapper.AppendLine("using System;");
        wrapper.AppendLine("using System.Collections.Generic;");
        wrapper.AppendLine("using System.Data.Common;");
        wrapper.AppendLine("public static class Probe {");
        wrapper.AppendLine(staticMethod);
        wrapper.AppendLine("    public static string Static(object? p) => BuildSql_" + Sanitize(stmt.Id) + "(p).Sql;");
        wrapper.AppendLine("    public static string Lambda(object? p) => Build(p).Item1;");
        wrapper.AppendLine("    static Func<object?, (string, List<DbParameter>)> Build = " + lambda.TrimEnd() + ";");
        wrapper.AppendLine("}");

        var references = new MetadataReference[] {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Data.Common").Location),
            MetadataReference.CreateFromFile(typeof(NuVatis.Binding.ParameterBinder).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            "WhereProbe",
            new[] { CSharpSyntaxTree.ParseText(wrapper.ToString()) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms);
        Assert.True(emitResult.Success,
            "생성 코드 컴파일 실패: " + string.Join("\n",
                emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));

        var probe = Assembly.Load(ms.ToArray()).GetType("Probe")!;
        return ((string)probe.GetMethod("Static")!.Invoke(null, new object?[] { param })!,
                (string)probe.GetMethod("Lambda")!.Invoke(null, new object?[] { param })!);
    }

    private static string Sanitize(string id) {
        var sb = new StringBuilder(id.Length);
        foreach (var c in id) sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        return sb.ToString();
    }

    /// <summary>두 제품 경로의 출력이 모두 기대 문자열을 만족하는지 확인한다.</summary>
    private static void AssertBothPaths(ParsedStatement stmt, object? param, params string[] expected) {
        var (fromStatic, fromLambda) = RunBothPaths(stmt, param);

        foreach (var (label, sql) in new[] { ("static", fromStatic), ("lambda", fromLambda) }) {
            foreach (var e in expected) {
                Assert.True(sql.Contains(e, StringComparison.Ordinal),
                    $"{label} 경로에서 '{e}' 를 찾지 못했다.\n실제: {sql.Replace("\n", "\\n")}");
            }
        }
    }

    private static void AssertBothPathsExclude(ParsedStatement stmt, object? param, string forbidden) {
        var (fromStatic, fromLambda) = RunBothPaths(stmt, param);

        foreach (var (label, sql) in new[] { ("static", fromStatic), ("lambda", fromLambda) }) {
            Assert.DoesNotContain(forbidden, sql);
        }
    }

    // ─────────────────────────────────────────────
    // 회귀 — 2개 이상 조건이 동시에 참일 때
    // ─────────────────────────────────────────────

    [Fact]
    public void Where_TwoActiveConditions_SeparatesOperators() {
        var stmt = StatementWith(
            Cond("Name != null", "AND ", "Name"),
            Cond("MinAge != null", "AND ", "MinAge"));

        // 파라미터 자리표시자 뒤에 공백 없이 AND 가 붙으면 파싱 불가한 SQL이다
        AssertBothPathsExclude(stmt, new SearchParam { Name = "abc", MinAge = 30 }, "@p0AND");
        AssertBothPaths(stmt, new SearchParam { Name = "abc", MinAge = 30 },
                        "WHERE Name = @p0 AND MinAge = @p1");
    }

    [Fact]
    public void Where_ThreeActiveConditions_SeparatesOperators() {
        var stmt = StatementWith(
            Cond("Name != null", "AND ", "Name"),
            Cond("MinAge != null", "AND ", "MinAge"),
            new IfNode("Status != null", ImmutableArray.Create<ParsedSqlNode>(
                new TextNode("AND status = 'open'"))));

        AssertBothPathsExclude(stmt,
            new SearchParam { Name = "abc", MinAge = 30, Status = "open" }, "@p0AND");
        AssertBothPaths(stmt,
            new SearchParam { Name = "abc", MinAge = 30, Status = "open" },
            "WHERE Name = @p0 AND MinAge = @p1 AND status = 'open'");
    }

    [Fact]
    public void Where_TwoActiveConditions_WithOrPrefix_SeparatesOperators() {
        var stmt = StatementWith(
            Cond("Name != null", "OR ", "Name"),
            Cond("MinAge != null", "OR ", "MinAge"));

        AssertBothPathsExclude(stmt, new SearchParam { Name = "abc", MinAge = 30 }, "@p0OR");
        AssertBothPaths(stmt, new SearchParam { Name = "abc", MinAge = 30 },
                        "WHERE Name = @p0 OR MinAge = @p1");
    }

    // ─────────────────────────────────────────────
    // 기존 동작 보존 — 0개 / 1개 조건
    // ─────────────────────────────────────────────

    [Fact]
    public void Where_NoActiveConditions_EmitsNoWhereClause() {
        var stmt = StatementWith(
            Cond("Name != null", "AND ", "Name"),
            Cond("MinAge != null", "AND ", "MinAge"));

        AssertBothPathsExclude(stmt, new SearchParam(), "WHERE");
    }

    [Fact]
    public void Where_SingleActiveCondition_KeepsExistingShape() {
        var stmt = StatementWith(
            Cond("Name != null", "AND ", "Name"),
            Cond("MinAge != null", "AND ", "MinAge"));

        AssertBothPaths(stmt, new SearchParam { Name = "abc" },
                        "WHERE Name = @p0");
        AssertBothPathsExclude(stmt, new SearchParam { Name = "abc" }, "MinAge");
    }

    [Fact]
    public void Where_ConditionWithoutPrefix_IsNotMangled() {
        // 접두사 없이 쓰는 XML도 흔하다 — WHERE 키워드가 중복되거나 접두사가 붙으면 안 된다
        var stmt = StatementWith(
            Cond("Name != null", "", "Name"),
            Cond("MinAge != null", "", "MinAge"));

        var (fromStatic, fromLambda) = RunBothPaths(stmt, new SearchParam { Name = "abc", MinAge = 30 });

        foreach (var sql in new[] { fromStatic, fromLambda }) {
            Assert.Equal(1, CountOccurrences(sql, "WHERE"));
            Assert.DoesNotContain("AND ", sql);
        }
    }

    private static int CountOccurrences(string haystack, string needle) {
        var count = 0;
        var idx   = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0) {
            count++;
            idx += needle.Length;
        }
        return count;
    }
}
