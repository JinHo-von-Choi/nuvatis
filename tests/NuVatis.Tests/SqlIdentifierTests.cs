using NuVatis.Core.Sql;
using System.Globalization;
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

    [Theory]
    [InlineData("users union")]            // trailing keyword, no space after
    [InlineData("union select")]           // keyword at start
    [InlineData("select * from users")]    // select keyword
    [InlineData("users--comment")]         // comment sequence, no space
    public void From_Injection_Pattern_Without_Surrounding_Spaces_Throws(string malicious)
    {
        Assert.Throws<ArgumentException>(() => SqlIdentifier.From(malicious));
    }

    [Fact]
    public void FromEnum_FlagsEnum_Combination_Throws()
    {
        // Flags enum combinations like "Read, Write" should throw
        Assert.Throws<ArgumentException>(() => SqlIdentifier.FromEnum(FlagsPermission.Read | FlagsPermission.Write));
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

    // --- T2-B2: dot-qualified 식별자 false positive 방지 ---

    [Theory]
    [InlineData("schema.or_table")]      // 'or' is table name suffix, not keyword
    [InlineData("db.and_condition")]     // 'and' is column name prefix
    [InlineData("schema.select_result")] // 'select' is part of identifier after dot
    public void From_DotQualified_IdentifierWithKeywordSuffix_Allowed(string identifier)
    {
        var id = SqlIdentifier.From(identifier);
        Assert.Equal(identifier, id.ToString());
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

    // --- T2-D: JoinTyped WHERE IN 절 리터럴 생성 ---

    [Fact]
    public void JoinTyped_WithInts_ReturnsCommaSeparated()
    {
        var result = SqlIdentifier.JoinTyped(new List<int> { 1, 2, 3 });
        Assert.Equal("1,2,3", result);
    }

    [Fact]
    public void JoinTyped_WithGuids_ReturnsQuotedCommaSeparated()
    {
        var ids = new List<Guid>
        {
            new("00000000-0000-0000-0000-000000000001"),
            new("00000000-0000-0000-0000-000000000002"),
        };
        var result = SqlIdentifier.JoinTyped(ids);
        Assert.Equal(
            "'00000000-0000-0000-0000-000000000001'," +
            "'00000000-0000-0000-0000-000000000002'",
            result);
    }

    [Fact]
    public void JoinTyped_WithEmptyCollection_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => SqlIdentifier.JoinTyped(new List<int>()));
        Assert.Contains("비어 있", ex.Message);
    }

    [Fact]
    public void JoinTyped_IsUsableInQueryTemplate()
    {
        var ids = new List<int> { 10, 20, 30 };
        var inClause = SqlIdentifier.JoinTyped(ids);
        var sql = $"SELECT * FROM orders WHERE id IN ({inClause})";
        Assert.Equal("SELECT * FROM orders WHERE id IN (10,20,30)", sql);
    }

    [Fact]
    public void JoinTyped_WithSingleItem_ReturnsNoComma()
    {
        var result = SqlIdentifier.JoinTyped(new List<long> { 42L });
        Assert.Equal("42", result);
    }

    [Fact]
    public void JoinTyped_Doubles_Use_InvariantCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var result = SqlIdentifier.JoinTyped(new List<double> { 1.5, 2.25 });
            Assert.Equal("1.5,2.25", result);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void JoinTyped_Decimals_Use_InvariantCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var result = SqlIdentifier.JoinTyped(new List<decimal> { 10.75m });
            Assert.Equal("10.75", result);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void JoinTyped_DateTime_Uses_Invariant_Fixed_Format()
    {
        var dt     = new DateTime(2026, 3, 1, 9, 30, 0);
        var result = SqlIdentifier.JoinTyped(new[] { dt });
        Assert.Equal("'2026-03-01 09:30:00.0000000'", result);
    }

    [Fact]
    public void JoinTyped_DateOnly_Uses_Invariant_Fixed_Format()
    {
        var d      = new DateOnly(2026, 3, 1);
        var result = SqlIdentifier.JoinTyped(new[] { d });
        Assert.Equal("'2026-03-01'", result);
    }

    [Fact]
    public void JoinTyped_DateTimeOffset_Uses_Invariant_Fixed_Format()
    {
        var o      = new DateTimeOffset(2026, 3, 1, 9, 30, 0, TimeSpan.FromHours(9));
        var result = SqlIdentifier.JoinTyped(new[] { o });
        Assert.Equal("'2026-03-01 09:30:00.0000000 +09:00'", result);
    }

    [Fact]
    public void JoinTyped_TimeOnly_Uses_Invariant_Fixed_Format()
    {
        var t      = new TimeOnly(9, 30, 0);
        var result = SqlIdentifier.JoinTyped(new[] { t });
        Assert.Equal("'09:30:00.0000000'", result);
    }

    [Fact]
    public void JoinTyped_Null_Collection_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => SqlIdentifier.JoinTyped<int>(null!));
    }

    [Fact]
    public void JoinTyped_Enum_Inlines_Underlying_Value()
    {
        var result = SqlIdentifier.JoinTyped(new[] { TableName.Users, TableName.Products });
        Assert.Equal("0,2", result);
    }

    [Fact]
    public void JoinTyped_UserDefinedStruct_Throws()
    {
        var values = new[] { new ArbitraryStruct() };
        Assert.Throws<ArgumentException>(() => SqlIdentifier.JoinTyped(values));
    }

    [Fact]
    public void JoinTyped_Bool_Throws()
    {
        Assert.Throws<ArgumentException>(() => SqlIdentifier.JoinTyped(new[] { true }));
    }

    [Fact]
    public void JoinTyped_Char_Throws()
    {
        Assert.Throws<ArgumentException>(() => SqlIdentifier.JoinTyped(new[] { 'a' }));
    }

    private struct ArbitraryStruct
    {
        public override string ToString() => "임의 문자열";
    }

    // --- 테스트용 enum ---
    private enum TableName { Users, Orders, Products }

    [System.Flags]
    private enum FlagsPermission { Read = 1, Write = 2, Execute = 4 }
}
