using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using NuVatis.Mapping.TypeHandlers;
using Xunit;

namespace NuVatis.Tests;

/**
 * TypeHandler 구현체 단위 테스트.
 *
 * @author 최진호
 * @date   2026-02-26
 */
public class TypeHandlerTests : IDisposable {

    private readonly SqliteConnection _conn;

    public TypeHandlerTests() {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
    }

    public void Dispose() {
        _conn.Dispose();
    }

#if NET8_0_OR_GREATER
    [Fact]
    public void DateOnlyTypeHandler_GetValue() {
        var handler = new DateOnlyTypeHandler();
        Assert.Equal(typeof(DateOnly), handler.TargetType);

        var dt  = new DateTime(2026, 2, 26, 0, 0, 0, DateTimeKind.Utc);
        var cmd = _conn.CreateCommand();
        cmd.CommandText = $"SELECT '{dt:yyyy-MM-dd}'";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var result = handler.GetValue(reader, 0);
        Assert.IsType<DateOnly>(result);
    }

    [Fact]
    public void DateOnlyTypeHandler_SetParameter_WithValue() {
        var handler = new DateOnlyTypeHandler();
        using var cmd    = _conn.CreateCommand();
        var param        = cmd.CreateParameter();
        handler.SetParameter(param, new DateOnly(2026, 2, 26));
        Assert.IsType<DateTime>(param.Value);
    }

    [Fact]
    public void DateOnlyTypeHandler_SetParameter_Null() {
        var handler = new DateOnlyTypeHandler();
        using var cmd    = _conn.CreateCommand();
        var param        = cmd.CreateParameter();
        handler.SetParameter(param, null);
        Assert.Equal(DBNull.Value, param.Value);
    }

    [Fact]
    public void TimeOnlyTypeHandler_GetValue_FromTimeSpan() {
        var handler = new TimeOnlyTypeHandler();
        var ts      = new TimeSpan(14, 30, 45);
        var cmd     = _conn.CreateCommand();
        cmd.CommandText = "SELECT @val";
        var param       = cmd.CreateParameter();
        param.ParameterName = "@val";
        param.Value          = ts.TotalSeconds;
        cmd.Parameters.Add(param);

        using var cmd2 = _conn.CreateCommand();
        cmd2.CommandText = "CREATE TABLE IF NOT EXISTS _th_test(t TEXT)";
        cmd2.ExecuteNonQuery();

        using var cmdInsert = _conn.CreateCommand();
        cmdInsert.CommandText = "DELETE FROM _th_test; INSERT INTO _th_test(t) VALUES ('14:30:45')";
        cmdInsert.ExecuteNonQuery();

        using var cmdSelect = _conn.CreateCommand();
        cmdSelect.CommandText = "SELECT t FROM _th_test";
        using var reader = cmdSelect.ExecuteReader();
        reader.Read();
        var raw = reader.GetValue(0);
        Assert.Equal("14:30:45", raw?.ToString());
    }

    [Fact]
    public void TimeOnlyTypeHandler_SetParameter_WithValue() {
        var handler = new TimeOnlyTypeHandler();
        Assert.Equal(typeof(TimeOnly), handler.TargetType);
        using var cmd = _conn.CreateCommand();
        var param     = cmd.CreateParameter();
        handler.SetParameter(param, new TimeOnly(14, 30, 0));
        Assert.IsType<TimeSpan>(param.Value);
        Assert.Equal(new TimeSpan(14, 30, 0), param.Value);
    }

    [Fact]
    public void TimeOnlyTypeHandler_SetParameter_Null() {
        var handler = new TimeOnlyTypeHandler();
        using var cmd = _conn.CreateCommand();
        var param     = cmd.CreateParameter();
        handler.SetParameter(param, null);
        Assert.Equal(DBNull.Value, param.Value);
    }

