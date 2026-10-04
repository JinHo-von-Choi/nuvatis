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
 * 파라미터 정적 타입 해석 기반 직접 접근 방출(__typed_)을 검증한다.
 *
 * WhereTagPrefixTests와 같은 방식 — 방출된 코드를 실제로 컴파일·실행한다.
 * 문자열 검사만으로는 "직접 접근이 나간 결과가 컴파일되는지"와
 * "해석 실패 시 리플렉션 폴백이 실제로 동작하는지"를 확인할 수 없다.
 */
public class TypedPropertyAccessTests {

    private sealed class OrderQuery {
        public string? Name   { get; set; }
        public int?    MinAge { get; set; }
        public string? Status { get; set; }
        public string? SortBy { get; set; }
    }

    private static ParsedStatement SearchStatement(params string[] paramNames) {
        var children = ImmutableArray.CreateBuilder<ParsedSqlNode>();
        children.Add(new TextNode("SELECT * FROM orders"));
        foreach (var p in paramNames) {
            children.Add(new MixedNode(ImmutableArray.Create<ParsedSqlNode>(
                new TextNode(" AND " + p + " = "),
                new ParameterNode(p, false))));
        }
        return new ParsedStatement("Search", "Select", null, null, null, new MixedNode(children.ToImmutable()));
    }

    /**
     * EmitBuildSqlStaticMethod가 만든 정적 메서드를 컴파일해 실행하고
     * (SQL, 파라미터 값들)을 반환한다.
     *
     * @param typedAccess null이면 파이프라인에서 심볼 해석이 실패한 상황(= 폴백 경로)
     */
    private static (string Sql, List<string> Values) Run(
        ParsedStatement stmt,
        string paramTypeFqn,
        string paramTypeDecl,
        bool passNull,
        object? typedAccess) {

        var methodSource = ParameterEmitter.EmitBuildSqlStaticMethod(
            stmt, "@", null, (NuVatis.Generators.Analysis.TypedPropertyAccess?)typedAccess);

        var wrapper = new StringBuilder();
        wrapper.AppendLine("using System;");
        wrapper.AppendLine("using System.Collections.Generic;");
        wrapper.AppendLine("using System.Data.Common;");
        if (paramTypeDecl.Length > 0) wrapper.AppendLine(paramTypeDecl);
        wrapper.AppendLine("public static class Probe {");
        wrapper.AppendLine(methodSource);
        // 파라미터 인스턴스를 프록이 직접 만들어야 한다. 테스트 어셈블리의 타입을 넘기면
        // 어셈블리가 달라 __typed_ 의 "is" 검사가 실패해 종전과 다른 경로가 된다.
        wrapper.AppendLine("    public static string[] Run(bool passNull) {");
        wrapper.AppendLine("        var p = passNull ? null : (object)new TestApp.Models.OrderQuery { Name = \"abc\", MinAge = 30 };");
        wrapper.AppendLine("        var r = BuildSql_" + Sanitize(stmt.Id) + "(p);");
        wrapper.AppendLine("        var vals = new List<string>();");
        wrapper.AppendLine("        foreach (var q in r.Parameters) vals.Add(q.Value is DBNull ? \"<DBNull>\" : Convert.ToString(q.Value) ?? \"<null>\");");
        wrapper.AppendLine("        vals.Insert(0, r.Sql);");
        wrapper.AppendLine("        return vals.ToArray();");
        wrapper.AppendLine("    }");
        wrapper.AppendLine("}");

