namespace NuVatis.QueryBuilder.Tests.Execution;

using NuVatis.QueryBuilder.Execution;
using NuVatis.QueryBuilder.Tests.Helpers;

public class RecordMapperTests {
    private sealed class UserDto {
        public int    Id       { get; set; }
        public string Name     { get; set; } = "";
        public bool   IsActive { get; set; }
    }

    [Fact]
    public void MapRow_SnakeToPascal_MapsCorrectly() {
        var reader = new FakeDataReader(
            columns: ["id", "name", "is_active"],
            row:     [42, "Alice", true]);

        var result = RecordMapper.MapRow<UserDto>(reader);

        Assert.Equal(42,      result.Id);
        Assert.Equal("Alice", result.Name);
        Assert.True(result.IsActive);
    }

    [Fact]
    public void MapRow_ScalarInt_ReturnsPrimitive() {
        var reader = new FakeDataReader(
            columns: ["count"],
            row:     [7]);

        var result = RecordMapper.MapRow<int>(reader);
        Assert.Equal(7, result);
    }

    [Fact]
    public void MapRow_ScalarString_ReturnsPrimitive() {
        var reader = new FakeDataReader(
            columns: ["name"],
            row:     ["Alice"]);

        var result = RecordMapper.MapRow<string>(reader);
        Assert.Equal("Alice", result);
    }

    [Fact]
    public void MapRow_NullColumn_SkipsProperty() {
        var reader = new FakeDataReader(
            columns: ["id", "name"],
            row:     [1, null]);

        var result = RecordMapper.MapRow<UserDto>(reader);

        Assert.Equal(1,  result.Id);
        Assert.Equal("", result.Name); // default 유지
    }

    [Fact]
    public void MapRow_NullableScalarInt_ReturnsValue() {
        var reader = new FakeDataReader(["count"], [5]);
        var result = RecordMapper.MapRow<int?>(reader);
        Assert.Equal(5, result);
    }

    [Fact]
    public void MapRow_NullableScalarNull_ReturnsNull() {
        var reader = new FakeDataReader(["count"], [null]);
        var result = RecordMapper.MapRow<int?>(reader);
        Assert.Null(result);
    }
}
