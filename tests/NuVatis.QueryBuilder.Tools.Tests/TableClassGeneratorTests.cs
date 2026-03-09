namespace NuVatis.QueryBuilder.Tools.Tests;

public class TableClassGeneratorTests {

    // ---------------------------------------------------------------------------
    // Helper
    // ---------------------------------------------------------------------------

    private static TableInfo Table(
        string schema,
        string name,
        params (string col, string type, bool nullable)[] cols) {
        var columns = cols
            .Select((c, i) => new ColumnInfo(c.col, c.type, c.nullable, false, i + 1))
            .ToList();
        return new TableInfo(schema, name, columns);
    }

    // ---------------------------------------------------------------------------
    // ToPascalCase
    // ---------------------------------------------------------------------------

    [Fact]
    public void ToPascalCase_SingleWord() {
        var result = TableClassGenerator.ToPascalCase("users");
        Assert.Equal("Users", result);
    }

    [Fact]
    public void ToPascalCase_UnderscoreSeparated() {
        var result = TableClassGenerator.ToPascalCase("user_profile");
        Assert.Equal("UserProfile", result);
    }

    [Fact]
    public void ToPascalCase_MultiUnderscore() {
        var result = TableClassGenerator.ToPascalCase("order_line_item");
        Assert.Equal("OrderLineItem", result);
    }

    // ---------------------------------------------------------------------------
    // Generate — 구조 검증
    // ---------------------------------------------------------------------------

    [Fact]
    public void Generate_ContainsCorrectNamespace() {
        var table  = Table("public", "users", ("id", "int4", false));
        var result = TableClassGenerator.Generate(table, "MyApp.Tables");
        Assert.Contains("namespace MyApp.Tables;", result);
    }

    [Fact]
    public void Generate_ContainsClassName() {
        var table  = Table("public", "users", ("id", "int4", false));
        var result = TableClassGenerator.Generate(table, "MyApp.Tables");
        Assert.Contains("class UsersTable", result);
    }

    [Fact]
    public void Generate_ContainsStaticInstance() {
        var table  = Table("public", "users", ("id", "int4", false));
        var result = TableClassGenerator.Generate(table, "MyApp.Tables");
        Assert.Contains("public static readonly UsersTable Instance", result);
    }

    // ---------------------------------------------------------------------------
    // Generate — PostgreSQL 타입 매핑
    // ---------------------------------------------------------------------------

    [Fact]
    public void Generate_PostgreSql_MapsInt4ToInt() {
        var table  = Table("public", "users", ("id", "int4", false));
        var result = TableClassGenerator.Generate(table, "MyApp.Tables", "postgresql");
        Assert.Contains("FieldNode<int>", result);
    }

    [Fact]
    public void Generate_PostgreSql_NullableColumn_HasQuestionMark() {
        var table  = Table("public", "orders", ("amount", "numeric", true));
        var result = TableClassGenerator.Generate(table, "MyApp.Tables", "postgresql");
        Assert.Contains("FieldNode<decimal?>", result);
    }

    [Fact]
    public void Generate_PostgreSql_UnknownType_FallsBackToString() {
        var table  = Table("public", "items", ("data", "hstore", false));
        var result = TableClassGenerator.Generate(table, "MyApp.Tables", "postgresql");
        Assert.Contains("FieldNode<string>", result);
    }

    // ---------------------------------------------------------------------------
    // Generate — MySQL 타입 매핑
    // ---------------------------------------------------------------------------

    [Fact]
    public void Generate_MySql_UsesMySqlTypeMap() {
        var table  = Table("mydb", "invoices", ("total", "decimal", false));
        var result = TableClassGenerator.Generate(table, "MyApp.Tables", "mysql");
        Assert.Contains("FieldNode<decimal>", result);
    }

    // ---------------------------------------------------------------------------
    // Generate — SafeIdentifier (컬럼명 변환)
    // ---------------------------------------------------------------------------

    [Fact]
    public void Generate_DigitStartColumn_GetsUnderscorePrefix() {
        var table  = Table("public", "stats", ("1stcol", "int4", false));
        var result = TableClassGenerator.Generate(table, "MyApp.Tables");
        // ColumnName.ToUpperInvariant() = "1STCOL" → starts with digit → "_1STCOL"
        Assert.Contains("_1STCOL", result);
    }

    [Fact]
    public void Generate_CSharpKeywordColumn_IsAtEscaped() {
        var table  = Table("public", "events", ("event", "varchar", false));
        var result = TableClassGenerator.Generate(table, "MyApp.Tables");
        // ColumnName.ToUpperInvariant() = "EVENT" → keyword check OrdinalIgnoreCase → "@EVENT"
        Assert.Contains("@EVENT", result);
    }

    // ---------------------------------------------------------------------------
    // GenerateTablesEntry
    // ---------------------------------------------------------------------------

    [Fact]
    public void GenerateTablesEntry_ContainsTableClass() {
        var tables = new List<TableInfo> {
            Table("public", "users", ("id", "int4", false))
        };
        var result = TableClassGenerator.GenerateTablesEntry(tables, "MyApp.Tables");
        Assert.Contains("UsersTable", result);
        Assert.Contains("public static class Tables", result);
    }

    [Fact]
    public void GenerateTablesEntry_MultipleTablesAllPresent() {
        var tables = new List<TableInfo> {
            Table("public", "users",  ("id",    "int4",    false)),
            Table("public", "orders", ("order_id", "int4", false)),
        };
        var result = TableClassGenerator.GenerateTablesEntry(tables, "MyApp.Tables");
        Assert.Contains("UsersTable",  result);
        Assert.Contains("OrdersTable", result);
    }

    [Fact]
    public void GenerateTablesEntry_DuplicateTableNameAcrossSchemas_GetsSchemaPrefixed() {
        // 두 스키마에 동일한 테이블명 "users" — 두 번째는 스키마 접두사 적용
        var tables = new List<TableInfo> {
            Table("public",  "users", ("id", "int4", false)),
            Table("private", "users", ("id", "int4", false)),
        };
        var result = TableClassGenerator.GenerateTablesEntry(tables, "MyApp.Tables");
        // 첫 번째: USERS, 두 번째 충돌 → PRIVATE_USERS
        Assert.Contains("PRIVATE_USERS", result);
    }
}
