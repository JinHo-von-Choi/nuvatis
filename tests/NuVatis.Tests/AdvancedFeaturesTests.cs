using NuVatis.Mapping;
using NuVatis.Statement;

namespace NuVatis.Tests;

/**
 * 고급 기능 모델 테스트.
 * SelectKeyConfig, DiscriminatorMapping, MappedStatement.SelectKey 검증.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public class AdvancedFeaturesTests {

    [Fact]
    public void SelectKeyConfig_BeforeOrder_PropertiesSet() {
        var config = new SelectKeyConfig {
            KeyProperty = "Id",
            Sql         = "SELECT nextval('user_seq')",
            Order       = SelectKeyOrder.Before,
            ResultType  = "long"
        };

        Assert.Equal("Id", config.KeyProperty);
        Assert.Equal(SelectKeyOrder.Before, config.Order);
        Assert.Equal("long", config.ResultType);
    }

    [Fact]
    public void SelectKeyConfig_AfterOrder_DefaultResultType() {
        var config = new SelectKeyConfig {
            KeyProperty = "Id",
            Sql         = "SELECT LAST_INSERT_ROWID()",
            Order       = SelectKeyOrder.After
        };

        Assert.Equal(SelectKeyOrder.After, config.Order);
        Assert.Null(config.ResultType);
    }

    [Fact]
    public void MappedStatement_WithSelectKey() {
        var statement = new MappedStatement {
            Id        = "insertUser",
            Namespace = "UserMapper",
            Type      = StatementType.Insert,
            SqlSource = "INSERT INTO users (name) VALUES (#{Name})",
            SelectKey = new SelectKeyConfig {
                KeyProperty = "Id",
                Sql         = "SELECT LAST_INSERT_ROWID()",
                Order       = SelectKeyOrder.After
            }
        };

        Assert.NotNull(statement.SelectKey);
        Assert.Equal("Id", statement.SelectKey.KeyProperty);
        Assert.Equal(SelectKeyOrder.After, statement.SelectKey.Order);
    }

    [Fact]
    public void MappedStatement_WithoutSelectKey_IsNull() {
        var statement = new MappedStatement {
            Id        = "selectUser",
            Namespace = "UserMapper",
            Type      = StatementType.Select,
            SqlSource = "SELECT * FROM users"
        };

        Assert.Null(statement.SelectKey);
    }

    [Fact]
    public void DiscriminatorMapping_CasesMapping() {
        var discriminator = new DiscriminatorMapping {
            Column   = "type",
            JdbcType = "VARCHAR",
            Cases    = new Dictionary<string, string> {
                ["admin"]  = "AdminResultMap",
                ["user"]   = "UserResultMap",
                ["guest"]  = "GuestResultMap"
            }
        };

        Assert.Equal("type", discriminator.Column);
        Assert.Equal("VARCHAR", discriminator.JdbcType);
        Assert.Equal(3, discriminator.Cases.Count);
        Assert.Equal("AdminResultMap", discriminator.Cases["admin"]);
    }

    [Fact]
    public void DiscriminatorMapping_EmptyCases() {
        var discriminator = new DiscriminatorMapping {
            Column   = "status",
            JdbcType = "INTEGER"
        };

        Assert.Empty(discriminator.Cases);
    }

    [Fact]
    public void ResultMapDefinition_WithDiscriminator() {
        var resultMap = new ResultMapDefinition {
            Id   = "vehicleMap",
            Type = "Vehicle",
            Discriminator = new DiscriminatorMapping {
                Column   = "vehicle_type",
                JdbcType = "VARCHAR",
                Cases    = new Dictionary<string, string> {
                    ["car"]   = "carResultMap",
                    ["truck"] = "truckResultMap"
                }
            }
        };

        Assert.NotNull(resultMap.Discriminator);
        Assert.Equal("vehicle_type", resultMap.Discriminator.Column);
        Assert.Equal(2, resultMap.Discriminator.Cases.Count);
    }

    [Fact]
    public void ResultMapDefinition_WithoutDiscriminator_IsNull() {
        var resultMap = new ResultMapDefinition {
            Id   = "simpleMap",
            Type = "User"
        };

        Assert.Null(resultMap.Discriminator);
    }
}
