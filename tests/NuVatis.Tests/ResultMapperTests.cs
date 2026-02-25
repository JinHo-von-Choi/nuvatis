using Microsoft.Data.Sqlite;
using NuVatis.Mapping;
using Xunit;

namespace NuVatis.Tests;

/**
 * ResultMapper 단위 테스트. SQLite in-memory로 DB 매핑 동작 검증.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public sealed class ResultMapperTests {

    private class TestUser {
        public int    Id   { get; set; }
        public string Name { get; set; } = "";
    }

    private class TestOrder {
        public int      OrderId  { get; set; }
        public string   Product  { get; set; } = "";
        public TestUser Customer { get; set; } = new();
    }

    private readonly ResultMapper _mapper;

    public ResultMapperTests() {
        var resultMaps = new Dictionary<string, ResultMapDefinition> {
            ["UserMap"] = new ResultMapDefinition {
                Id   = "UserMap",
                Type = typeof(TestUser).FullName!,
                Mappings = new List<ResultMapping> {
                    new() { Column = "id",   Property = "Id",   IsId = true },
                    new() { Column = "name", Property = "Name" }
                }
            },
            ["OrderMap"] = new ResultMapDefinition {
                Id   = "OrderMap",
                Type = typeof(TestOrder).FullName!,
                Mappings = new List<ResultMapping> {
                    new() { Column = "order_id", Property = "OrderId", IsId = true },
                    new() { Column = "product",  Property = "Product" }
                },
                Associations = new List<AssociationMapping> {
                    new() { Property = "Customer", ResultMapId = "UserMap", ColumnPrefix = "user_" }
                }
            }
        };
        _mapper = new ResultMapper(resultMaps);
    }

    [Fact]
    public void MapSimpleRow() {
        using var conn = CreateDb();
        Exec(conn, "CREATE TABLE users (id INTEGER, name TEXT)");
        Exec(conn, "INSERT INTO users VALUES (1, 'Alice')");

        using var cmd    = conn.CreateCommand();
        cmd.CommandText  = "SELECT id, name FROM users WHERE id = 1";
        using var reader = cmd.ExecuteReader();

        Assert.True(reader.Read());
        var user = _mapper.MapRow<TestUser>(reader, "UserMap");

        Assert.Equal(1, user.Id);
        Assert.Equal("Alice", user.Name);
    }

    [Fact]
    public void MapWithAssociation() {
        using var conn = CreateDb();
        Exec(conn, "CREATE TABLE orders (order_id INTEGER, product TEXT, user_id INTEGER)");
        Exec(conn, "CREATE TABLE users (id INTEGER, name TEXT)");
        Exec(conn, "INSERT INTO users VALUES (10, 'Bob')");
        Exec(conn, "INSERT INTO orders VALUES (100, 'Laptop', 10)");

        using var cmd   = conn.CreateCommand();
        cmd.CommandText = "SELECT o.order_id, o.product, u.id AS user_id, u.name AS user_name FROM orders o JOIN users u ON o.user_id = u.id";
        using var reader = cmd.ExecuteReader();

        Assert.True(reader.Read());
        var order = _mapper.MapRow<TestOrder>(reader, "OrderMap");

        Assert.Equal(100, order.OrderId);
        Assert.Equal("Laptop", order.Product);
        Assert.NotNull(order.Customer);
        Assert.Equal(10, order.Customer.Id);
        Assert.Equal("Bob", order.Customer.Name);
    }

    [Fact]
    public void MapRowsList() {
        using var conn = CreateDb();
        Exec(conn, "CREATE TABLE users (id INTEGER, name TEXT)");
        Exec(conn, "INSERT INTO users VALUES (1, 'Alice'), (2, 'Bob')");

        using var cmd   = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name FROM users ORDER BY id";
        using var reader = cmd.ExecuteReader();

        var users = _mapper.MapRows<TestUser>(reader, "UserMap");

        Assert.Equal(2, users.Count);
        Assert.Equal("Alice", users[0].Name);
        Assert.Equal("Bob", users[1].Name);
    }

    private static SqliteConnection CreateDb() {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        return conn;
    }

    private static void Exec(SqliteConnection conn, string sql) {
        using var cmd   = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
