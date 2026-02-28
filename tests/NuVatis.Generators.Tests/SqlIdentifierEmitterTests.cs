using System.Collections.Generic;
using System.Collections.Immutable;
using NuVatis.Generators.Emitters;
using NuVatis.Generators.Models;
using Xunit;

namespace NuVatis.Generators.Tests;

/**
 * ParameterEmitter의 ${} 문자열 치환 경로에서 SqlIdentifier 타입 가드 검증.
 *
 * 파라미터 타입이 SqlIdentifier인 경우 ToString() 직접 호출,
 * string 등 다른 타입인 경우 InvalidOperationException 런타임 가드 삽입을 검증한다.
 *
 * @author 최진호
 * @date   2026-02-28
 */
public class SqlIdentifierEmitterTests {

    // -----------------------------------------------------------------------
    // 공통 헬퍼
    // -----------------------------------------------------------------------

    private static ParsedStatement BuildStatement(string id, ParsedSqlNode root) {
        return new ParsedStatement(id, "Select", null, null, null, root);
    }

    private static string Emit(
        ParsedStatement stmt,
        IReadOnlyDictionary<string, string>? paramTypeMap = null,
        string prefix = "@") {
        return ParameterEmitter.EmitBuildSqlMethod(stmt, prefix, paramTypeMap);
    }

    // -----------------------------------------------------------------------
    // 1. SqlIdentifier 타입 → ToString() 직접 호출, 런타임 가드 없음
    // -----------------------------------------------------------------------

    [Fact]
    public void SqlIdentifier_Param_EmitsToString_WithoutRuntimeGuard() {
        var node = new ParameterNode("SortColumn", IsStringSubstitution: true);
        var stmt = BuildStatement("selectSorted", node);

        var typeMap = new Dictionary<string, string>
        {
            ["SortColumn"] = "NuVatis.Core.Sql.SqlIdentifier"
        };

        var code = Emit(stmt, typeMap);

        // ToString() 직접 호출 포함
        Assert.Contains("GetPropertyValue(param, \"SortColumn\")?.ToString()", code);

        // InvalidOperationException 런타임 가드 없음
        Assert.DoesNotContain("InvalidOperationException", code);
    }

    [Fact]
    public void SqlIdentifier_ShortName_EmitsRuntimeGuard() {
        // FQN 없이 짧은 타입명("SqlIdentifier")만 전달하면 신뢰할 수 없어 런타임 가드를 삽입해야 한다
        var node = new ParameterNode("TableName", IsStringSubstitution: true);
        var stmt = BuildStatement("selectFromTable", node);

        var typeMap = new Dictionary<string, string>
        {
            ["TableName"] = "SqlIdentifier"
        };

        var code = Emit(stmt, typeMap);

        // 짧은 타입명은 FQN 정확 일치 실패 → 런타임 가드 삽입
        Assert.Contains("InvalidOperationException", code);
        Assert.Contains("is not NuVatis.Core.Sql.SqlIdentifier", code);
    }

    [Fact]
    public void SimilarSuffixTypeName_ShouldEmitRuntimeGuard() {
        // MySqlIdentifier, FakeSqlIdentifier 같은 유사 타입명은 FQN 불일치로 가드가 삽입되어야 한다
        var node = new ParameterNode("col", IsStringSubstitution: true);
        var stmt = BuildStatement("bypassAttempt", node);

        var typeMap = new Dictionary<string, string>
        {
            ["col"] = "MySqlIdentifier"
        };

        var code = Emit(stmt, typeMap);

        // 유사 타입명은 가드 삽입 필수
        Assert.Contains("InvalidOperationException", code);
        Assert.Contains("is not NuVatis.Core.Sql.SqlIdentifier", code);
        // ToString() 직접 호출 경로가 없어야 한다
        Assert.DoesNotContain("?.ToString()", code);
    }

    // -----------------------------------------------------------------------
    // 2. string 타입 → InvalidOperationException 런타임 가드 삽입
    // -----------------------------------------------------------------------

    [Fact]
    public void String_Param_EmitsRuntimeGuard() {
        var node = new ParameterNode("SortColumn", IsStringSubstitution: true);
        var stmt = BuildStatement("unsafeSelect", node);

        var typeMap = new Dictionary<string, string>
        {
            ["SortColumn"] = "string"
        };

        var code = Emit(stmt, typeMap);

        // 런타임 가드: SqlIdentifier 타입 체크
        Assert.Contains("InvalidOperationException", code);
        Assert.Contains("is not NuVatis.Core.Sql.SqlIdentifier", code);
    }

    [Fact]
    public void NonSqlIdentifier_Param_EmitsRuntimeGuard() {
        var node = new ParameterNode("OrderBy", IsStringSubstitution: true);
        var stmt = BuildStatement("queryWithOrder", node);

        var typeMap = new Dictionary<string, string>
        {
            ["OrderBy"] = "System.String"
        };

        var code = Emit(stmt, typeMap);

        Assert.Contains("InvalidOperationException", code);
        Assert.Contains("is not NuVatis.Core.Sql.SqlIdentifier", code);
    }

    // -----------------------------------------------------------------------
    // 3. 타입 정보 없음 (null 맵 또는 키 없음) → 런타임 가드 삽입 (안전한 기본값)
    // -----------------------------------------------------------------------

