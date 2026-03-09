using NuVatis.MySql;
using NuVatis.Oracle;
using NuVatis.PostgreSql;
using NuVatis.SqlServer;
using NuVatis.Sqlite;
using NuVatis.Provider;
using Xunit;

namespace NuVatis.Tests;

/**
 * 모든 IDbProvider 구현체의 단위 테스트.
 *
 * @author 최진호
 * @date   2026-02-26
 */
public class ProviderTests {

    [Fact]
    public void SqliteProvider_Properties() {
        var p = new SqliteProvider();
        Assert.Equal("Sqlite", p.Name);
        Assert.Equal("@", p.ParameterPrefix);
        Assert.Equal("@p0", p.GetParameterName(0));
        Assert.Equal("@p5", p.GetParameterName(5));
        Assert.Equal("\"Users\"", p.WrapIdentifier("Users"));
        using var conn = p.CreateConnection("Data Source=:memory:");
        Assert.NotNull(conn);
    }

    [Fact]
    public void PostgreSqlProvider_Properties() {
        var p = new PostgreSqlProvider();
        Assert.Equal("PostgreSql", p.Name);
        Assert.Equal("@", p.ParameterPrefix);
        Assert.Equal("@p0", p.GetParameterName(0));
        Assert.Equal("\"Users\"", p.WrapIdentifier("Users"));
        using var conn = p.CreateConnection("Host=localhost");
        Assert.NotNull(conn);
    }

    [Fact]
    public void MySqlProvider_Properties() {
        var p = new MySqlProvider();
        Assert.Equal("MySql", p.Name);
        Assert.Equal("@", p.ParameterPrefix);
        Assert.Equal("@p0", p.GetParameterName(0));
        Assert.Equal("`Users`", p.WrapIdentifier("Users"));
        using var conn = p.CreateConnection("Server=localhost");
        Assert.NotNull(conn);
    }

    [Fact]
    public void SqlServerProvider_Properties() {
        var p = new SqlServerProvider();
        Assert.Equal("SqlServer", p.Name);
        Assert.Equal("@", p.ParameterPrefix);
        Assert.Equal("@p0", p.GetParameterName(0));
        Assert.Equal("[Users]", p.WrapIdentifier("Users"));
    }

    [Fact]
    public void SqlServerProvider_CreateConnection_ReturnsNonNull() {
        var p    = new SqlServerProvider();
        using var conn = p.CreateConnection("Server=localhost;Database=test;User Id=sa;Password=test;TrustServerCertificate=True;");
        Assert.NotNull(conn);
        // 연결을 열지 않으므로 실제 SQL Server 불필요
    }

    [Fact]
    public void OracleProvider_Properties() {
        var p = new OracleProvider();
        Assert.Equal("Oracle", p.Name);
        Assert.Equal(":", p.ParameterPrefix);
        Assert.Equal(":p0", p.GetParameterName(0));
        Assert.Equal(":p5", p.GetParameterName(5));
        Assert.Equal("\"Users\"", p.WrapIdentifier("Users"));
    }

    [Fact]
    public void OracleProvider_CreateConnection_ReturnsNonNull() {
        var p    = new OracleProvider();
        using var conn = p.CreateConnection("Data Source=localhost/ORCL;User Id=test;Password=test;");
        Assert.NotNull(conn);
        // 연결을 열지 않으므로 실제 Oracle 인스턴스 불필요
    }

    [Fact]
    public void NuVatisProviderAttribute_Stores_Name() {
        var attr = new NuVatisProviderAttribute("TestDb");
        Assert.Equal("TestDb", attr.ProviderName);
    }
}
