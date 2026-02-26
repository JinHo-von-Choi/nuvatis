using System.Collections.Immutable;
using NuVatis.Generators.Emitters;
using NuVatis.Generators.Models;
using Xunit;

namespace NuVatis.Generators.Tests;

/**
 * DynamicSqlEmitter 단위 테스트.
 * MyBatis 표현식 → C# 변환, 동적 SQL 노드 → C# 코드 생성을 검증한다.
 *
 * @author 최진호
 * @date   2026-02-26
 */
public class DynamicSqlEmitterTests {

    [Theory]
    [InlineData("name != null", "p.Name != null")]
    [InlineData("age > 18", "p.Age > 18")]
    [InlineData("name != null and age > 0", "p.Name != null && p.Age > 0")]
    [InlineData("a == 1 or b == 2", "p.A == 1 || p.B == 2")]
    [InlineData("type == 'admin'", "p.Type == \"admin\"")]
    public void ConvertTestExpression_ConvertsCorrectly(string input, string expected) {
        var result = DynamicSqlEmitter.ConvertTestExpression(input, "p");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void HasDynamicNodes_DetectsIfNode() {
        var node = new IfNode("test", ImmutableArray.Create<ParsedSqlNode>(new TextNode("sql")));
        Assert.True(DynamicSqlEmitter.HasDynamicNodes(node));
    }

    [Fact]
    public void HasDynamicNodes_DetectsNestedInMixed() {
        var node = new MixedNode(ImmutableArray.Create<ParsedSqlNode>(
            new TextNode("SELECT * FROM t"),
            new IfNode("test", ImmutableArray.Create<ParsedSqlNode>(new TextNode("AND x")))
        ));
        Assert.True(DynamicSqlEmitter.HasDynamicNodes(node));
    }

    [Fact]
    public void HasDynamicNodes_ReturnsFalseForStatic() {
        var node = new MixedNode(ImmutableArray.Create<ParsedSqlNode>(
            new TextNode("SELECT * FROM t"),
            new ParameterNode("id", false)
        ));
        Assert.False(DynamicSqlEmitter.HasDynamicNodes(node));
    }

    [Fact]
    public void EmitSqlBuilder_SimpleText() {
        var node = new TextNode("SELECT * FROM users");
        var code = DynamicSqlEmitter.EmitSqlBuilder(node, "param", "T");

        Assert.Contains("__sql.Append(\"SELECT * FROM users\")", code);
    }

    [Fact]
    public void EmitSqlBuilder_IfNode_GeneratesConditional() {
        var node = new MixedNode(ImmutableArray.Create<ParsedSqlNode>(
            new TextNode("SELECT * FROM users WHERE 1=1"),
            new IfNode("name != null", ImmutableArray.Create<ParsedSqlNode>(
                new TextNode(" AND name = "),
                new ParameterNode("name", false)
            ))
        ));

        var code = DynamicSqlEmitter.EmitSqlBuilder(node, "param", "T");

        Assert.Contains("if (param.Name != null)", code);
        Assert.Contains("__sql.Append(\" AND name = \")", code);
        Assert.Contains("__sql.Append(\"#{name}\")", code);
    }

    [Fact]
    public void EmitSqlBuilder_ChooseNode_GeneratesIfElse() {
        var node = new ChooseNode(
            ImmutableArray.Create(
                new WhenClause("type == 'a'", ImmutableArray.Create<ParsedSqlNode>(
                    new TextNode("type_a"))),
                new WhenClause("type == 'b'", ImmutableArray.Create<ParsedSqlNode>(
                    new TextNode("type_b")))
            ),
            ImmutableArray.Create<ParsedSqlNode>(new TextNode("type_default"))
        );

        var code = DynamicSqlEmitter.EmitSqlBuilder(node, "p", "T");

        Assert.Contains("if (p.Type == \"a\")", code);
        Assert.Contains("else if (p.Type == \"b\")", code);
        Assert.Contains("else", code);
        Assert.Contains("type_default", code);
    }

    [Fact]
    public void EmitSqlBuilder_ForEachNode_GeneratesLoop() {
        var node = new ForEachNode(
            Collection: "ids",
            Item: "id",
            Open: "(",
            Close: ")",
            Separator: ", ",
            Children: ImmutableArray.Create<ParsedSqlNode>(
                new ParameterNode("id", false)
            )
        );

        var code = DynamicSqlEmitter.EmitSqlBuilder(node, "param", "T");

        Assert.Contains("foreach (var id in param.Ids)", code);
        Assert.Contains("__sql.Append(\"(\")", code);
        Assert.Contains("__sql.Append(\")\")", code);
        Assert.Contains("__sql.Append(\", \")", code);
    }

    [Fact]
    public void EmitSqlBuilder_WhereNode_TrimsLeadingAnd() {
        var node = new WhereNode(ImmutableArray.Create<ParsedSqlNode>(
            new IfNode("name != null", ImmutableArray.Create<ParsedSqlNode>(
                new TextNode("AND name = 'test'")
            ))
        ));

        var code = DynamicSqlEmitter.EmitSqlBuilder(node, "p", "T");

        Assert.Contains("WHERE", code);
        Assert.Contains("TrimStart", code);
        Assert.Contains("StartsWith(\"AND \"", code);
    }

    [Fact]
    public void EmitSqlBuilder_ParameterNode_StringSubstitution() {
        var node = new ParameterNode("tableName", IsStringSubstitution: true);
        var code = DynamicSqlEmitter.EmitSqlBuilder(node, "p", "T");

        Assert.Contains("p.TableName", code);
        Assert.DoesNotContain("#{", code);
    }

    [Fact]
    public void EmitSqlBuilder_ParameterNode_ParameterBinding() {
        var node = new ParameterNode("userId", IsStringSubstitution: false);
        var code = DynamicSqlEmitter.EmitSqlBuilder(node, "p", "T");

        Assert.Contains("#{userId}", code);
    }

    [Fact]
    public void EmitSqlBuilder_BindNode_GeneratesLocalVariable() {
        var node = new MixedNode(ImmutableArray.Create<ParsedSqlNode>(
            new BindNode("pattern", "'%' + name + '%'"),
            new TextNode("SELECT * FROM users WHERE name LIKE "),
            new ParameterNode("pattern", false)
        ));

        var code = DynamicSqlEmitter.EmitSqlBuilder(node, "p", "T");

        Assert.Contains("var pattern =", code);
        Assert.Contains("p.Name", code);
        Assert.Contains("#{pattern}", code);
    }

    [Fact]
    public void HasDynamicNodes_DetectsForEachNode() {
        var node = new ForEachNode("ids", "id", "(", ")", ",",
            ImmutableArray.Create<ParsedSqlNode>(new ParameterNode("id", false)));
        Assert.True(DynamicSqlEmitter.HasDynamicNodes(node));
    }
}
