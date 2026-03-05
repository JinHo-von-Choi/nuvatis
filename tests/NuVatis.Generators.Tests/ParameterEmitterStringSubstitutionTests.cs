using System.Collections.Generic;
using System.Collections.Immutable;
using NuVatis.Generators.Emitters;
using NuVatis.Generators.Models;
using Xunit;

namespace NuVatis.Generators.Tests;

/**
 * ParameterEmitter.EmitBuildSqlMethod — ${} 치환 코드 생성 경로 단위 테스트.
 * 모든 노드는 positional record 생성자를 사용한다.
 *
 * @author 최진호
 * @date   2026-03-05
 */
public class ParameterEmitterStringSubstitutionTests {

    // ParsedStatement(Id, StatementType, ResultMapId?, ResultType?, ParameterType?, RootNode, Timeout?)
    private static ParsedStatement MakeStatement(string id, bool isStringSubstitution) {
        var paramNode = new ParameterNode("col", isStringSubstitution);
        var rootNode  = new MixedNode(ImmutableArray.Create<ParsedSqlNode>(
            new TextNode("SELECT * FROM "),
            paramNode));
        return new ParsedStatement(id, "Select", null, null, null, rootNode);
    }

    [Fact]
    public void EmitBuildSqlMethod_WithoutParamTypeMap_GeneratesRuntimeGuard() {
        var stmt = MakeStatement("GetDynamic", isStringSubstitution: true);

        var code = ParameterEmitter.EmitBuildSqlMethod(stmt, "@", paramTypeMap: null);

        Assert.Contains("is not NuVatis.Core.Sql.SqlIdentifier", code);
        Assert.Contains("throw new System.InvalidOperationException", code);
        Assert.DoesNotContain("?.ToString()", code);
    }

    [Fact]
    public void EmitBuildSqlMethod_WithSqlIdentifierInParamTypeMap_GeneratesDirectToString() {
        var stmt = MakeStatement("GetTable", isStringSubstitution: true);
        var typeMap = new Dictionary<string, string> {
            ["col"] = "NuVatis.Core.Sql.SqlIdentifier"
        };

        var code = ParameterEmitter.EmitBuildSqlMethod(stmt, "@", paramTypeMap: typeMap);

        Assert.Contains("?.ToString()", code);
        Assert.DoesNotContain("is not NuVatis.Core.Sql.SqlIdentifier", code);
    }

    [Fact]
    public void EmitBuildSqlMethod_WithSimilarButNotSqlIdentifierFqn_GeneratesRuntimeGuard() {
        var stmt = MakeStatement("GetTable", isStringSubstitution: true);
        var typeMap = new Dictionary<string, string> {
            ["col"] = "MyApp.SqlIdentifier"
        };

        var code = ParameterEmitter.EmitBuildSqlMethod(stmt, "@", paramTypeMap: typeMap);

        Assert.Contains("is not NuVatis.Core.Sql.SqlIdentifier", code);
    }

    [Fact]
    public void EmitBuildSqlMethod_RegularParameter_GeneratesDbParameter() {
        var stmt = MakeStatement("GetById", isStringSubstitution: false);

        var code = ParameterEmitter.EmitBuildSqlMethod(stmt, "@", paramTypeMap: null);

        Assert.Contains("dbFactory.CreateParameter()", code);
        Assert.DoesNotContain("is not NuVatis.Core.Sql.SqlIdentifier", code);
    }
}
