using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using NuVatis.Mapping;
using Xunit;

namespace NuVatis.Tests;

/**
 * ResultMapper 단위 테스트.
 * Association, Collection, Nullable, enum 매핑을 검증한다.
 *
 * @author 최진호
 * @date   2026-02-26
 */
public class ResultMapperTests : IDisposable {

    private readonly SqliteConnection _conn;

    public class UserDto {
        public int    Id    { get; set; }
        public string Name  { get; set; } = "";
        public int?   Score { get; set; }
    }

    public class OrderDto {
        public int    OrderId   { get; set; }
        public string Product   { get; set; } = "";
    }

    public class UserWithOrders {
        public int               Id     { get; set; }
        public string            Name   { get; set; } = "";
        public List<OrderDto>    Orders { get; set; } = new();
    }

    public class AddressDto {
        public string City    { get; set; } = "";
        public string Street  { get; set; } = "";
    }

    public class UserWithAddress {
        public int        Id      { get; set; }
        public string     Name    { get; set; } = "";
        public AddressDto Address { get; set; } = null!;
    }

    private readonly Dictionary<string, ResultMapDefinition> _maps;
    private readonly ResultMapper _mapper;

    public ResultMapperTests() {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE users (id INTEGER, name TEXT, score INTEGER);
            INSERT INTO users VALUES (1, 'Alice', 100);
            INSERT INTO users VALUES (2, 'Bob',   NULL);
            CREATE TABLE user_orders (user_id INTEGER, order_id INTEGER, product TEXT, name TEXT);
            INSERT INTO user_orders VALUES (1, 10, 'Widget', 'Alice');
            INSERT INTO user_orders VALUES (1, 11, 'Gadget', 'Alice');
            INSERT INTO user_orders VALUES (2, 20, 'Doohickey', 'Bob');
            CREATE TABLE user_addr (id INTEGER, name TEXT, addr_city TEXT, addr_street TEXT);
            INSERT INTO user_addr VALUES (1, 'Alice', 'Seoul', 'Gangnam');
        """;
        cmd.ExecuteNonQuery();

        _maps = new Dictionary<string, ResultMapDefinition> {
            ["UserMap"] = new() {
                Id   = "UserMap",
                Type = typeof(UserDto).FullName!,
                Mappings = new List<ResultMapping> {
                    new() { Property = "Id",    Column = "id",    IsId = true },
                    new() { Property = "Name",  Column = "name" },
                    new() { Property = "Score", Column = "score" }
                }
            },
            ["OrderMap"] = new() {
                Id   = "OrderMap",
                Type = typeof(OrderDto).FullName!,
                Mappings = new List<ResultMapping> {
                    new() { Property = "OrderId", Column = "order_id", IsId = true },
                    new() { Property = "Product", Column = "product" }
                }
            },
            ["UserOrdersMap"] = new() {
                Id   = "UserOrdersMap",
                Type = typeof(UserWithOrders).FullName!,
                Mappings = new List<ResultMapping> {
                    new() { Property = "Id",   Column = "user_id", IsId = true },
                    new() { Property = "Name", Column = "name" }
                },
                Collections = new List<CollectionMapping> {
                    new() { Property = "Orders", ResultMapId = "OrderMap" }
                }
            },
            ["AddressMap"] = new() {
                Id   = "AddressMap",
                Type = typeof(AddressDto).FullName!,
                Mappings = new List<ResultMapping> {
                    new() { Property = "City",   Column = "city" },
                    new() { Property = "Street", Column = "street" }
                }
            },
            ["UserAddressMap"] = new() {
                Id   = "UserAddressMap",
                Type = typeof(UserWithAddress).FullName!,
                Mappings = new List<ResultMapping> {
                    new() { Property = "Id",   Column = "id", IsId = true },
                    new() { Property = "Name", Column = "name" }
                },
                Associations = new List<AssociationMapping> {
                    new() { Property = "Address", ResultMapId = "AddressMap", ColumnPrefix = "addr_" }
                }
            }
        };

        _mapper = new ResultMapper(_maps);
    }

    [Fact]
    public void MapRow_SimpleType() {
        using var cmd   = _conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, score FROM users WHERE id = 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var user = _mapper.MapRow<UserDto>(reader, "UserMap");
        Assert.Equal(1, user.Id);
        Assert.Equal("Alice", user.Name);
        Assert.Equal(100, user.Score);
    }

    [Fact]
    public void MapRow_NullableProperty() {
        using var cmd   = _conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, score FROM users WHERE id = 2";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var user = _mapper.MapRow<UserDto>(reader, "UserMap");
        Assert.Equal(2, user.Id);
        Assert.Null(user.Score);
    }

    [Fact]
    public void MapRows_SimpleList() {
        using var cmd   = _conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, score FROM users ORDER BY id";
        using var reader = cmd.ExecuteReader();

        var users = _mapper.MapRows<UserDto>(reader, "UserMap");
        Assert.Equal(2, users.Count);
    }

    [Fact]
    public void MapRows_WithCollection() {
        using var cmd   = _conn.CreateCommand();
        cmd.CommandText = "SELECT user_id, name, order_id, product FROM user_orders ORDER BY user_id, order_id";
        using var reader = cmd.ExecuteReader();

        var users = _mapper.MapRows<UserWithOrders>(reader, "UserOrdersMap");
        Assert.Equal(2, users.Count);
        Assert.Equal(2, users[0].Orders.Count);
        Assert.Single(users[1].Orders);
    }

    [Fact]
    public void MapRow_WithAssociation() {
        using var cmd   = _conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, addr_city, addr_street FROM user_addr WHERE id = 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var user = _mapper.MapRow<UserWithAddress>(reader, "UserAddressMap");
        Assert.Equal(1, user.Id);
        Assert.NotNull(user.Address);
        Assert.Equal("Seoul", user.Address.City);
        Assert.Equal("Gangnam", user.Address.Street);
    }

    [Fact]
    public void MapRow_NotFound_ResultMap_Throws() {
        using var cmd   = _conn.CreateCommand();
        cmd.CommandText = "SELECT 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        Assert.Throws<KeyNotFoundException>(() =>
            _mapper.MapRow<UserDto>(reader, "NonExistentMap"));
    }

    [Fact]
    public void MapRows_NotFound_ResultMap_Throws() {
        using var cmd   = _conn.CreateCommand();
        cmd.CommandText = "SELECT 1";
        using var reader = cmd.ExecuteReader();

        Assert.Throws<KeyNotFoundException>(() =>
            _mapper.MapRows<UserDto>(reader, "NonExistentMap"));
    }

    public void Dispose() {
        _conn.Dispose();
    }
}
