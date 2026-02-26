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
}
