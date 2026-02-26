using System.Collections.Generic;
using System.Collections.Immutable;
using NuVatis.Generators.Diagnostics;
using NuVatis.Generators.Models;
using Xunit;

namespace NuVatis.Generators.Tests;

/**
 * StringSubstitutionAnalyzer 단위 테스트.
 * ${} 문자열 치환 탐지가 다양한 SQL 노드 구조에서 정확히 동작하는지,
 * [SqlConstant] 화이트리스트에 의한 NV004 억제가 올바르게 동작하는지 검증한다.
 *
 * @author 최진호
 * @date   2026-02-25
 * @modified 2026-02-26 [SqlConstant] 억제 테스트 추가
 */
public class StringSubstitutionAnalyzerTests {

    [Fact]
    public void Detects_StringSubstitution_In_Simple_Statement() {
        var mapper = BuildMapper("TestNs", "selectById",
            new MixedNode(ImmutableArray.Create<ParsedSqlNode>(
                new TextNode("SELECT * FROM users WHERE name = "),
                new ParameterNode("name", IsStringSubstitution: true)
            )));

        var results = StringSubstitutionAnalyzer.Analyze(mapper);

        Assert.Single(results);
        Assert.Equal("name", results[0].ParameterName);
        Assert.Equal("TestNs", results[0].Namespace);
        Assert.Equal("selectById", results[0].StatementId);
    }

    [Fact]
    public void Ignores_ParameterBinding_With_Hash() {
        var mapper = BuildMapper("TestNs", "selectById",
            new MixedNode(ImmutableArray.Create<ParsedSqlNode>(
                new TextNode("SELECT * FROM users WHERE id = "),
                new ParameterNode("id", IsStringSubstitution: false)
            )));

        var results = StringSubstitutionAnalyzer.Analyze(mapper);

        Assert.Empty(results);
    }

    [Fact]
    public void Detects_StringSubstitution_Inside_IfNode() {
        var mapper = BuildMapper("TestNs", "search",
            new IfNode("tableName != null", ImmutableArray.Create<ParsedSqlNode>(
                new TextNode("SELECT * FROM "),
                new ParameterNode("tableName", IsStringSubstitution: true)
            )));

        var results = StringSubstitutionAnalyzer.Analyze(mapper);

        Assert.Single(results);
        Assert.Equal("tableName", results[0].ParameterName);
    }

    [Fact]
    public void Detects_StringSubstitution_Inside_ForEachNode() {
        var mapper = BuildMapper("TestNs", "dynamicQuery",
            new ForEachNode(
                Collection: "columns",
                Item:       "col",
                Open:       null,
                Close:      null,
                Separator:  ", ",
                Children: ImmutableArray.Create<ParsedSqlNode>(
                    new ParameterNode("col", IsStringSubstitution: true)
                )));

        var results = StringSubstitutionAnalyzer.Analyze(mapper);

        Assert.Single(results);
        Assert.Equal("col", results[0].ParameterName);
    }

    [Fact]
    public void Detects_StringSubstitution_Inside_ChooseNode() {
        var mapper = BuildMapper("TestNs", "conditionalQuery",
            new ChooseNode(
                Whens: ImmutableArray.Create(
                    new WhenClause("sortColumn != null", ImmutableArray.Create<ParsedSqlNode>(
                        new TextNode("ORDER BY "),
                        new ParameterNode("sortColumn", IsStringSubstitution: true)
                    ))
                ),
                Otherwise: ImmutableArray.Create<ParsedSqlNode>(
                    new TextNode("ORDER BY "),
                    new ParameterNode("defaultSort", IsStringSubstitution: true)
                )));

        var results = StringSubstitutionAnalyzer.Analyze(mapper);

        Assert.Equal(2, results.Length);
        Assert.Equal("sortColumn", results[0].ParameterName);
        Assert.Equal("defaultSort", results[1].ParameterName);
    }

    [Fact]
    public void Detects_Multiple_Usages_Across_Statements() {
        var statements = ImmutableArray.Create(
            new ParsedStatement("stmt1", "Select", null, null, null,
                new ParameterNode("col1", IsStringSubstitution: true)),
            new ParsedStatement("stmt2", "Select", null, null, null,
                new MixedNode(ImmutableArray.Create<ParsedSqlNode>(
                    new ParameterNode("safe", IsStringSubstitution: false),
                    new ParameterNode("col2", IsStringSubstitution: true)
                ))));

        var mapper = new ParsedMapper("MultiNs",
            ImmutableArray<ParsedResultMap>.Empty,
            statements,
            ImmutableArray<ParsedSqlFragment>.Empty);

        var results = StringSubstitutionAnalyzer.Analyze(mapper);

        Assert.Equal(2, results.Length);
        Assert.Equal("stmt1", results[0].StatementId);
        Assert.Equal("stmt2", results[1].StatementId);
    }