        var references = new MetadataReference[] {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Data.Common").Location),
            MetadataReference.CreateFromFile(typeof(NuVatis.Binding.ParameterBinder).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            "TypedProbe",
            new[] { CSharpSyntaxTree.ParseText(wrapper.ToString()) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var emit = compilation.Emit(ms);
        Assert.True(emit.Success, "생성 코드 컴파일 실패: " + string.Join("\n",
            emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));

        var probe = Assembly.Load(ms.ToArray()).GetType("Probe")!;
        var result = (string[])probe.GetMethod("Run")!.Invoke(null, new object?[] { passNull })!;
        return (result[0], result.Skip(1).ToList());
    }

    private static string Sanitize(string id) {
        var sb = new StringBuilder(id.Length);
        foreach (var c in id) sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        return sb.ToString();
    }

    private const string OrderQueryDecl = @"
namespace TestApp.Models
{
    public class OrderQuery
    {
        public string? Name { get; set; }
        public int? MinAge { get; set; }
        public string? Status { get; set; }
        public string? SortBy { get; set; }
    }
}";

    private static object ResolveAccess(string fqn, params string[] referenced) {
        var tree = CSharpSyntaxTree.ParseText(OrderQueryDecl);
        var refs = new MetadataReference[] {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location),
        };
        var comp = CSharpCompilation.Create("Resolve", new[] { tree }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var access = NuVatis.Generators.Analysis.TypedPropertyAccessResolver.Resolve(
            comp, fqn, SearchStatement(referenced).RootNode);
        Assert.NotNull(access);
        return access;
    }

    /** NotNull 단언 없이 해석 결과(null 가능)를 그대로 돌려준다. */
    private static object? ResolveNullable(string fqn, params string[] referenced) {
        var comp = CSharpCompilation.Create(
            "Resolve2",
            new[] { CSharpSyntaxTree.ParseText(OrderQueryDecl) },
            new MetadataReference[] {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
                MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return NuVatis.Generators.Analysis.TypedPropertyAccessResolver.Resolve(
            comp, fqn, SearchStatement(referenced).RootNode);
    }

    // ─────────────────────────────────────────────
    // 직접 접근 경로
    // ─────────────────────────────────────────────

    [Fact]
    public void ResolvedType_EmitsDirectAccess_AndRuns() {
        var stmt  = SearchStatement("Name", "MinAge");
        var typed = ResolveAccess("TestApp.Models.OrderQuery", "Name", "MinAge");

        var (sql, values) = Run(stmt, "TestApp.Models.OrderQuery", OrderQueryDecl, false, typed);

        Assert.Equal("SELECT * FROM orders AND Name = @p0 AND MinAge = @p1", sql);
        Assert.Equal(new[] { "abc", "30" }, values);
    }

    [Fact]
    public void ResolvedType_EmitsNoReflectionForResolvedProperties() {
        var code = ParameterEmitter.EmitBuildSqlStaticMethod(
            SearchStatement("Name"), "@", null,
            (NuVatis.Generators.Analysis.TypedPropertyAccess)ResolveAccess(
                "TestApp.Models.OrderQuery", "Name"));

        Assert.Contains("__typed_?.Name", code);
        // 해석된 프로퍼티에 대해서는 GetProperty 호출이 남아 있으면 안 된다
        Assert.DoesNotContain("__getprop_(__param_, \"Name\")", code);
        // 폴백용 지역 함수는 그대로 유지된다 (중첩 경로·미해석 프로퍼티용)
        Assert.Contains("static object? __getprop_(", code);
    }

    [Fact]
    public void CastToObject_IsEmittedForNullableValueTypes() {
        // (object?) 캐스트가 없으면 "int? ?? DBNull.Value" 가 컴파일되지 않는다
        var code = ParameterEmitter.EmitBuildSqlStaticMethod(
            SearchStatement("MinAge"), "@", null,
            (NuVatis.Generators.Analysis.TypedPropertyAccess)ResolveAccess(
                "TestApp.Models.OrderQuery", "MinAge"));

        Assert.Contains("(object?)__typed_?.MinAge ?? System.DBNull.Value", code);
    }

    [Fact]
    public void CaseInsensitiveMatch_EmitsDeclaredCasing() {
        // __getprop_는 IgnoreCase로 찾는다 — 대소문자가 달라도 같은 프로퍼티여야 한다
        var code = ParameterEmitter.EmitBuildSqlStaticMethod(
            SearchStatement("name"), "@", null,
            (NuVatis.Generators.Analysis.TypedPropertyAccess)ResolveAccess(
                "TestApp.Models.OrderQuery", "name"));

        Assert.Contains("__typed_?.Name", code);
    }

    // ─────────────────────────────────────────────
    // 폴백 경로 — 기존 동작 보존
    // ─────────────────────────────────────────────

    [Fact]
    public void UnresolvableProperty_FallsBackToReflection_AndReturnsDbNull() {
        // 타입에는 없는 프로퍼티 — 컴파일 에러가 나면 안 되고 종전처럼 null이어야 한다
        var stmt = SearchStatement("Nonexistent");

        // 미해석 프로퍼티만 있으면 Resolve 가 null을 준다 → 전량 리플렉션 폴백
        Assert.Null(ResolveNullable("TestApp.Models.OrderQuery", "Nonexistent"));

        var (sql, values) = Run(stmt, "TestApp.Models.OrderQuery", OrderQueryDecl, false, null);

        Assert.Contains("Nonexistent", sql);
        Assert.Equal(new[] { "<DBNull>" }, values);
    }

    [Fact]
    public void UnknownType_KeepsReflectionPath() {
        // 컴파일에서 타입을 못 찾으면 해석이 null이므로 전량 리플렉션
        var access = NuVatis.Generators.Analysis.TypedPropertyAccessResolver.Resolve(
            CSharpCompilation.Create("X", null, null), "No.Such.Type", SearchStatement("Name").RootNode);
        Assert.Null(access);
    }

    [Fact]
    public void ObjectParameterType_KeepsReflectionPath() {
        // object 파라미터는 정적 타입 정보를 주지 못한다
        var access = NuVatis.Generators.Analysis.TypedPropertyAccessResolver.Resolve(
            CSharpCompilation.Create("X", null, null), "object", SearchStatement("Name").RootNode);
        Assert.Null(access);
    }

    [Fact]
    public void NullTypeName_KeepsReflectionPath() {
        var access = NuVatis.Generators.Analysis.TypedPropertyAccessResolver.Resolve(
            CSharpCompilation.Create("X", null, null), null, SearchStatement("Name").RootNode);
        Assert.Null(access);
    }

    [Fact]
    public void NullParameter_FallsBackToNullSemantics() {
        // 타입이 맞지 않거나 null이면 __typed_ 가 null 이므로 종전과 동일하게 null
        var stmt  = SearchStatement("Name");
        var typed = ResolveAccess("TestApp.Models.OrderQuery", "Name");

        var (sql, values) = Run(stmt, "TestApp.Models.OrderQuery", OrderQueryDecl, true, typed);

        Assert.Equal("SELECT * FROM orders AND Name = @p0", sql);
        Assert.Equal(new[] { "<DBNull>" }, values);
    }

    [Fact]
    public void WithoutTypedAccess_GeneratedCodeStillCompilesAndRuns() {
        // typedAccess가 null일 때(= 해석 실패) 기존 리플렉션 경로가 그대로 동작해야 한다
        var (sql, values) = Run(SearchStatement("Name", "MinAge"),
            "TestApp.Models.OrderQuery", OrderQueryDecl, false, null);

        Assert.Equal("SELECT * FROM orders AND Name = @p0 AND MinAge = @p1", sql);
        Assert.Equal(new[] { "abc", "30" }, values);
    }
}