    // GetValue의 타입 분기(TimeSpan / DateTime / 그 외)는 provider가 반환하는 CLR 타입에
    // 의존하므로, 컬럼 타입을 직접 제어할 수 있는 DataTableReader로 검증한다.
    // (SQLite TEXT 컬럼은 GetValue가 string을 돌려주므로 분기를 재현할 수 없다)
    private static System.Data.Common.DbDataReader ReaderReturning(object value) {
        var table = new DataTable();
        table.Columns.Add("v", value.GetType());
        table.Rows.Add(value);
        var reader = table.CreateDataReader();
        reader.Read();
        return reader;
    }

    [Fact]
    public void TimeOnlyTypeHandler_GetValue_TimeSpan_ReturnsTimeOnly() {
        var handler = new TimeOnlyTypeHandler();
        using var reader = ReaderReturning(new TimeSpan(14, 30, 45));
        var result = Assert.IsType<TimeOnly>(handler.GetValue(reader, 0));
        Assert.Equal(new TimeOnly(14, 30, 45), result);
    }

    [Fact]
    public void TimeOnlyTypeHandler_GetValue_DateTime_ReturnsTimeOnly() {
        var handler = new TimeOnlyTypeHandler();
        using var reader = ReaderReturning(new DateTime(2026, 2, 26, 9, 8, 7));
        var result = Assert.IsType<TimeOnly>(handler.GetValue(reader, 0));
        Assert.Equal(new TimeOnly(9, 8, 7), result);
    }

    [Fact]
    public void TimeOnlyTypeHandler_GetValue_UnsupportedType_Throws() {
        var handler = new TimeOnlyTypeHandler();
        using var reader = ReaderReturning(42);
        var ex = Assert.Throws<InvalidOperationException>(() => handler.GetValue(reader, 0));
        Assert.Contains("TimeOnly 변환 불가", ex.Message);
    }
#endif

    public enum Color { Red, Green, Blue }

    [Fact]
    public void EnumStringTypeHandler_SetParameter_Value() {
        var handler = new EnumStringTypeHandler<Color>();
        Assert.Equal(typeof(Color), handler.TargetType);
        using var cmd = _conn.CreateCommand();
        var param     = cmd.CreateParameter();
        handler.SetParameter(param, Color.Green);
        Assert.Equal("Green", param.Value);
    }

    [Fact]
    public void EnumStringTypeHandler_SetParameter_Null() {
        var handler = new EnumStringTypeHandler<Color>();
        using var cmd = _conn.CreateCommand();
        var param     = cmd.CreateParameter();
        handler.SetParameter(param, null);
        Assert.Equal(DBNull.Value, param.Value);
    }

    [Fact]
    public void EnumStringTypeHandler_GetValue_Valid() {
        var handler = new EnumStringTypeHandler<Color>();
        var cmd     = _conn.CreateCommand();
        cmd.CommandText = "SELECT 'Blue'";
        using var reader = cmd.ExecuteReader();
        reader.Read();
        var result = handler.GetValue(reader, 0);
        Assert.Equal(Color.Blue, result);
    }

    [Fact]
    public void EnumStringTypeHandler_GetValue_Invalid_Throws() {
        var handler = new EnumStringTypeHandler<Color>();
        var cmd     = _conn.CreateCommand();
        cmd.CommandText = "SELECT 'Purple'";
        using var reader = cmd.ExecuteReader();
        reader.Read();
        Assert.Throws<InvalidOperationException>(() => handler.GetValue(reader, 0));
    }

    // ---------------------------------------------------------------------------
    // JsonTypeHandler<T> — 값 컬렉션을 JSON으로 직렬화/역직렬화한다.
    // ---------------------------------------------------------------------------

    public sealed class JsonPayload {
        public string Title  { get; set; } = string.Empty;
        public int    Count  { get; set; }
    }

    private static List<JsonPayload> SamplePayload() =>
        new() {
            new() { Title = "first",  Count = 1 },
            new() { Title = "second", Count = 2 }
        };

