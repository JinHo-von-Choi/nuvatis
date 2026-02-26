using NuVatis.Statement;
using Xunit;

namespace NuVatis.Tests;

/**
 * MappedStatement 단위 테스트.
 *
 * @author 최진호
 * @date   2026-02-26
 */
public class MappedStatementTests {

    [Fact]
    public void FullId_Format() {
        var stmt = new MappedStatement {
            Id        = "selectById",
            Namespace = "UserMapper",
            Type      = StatementType.Select,
            SqlSource = "SELECT * FROM users WHERE id = @id"
        };
        Assert.Equal("UserMapper.selectById", stmt.FullId);
    }

    [Fact]
    public void Optional_Properties_Default_Null() {
        var stmt = new MappedStatement {
            Id        = "insertUser",
            Namespace = "UserMapper",
            Type      = StatementType.Insert,
            SqlSource = "INSERT INTO users (name) VALUES (@name)"
        };
        Assert.Null(stmt.ResultMapId);
        Assert.Null(stmt.ResultType);
        Assert.Null(stmt.ParameterType);
        Assert.Null(stmt.SelectKey);
        Assert.Null(stmt.CommandTimeout);
        Assert.False(stmt.UseCache);
    }

    [Fact]
    public void All_Properties_Set() {
        var stmt = new MappedStatement {
            Id             = "selectAll",
            Namespace      = "OrderMapper",
            Type           = StatementType.Select,
            SqlSource      = "SELECT * FROM orders",
            ResultMapId    = "OrderResultMap",
            ResultType     = typeof(object),
            ParameterType  = typeof(int),
            CommandTimeout = 30,
            UseCache       = true,
            SelectKey      = new SelectKeyConfig {
                KeyProperty = "Id",
                Sql         = "SELECT last_insert_rowid()",
                Order       = SelectKeyOrder.After
            }
        };
        Assert.Equal("OrderMapper.selectAll", stmt.FullId);
        Assert.Equal("OrderResultMap", stmt.ResultMapId);
        Assert.Equal(typeof(object), stmt.ResultType);
        Assert.Equal(typeof(int), stmt.ParameterType);
        Assert.Equal(30, stmt.CommandTimeout);
        Assert.True(stmt.UseCache);
        Assert.NotNull(stmt.SelectKey);
        Assert.Equal("Id", stmt.SelectKey.KeyProperty);
    }
}
