using NuVatis.Sql;
using Xunit;

namespace NuVatis.Tests;

/**
 * SqlIdentifier 단위 테스트.
 * SQL Injection 패턴 거부, 정상 식별자 허용, 팩토리 메서드 검증.
 *
 * @author 최진호
 * @date   2026-02-27
 */
public class SqlIdentifierTests
{
    // --- T2-A: 정상 생성 ---

    [Fact]
    public void From_Valid_String_Returns_Identifier()
    {
        var id = SqlIdentifier.From("users");
        Assert.Equal("users", id.ToString());
    }

    [Fact]
    public void FromEnum_Returns_EnumName_As_Identifier()
    {
        var id = SqlIdentifier.FromEnum(TableName.Users);
        Assert.Equal("Users", id.ToString());
    }

    [Fact]
    public void From_Underscore_And_Dot_Allowed()
    {
        var id = SqlIdentifier.From("schema.table_name");
        Assert.Equal("schema.table_name", id.ToString());
    }

    // --- T2-B: SQL Injection 패턴 거부 ---

    [Theory]
    [InlineData("users; DROP TABLE users--")]
    [InlineData("users'")]
    [InlineData("users\"")]
    [InlineData("/* comment */ users")]
    [InlineData("users -- comment")]
    [InlineData("users UNION SELECT 1")]
    public void From_Injection_Pattern_Throws(string malicious)
    {
        Assert.Throws<ArgumentException>(() => SqlIdentifier.From(malicious));
    }

    [Fact]
    public void From_Empty_String_Throws()
    {
        Assert.Throws<ArgumentException>(() => SqlIdentifier.From(""));
    }

    [Fact]
    public void From_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => SqlIdentifier.From(null!));
    }

    // --- T2-C: AllowedValues 화이트리스트 팩토리 ---

    [Fact]
    public void FromAllowed_Matching_Value_Returns_Identifier()
    {
        var id = SqlIdentifier.FromAllowed("created_at", "id", "created_at", "user_name");
        Assert.Equal("created_at", id.ToString());
    }

    [Fact]
    public void FromAllowed_NonMatching_Value_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            SqlIdentifier.FromAllowed("injected", "id", "created_at"));
    }

    // --- 테스트용 enum ---
    private enum TableName { Users, Orders, Products }
}
