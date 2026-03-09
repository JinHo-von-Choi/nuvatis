namespace NuVatis.QueryBuilder.Tests.Helpers;

using System.Collections;
using System.Data;
using System.Data.Common;

/** 단위 테스트용 인메모리 DbDataReader */
public sealed class FakeDataReader : DbDataReader {
    private readonly string[]  _columns;
    private readonly object?[] _row;
    private bool               _read;

    public FakeDataReader(string[] columns, object?[] row) {
        _columns = columns;
        _row     = row;
    }

    public override bool   Read()       { var r = !_read; _read = true; return r; }
    public override int    FieldCount   => _columns.Length;
    public override int    GetOrdinal(string name) => Array.IndexOf(_columns, name);
    public override string GetName(int i)   => _columns[i];
    public override object GetValue(int i)  => _row[i] ?? DBNull.Value;
    public override bool   IsDBNull(int i)  => _row[i] is null || _row[i] is DBNull;

    public override bool     GetBoolean(int i)  => (bool)_row[i]!;
    public override byte     GetByte(int i)     => Convert.ToByte(_row[i]);
    public override char     GetChar(int i)     => Convert.ToChar(_row[i]);
    public override DateTime GetDateTime(int i) => (DateTime)_row[i]!;
    public override decimal  GetDecimal(int i)  => Convert.ToDecimal(_row[i]);
    public override double   GetDouble(int i)   => Convert.ToDouble(_row[i]);
    public override float    GetFloat(int i)    => Convert.ToSingle(_row[i]);
    public override Guid     GetGuid(int i)     => (Guid)_row[i]!;
    public override short    GetInt16(int i)    => Convert.ToInt16(_row[i]);
    public override int      GetInt32(int i)    => Convert.ToInt32(_row[i]);
    public override long     GetInt64(int i)    => Convert.ToInt64(_row[i]);
    public override string   GetString(int i)   => (string)_row[i]!;

    public override string GetDataTypeName(int i)  => _row[i]?.GetType().Name ?? "null";
    public override Type   GetFieldType(int i)     => _row[i]?.GetType() ?? typeof(object);

    public override long GetBytes(int i, long offset, byte[]? buffer, int bufferOffset, int length) => 0;
    public override long GetChars(int i, long offset, char[]? buffer, int bufferOffset, int length) => 0;
    public override int  GetValues(object[] values) => 0;

    public override object this[int i]       => _row[i]!;
    public override object this[string name] => _row[GetOrdinal(name)]!;

    public override int  Depth           => 0;
    public override bool IsClosed        => false;
    public override int  RecordsAffected => -1;
    public override bool HasRows         => true;
    public override bool NextResult()    => false;

    public override IEnumerator GetEnumerator() => throw new NotSupportedException();
}