    [Fact]
    public void JsonTypeHandler_TargetType_IsPayload() {
        var handler = new JsonTypeHandler<JsonPayload>();
        Assert.Equal(typeof(JsonPayload), handler.TargetType);
    }

    [Fact]
    public void JsonTypeHandler_SetParameter_Value_SerializesToJson() {
        var handler = new JsonTypeHandler<JsonPayload>();
        using var cmd = _conn.CreateCommand();
        var param     = cmd.CreateParameter();
        handler.SetParameter(param, new JsonPayload { Title = "hello", Count = 7 });

        var json = Assert.IsType<string>(param.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal("hello", doc.RootElement.GetProperty("Title").GetString());
        Assert.Equal(7, doc.RootElement.GetProperty("Count").GetInt32());
    }

    [Fact]
    public void JsonTypeHandler_SetParameter_Null_StoresDBNull() {
        var handler = new JsonTypeHandler<JsonPayload>();
        using var cmd = _conn.CreateCommand();
        var param     = cmd.CreateParameter();
        handler.SetParameter(param, null);
        Assert.Equal(DBNull.Value, param.Value);
    }

    [Fact]
    public void JsonTypeHandler_GetValue_Valid_Deserializes() {
        var handler = new JsonTypeHandler<JsonPayload>();
        var cmd     = _conn.CreateCommand();
        cmd.CommandText = """SELECT '{"Title":"from-db","Count":42}'""";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var result = Assert.IsType<JsonPayload>(handler.GetValue(reader, 0));
        Assert.Equal("from-db", result.Title);
        Assert.Equal(42, result.Count);
    }

    [Fact]
    public void JsonTypeHandler_GetValue_Collection_RoundTripsThroughDatabase() {
        // SetParameter가 만든 값을 실제 컬럼에 저장한 뒤 GetValue로 되읽어 왕복을 검증한다.
        var handler = new JsonTypeHandler<List<JsonPayload>>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "CREATE TABLE t (payload TEXT)";
        cmd.ExecuteNonQuery();

        var param = cmd.CreateParameter();
        param.ParameterName = "@payload";
        handler.SetParameter(param, SamplePayload());
        cmd.CommandText = "INSERT INTO t (payload) VALUES (@payload)";
        cmd.Parameters.Add(param);
        cmd.ExecuteNonQuery();

        cmd.Parameters.Clear();
        cmd.CommandText = "SELECT payload FROM t";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var result = Assert.IsType<List<JsonPayload>>(handler.GetValue(reader, 0));
        Assert.Equal(2, result.Count);
        Assert.Equal("first", result[0].Title);
        Assert.Equal(2, result[1].Count);
    }

    [Fact]
    public void JsonTypeHandler_GetValue_DBNull_ReturnsNull() {
        var handler = new JsonTypeHandler<JsonPayload>();
        var cmd     = _conn.CreateCommand();
        cmd.CommandText = "SELECT NULL";
        using var reader = cmd.ExecuteReader();
        reader.Read();
        Assert.Null(handler.GetValue(reader, 0));
    }

    [Fact]
    public void JsonTypeHandler_GetValue_EmptyString_ReturnsNull() {
        var handler = new JsonTypeHandler<JsonPayload>();
        var cmd     = _conn.CreateCommand();
        cmd.CommandText = "SELECT ''";
        using var reader = cmd.ExecuteReader();
        reader.Read();
        Assert.Null(handler.GetValue(reader, 0));
    }

    [Fact]
    public void JsonTypeHandler_WithOptions_AppliesCustomNamingPolicy() {
        var options = new System.Text.Json.JsonSerializerOptions {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        };
        var handler = new JsonTypeHandler<JsonPayload>(options);
        using var cmd = _conn.CreateCommand();
        var param     = cmd.CreateParameter();
        handler.SetParameter(param, new JsonPayload { Title = "camel", Count = 1 });

        var json = Assert.IsType<string>(param.Value);
        Assert.Contains("\"title\"", json);
        Assert.DoesNotContain("\"Title\"", json);
    }
}