    [Fact]
    public void NullTypeMap_EmitsRuntimeGuard() {
        var node = new ParameterNode("SortColumn", IsStringSubstitution: true);
        var stmt = BuildStatement("unsafeSelect", node);

        var code = Emit(stmt, paramTypeMap: null);

        // 타입 정보 없으면 보수적으로 가드 삽입
        Assert.Contains("InvalidOperationException", code);
        Assert.Contains("is not NuVatis.Core.Sql.SqlIdentifier", code);
    }

    [Fact]
    public void MissingKey_In_TypeMap_EmitsRuntimeGuard() {
        var node = new ParameterNode("SortColumn", IsStringSubstitution: true);
        var stmt = BuildStatement("unsafeSelect", node);

        var typeMap = new Dictionary<string, string>
        {
            ["OtherParam"] = "string"
        };

        var code = Emit(stmt, typeMap);

        Assert.Contains("InvalidOperationException", code);
        Assert.Contains("is not NuVatis.Core.Sql.SqlIdentifier", code);
    }

    // -----------------------------------------------------------------------
    // 4. 일반 파라미터 바인딩(#{}) 경로는 영향받지 않음
    // -----------------------------------------------------------------------

    [Fact]
    public void HashParam_IsNotAffected_ByTypeMap() {
        var node = new ParameterNode("id", IsStringSubstitution: false);
        var stmt = BuildStatement("selectById", node);

        var typeMap = new Dictionary<string, string>
        {
            ["id"] = "int"
        };

        var code = Emit(stmt, typeMap);

        // 일반 파라미터 바인딩: DbParameter 생성
        Assert.Contains("dbFactory.CreateParameter()", code);
        Assert.DoesNotContain("InvalidOperationException", code);
    }

    [Fact]
    public void HashParam_WithoutTypeMap_WorksAsUsual() {
        var node = new ParameterNode("userId", IsStringSubstitution: false);
        var stmt = BuildStatement("getUser", node);

        var code = Emit(stmt, paramTypeMap: null);

        Assert.Contains("dbFactory.CreateParameter()", code);
        Assert.DoesNotContain("InvalidOperationException", code);
    }

    // -----------------------------------------------------------------------
    // 5. 혼합 노드: SqlIdentifier + string 혼합
    // -----------------------------------------------------------------------

    [Fact]
    public void Mixed_SqlIdentifier_And_String_EmitsCorrectly() {
        var root = new MixedNode(ImmutableArray.Create<ParsedSqlNode>(
            new TextNode("SELECT * FROM users ORDER BY "),
            new ParameterNode("SortColumn", IsStringSubstitution: true),
            new TextNode(" WHERE id = "),
            new ParameterNode("id", IsStringSubstitution: false)
        ));

        var stmt = BuildStatement("mixedQuery", root);

        var typeMap = new Dictionary<string, string>
        {
            ["SortColumn"] = "NuVatis.Core.Sql.SqlIdentifier",
            ["id"]         = "int"
        };

        var code = Emit(stmt, typeMap);

        // SortColumn: 가드 없음
        Assert.DoesNotContain("InvalidOperationException", code);
        // id: DbParameter 생성
        Assert.Contains("dbFactory.CreateParameter()", code);
    }

    // -----------------------------------------------------------------------
    // 6. IfNode 내부의 ${} 파라미터도 typeMap 전파 검증
    // -----------------------------------------------------------------------

    [Fact]
    public void IfNode_Inner_SqlIdentifier_EmitsToString() {
        var root = new IfNode(
            "SortColumn != null",
            ImmutableArray.Create<ParsedSqlNode>(
                new TextNode("ORDER BY "),
                new ParameterNode("SortColumn", IsStringSubstitution: true)
            ));

        var stmt = BuildStatement("conditionalSort", root);

        var typeMap = new Dictionary<string, string>
        {
            ["SortColumn"] = "NuVatis.Core.Sql.SqlIdentifier"
        };

        var code = Emit(stmt, typeMap);

        Assert.Contains("GetPropertyValue(param, \"SortColumn\")?.ToString()", code);
        Assert.DoesNotContain("InvalidOperationException", code);
    }

    [Fact]
    public void IfNode_Inner_String_EmitsRuntimeGuard() {
        var root = new IfNode(
            "SortColumn != null",
            ImmutableArray.Create<ParsedSqlNode>(
                new TextNode("ORDER BY "),
                new ParameterNode("SortColumn", IsStringSubstitution: true)
            ));

        var stmt = BuildStatement("conditionalSort", root);

        var typeMap = new Dictionary<string, string>
        {
            ["SortColumn"] = "string"
        };

        var code = Emit(stmt, typeMap);

        Assert.Contains("InvalidOperationException", code);
    }

    // -----------------------------------------------------------------------
    // 7. 하위 호환: 기존 2-파라미터 시그니처 없어도 빌드 가능 (optional 파라미터 확인)
    // -----------------------------------------------------------------------

    [Fact]
    public void LegacyCall_WithoutTypeMap_StillBuilds() {
        var node = new ParameterNode("SortColumn", IsStringSubstitution: true);
        var stmt = BuildStatement("legacyCall", node);

        // paramTypeMap 없이 호출 (하위 호환)
        var code = ParameterEmitter.EmitBuildSqlMethod(stmt, "@");

        // 기본 동작: 런타임 가드 삽입 (안전한 기본값)
        Assert.Contains("InvalidOperationException", code);
    }
}
