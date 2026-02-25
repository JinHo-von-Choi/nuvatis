using System.Collections.Immutable;
using NuVatis.Generators.Models;
using NuVatis.Generators.Parsing;
using Xunit;

namespace NuVatis.Generators.Tests;

/**
 * IncludeResolver 단위 테스트.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public class IncludeResolverTests {

    [Fact]
    public void SimpleInclude_ReplacedWithFragment() {
        var fragments = ImmutableArray.Create(
            new ParsedSqlFragment("cols", new TextNode("id, name"))
        );
        var statement = new ParsedStatement(
            "selectUser", "Select", null, "User", null,
            new MixedNode(ImmutableArray.Create<ParsedSqlNode>(
                new TextNode("SELECT "),
                new IncludeNode("cols"),
                new TextNode(" FROM users")
            ))
        );
        var mapper = new ParsedMapper(
            "UserMapper",
            ImmutableArray<ParsedResultMap>.Empty,
            ImmutableArray.Create(statement),
            fragments
        );

        var resolved = IncludeResolver.ResolveIncludes(mapper);

        var root = Assert.IsType<MixedNode>(resolved.Statements[0].RootNode);
        var replaced = Assert.IsType<TextNode>(root.Children[1]);
        Assert.Equal("id, name", replaced.Text);
    }

    [Fact]
    public void NestedInclude_ResolvedRecursively() {
        var fragments = ImmutableArray.Create(
            new ParsedSqlFragment("A", new IncludeNode("B")),
            new ParsedSqlFragment("B", new TextNode("resolved_content"))
        );
        var statement = new ParsedStatement(
            "test", "Select", null, null, null,
            new IncludeNode("A")
        );
        var mapper = new ParsedMapper(
            "TestMapper",
            ImmutableArray<ParsedResultMap>.Empty,
            ImmutableArray.Create(statement),
            fragments
        );

        var resolved = IncludeResolver.ResolveIncludes(mapper);

        var text = Assert.IsType<TextNode>(resolved.Statements[0].RootNode);
        Assert.Equal("resolved_content", text.Text);
    }

    [Fact]
    public void UnresolvedInclude_ReturnsComment() {
        var statement = new ParsedStatement(
            "test", "Select", null, null, null,
            new IncludeNode("missing")
        );
        var mapper = new ParsedMapper(
            "TestMapper",
            ImmutableArray<ParsedResultMap>.Empty,
            ImmutableArray.Create(statement),
            ImmutableArray<ParsedSqlFragment>.Empty
        );

        var resolved = IncludeResolver.ResolveIncludes(mapper);

        var text = Assert.IsType<TextNode>(resolved.Statements[0].RootNode);
        Assert.Equal("/* UNRESOLVED: missing */", text.Text);
    }

    [Fact]
    public void CircularInclude_ReturnsCircularComment() {
        var fragments = ImmutableArray.Create(
            new ParsedSqlFragment("A", new IncludeNode("B")),
            new ParsedSqlFragment("B", new IncludeNode("A"))
        );
        var statement = new ParsedStatement(
            "test", "Select", null, null, null,
            new IncludeNode("A")
        );
        var mapper = new ParsedMapper(
            "TestMapper",
            ImmutableArray<ParsedResultMap>.Empty,
            ImmutableArray.Create(statement),
            fragments
        );

        var resolved = IncludeResolver.ResolveIncludes(mapper);

        var text = Assert.IsType<TextNode>(resolved.Statements[0].RootNode);
        Assert.Equal("/* CIRCULAR: A */", text.Text);
    }
}