    [Fact]
    public void Detects_Inside_WhereNode() {
        var mapper = BuildMapper("TestNs", "dynamicWhere",
            new WhereNode(ImmutableArray.Create<ParsedSqlNode>(
                new ParameterNode("columnName", IsStringSubstitution: true),
                new TextNode(" = "),
                new ParameterNode("value", IsStringSubstitution: false)
            )));

        var results = StringSubstitutionAnalyzer.Analyze(mapper);

        Assert.Single(results);
        Assert.Equal("columnName", results[0].ParameterName);
    }

    [Fact]
    public void Detects_Inside_SetNode() {
        var mapper = BuildMapper("TestNs", "dynamicUpdate",
            new SetNode(ImmutableArray.Create<ParsedSqlNode>(
                new ParameterNode("columnExpr", IsStringSubstitution: true)
            )));

        var results = StringSubstitutionAnalyzer.Analyze(mapper);

        Assert.Single(results);
        Assert.Equal("columnExpr", results[0].ParameterName);
    }

    [Fact]
    public void Returns_Empty_For_No_Substitution() {
        var mapper = BuildMapper("SafeNs", "safeQuery",
            new MixedNode(ImmutableArray.Create<ParsedSqlNode>(
                new TextNode("SELECT * FROM users WHERE id = "),
                new ParameterNode("id", IsStringSubstitution: false),
                new TextNode(" AND name = "),
                new ParameterNode("name", IsStringSubstitution: false)
            )));

        var results = StringSubstitutionAnalyzer.Analyze(mapper);

        Assert.Empty(results);
    }

    [Fact]
    public void SqlConstant_Suppresses_Warning() {
        var mapper = BuildMapper("TestNs", "dynamicOrder",
            new MixedNode(ImmutableArray.Create<ParsedSqlNode>(
                new TextNode("SELECT * FROM users ORDER BY "),
                new ParameterNode("OrderColumn", IsStringSubstitution: true)
            )));

        var sqlConstants = new HashSet<string> { "OrderColumn", "orderColumn" };
        var results      = StringSubstitutionAnalyzer.Analyze(mapper, sqlConstants);

        Assert.Empty(results);
    }

    [Fact]
    public void NonSqlConstant_Still_Triggers_Warning() {
        var mapper = BuildMapper("TestNs", "dynamicOrder",
            new MixedNode(ImmutableArray.Create<ParsedSqlNode>(
                new TextNode("SELECT * FROM users ORDER BY "),
                new ParameterNode("userInput", IsStringSubstitution: true)
            )));

        var sqlConstants = new HashSet<string> { "OrderColumn" };
        var results      = StringSubstitutionAnalyzer.Analyze(mapper, sqlConstants);

        Assert.Single(results);
        Assert.Equal("userInput", results[0].ParameterName);
    }

    [Fact]
    public void MixedSqlConstant_And_NonConstant_Reports_Only_NonConstant() {
        var mapper = BuildMapper("TestNs", "mixed",
            new MixedNode(ImmutableArray.Create<ParsedSqlNode>(
                new TextNode("SELECT * FROM "),
                new ParameterNode("tableName", IsStringSubstitution: true),
                new TextNode(" ORDER BY "),
                new ParameterNode("sortColumn", IsStringSubstitution: true)
            )));

        var sqlConstants = new HashSet<string> { "sortColumn" };
        var results      = StringSubstitutionAnalyzer.Analyze(mapper, sqlConstants);

        Assert.Single(results);
        Assert.Equal("tableName", results[0].ParameterName);
    }

    private static ParsedMapper BuildMapper(string ns, string statementId, ParsedSqlNode rootNode) {
        return new ParsedMapper(
            ns,
            ImmutableArray<ParsedResultMap>.Empty,
            ImmutableArray.Create(new ParsedStatement(statementId, "Select", null, null, null, rootNode)),
            ImmutableArray<ParsedSqlFragment>.Empty);
    }
}
