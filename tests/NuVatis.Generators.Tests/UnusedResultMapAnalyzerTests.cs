using System.Collections.Immutable;
using NuVatis.Generators.Diagnostics;
using NuVatis.Generators.Models;
using Xunit;

namespace NuVatis.Generators.Tests;

/**
 * UnusedResultMapAnalyzer 단위 테스트.
 * statement에서 참조되지 않는 ResultMap 탐지를 검증한다.
 *
 * @author 최진호
 * @date   2026-02-26
 */
public class UnusedResultMapAnalyzerTests {

    [Fact]
    public void Detects_Unused_ResultMap() {
        var mapper = new ParsedMapper(
            "UserMapper",
            ImmutableArray.Create(
                CreateResultMap("userMap", "MyApp.User"),
                CreateResultMap("orphanMap", "MyApp.Orphan")),
            ImmutableArray.Create(
                new ParsedStatement("selectUser", "Select", "userMap", null, null,
                    new TextNode("SELECT * FROM users"))),
            ImmutableArray<ParsedSqlFragment>.Empty);

        var results = UnusedResultMapAnalyzer.Analyze(mapper);

        Assert.Single(results);
        Assert.Equal("orphanMap", results[0].ResultMapId);
        Assert.Equal("UserMapper", results[0].Namespace);
    }

    [Fact]
    public void Returns_Empty_When_All_ResultMaps_Referenced() {
        var mapper = new ParsedMapper(
            "UserMapper",
            ImmutableArray.Create(
                CreateResultMap("userMap", "MyApp.User")),
            ImmutableArray.Create(
                new ParsedStatement("selectUser", "Select", "userMap", null, null,
                    new TextNode("SELECT * FROM users"))),
            ImmutableArray<ParsedSqlFragment>.Empty);

        var results = UnusedResultMapAnalyzer.Analyze(mapper);

        Assert.Empty(results);
    }

    [Fact]
    public void Returns_Empty_When_No_ResultMaps_Defined() {
        var mapper = new ParsedMapper(
            "UserMapper",
            ImmutableArray<ParsedResultMap>.Empty,
            ImmutableArray.Create(
                new ParsedStatement("selectUser", "Select", null, null, null,
                    new TextNode("SELECT * FROM users"))),
            ImmutableArray<ParsedSqlFragment>.Empty);

        var results = UnusedResultMapAnalyzer.Analyze(mapper);

        Assert.Empty(results);
    }

    [Fact]
    public void Detects_Multiple_Unused_ResultMaps() {
        var mapper = new ParsedMapper(
            "TestMapper",
            ImmutableArray.Create(
                CreateResultMap("mapA", "A"),
                CreateResultMap("mapB", "B"),
                CreateResultMap("mapC", "C")),
            ImmutableArray.Create(
                new ParsedStatement("selectB", "Select", "mapB", null, null,
                    new TextNode("SELECT * FROM b"))),
            ImmutableArray<ParsedSqlFragment>.Empty);

        var results = UnusedResultMapAnalyzer.Analyze(mapper);

        Assert.Equal(2, results.Length);
        Assert.Contains(results, r => r.ResultMapId == "mapA");
        Assert.Contains(results, r => r.ResultMapId == "mapC");
    }

    private static ParsedResultMap CreateResultMap(string id, string type) {
        return new ParsedResultMap(
            id, type, null,
            ImmutableArray.Create(
                new ParsedResultMapping("id", "Id", null, true),
                new ParsedResultMapping("name", "Name", null, false)),
            ImmutableArray<ParsedAssociation>.Empty,
            ImmutableArray<ParsedCollection>.Empty);
    }
}
