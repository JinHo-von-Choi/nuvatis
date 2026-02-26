using System.Data;
using System.Data.Common;
using System.Reflection;
using NuVatis.Mapping;

namespace NuVatis.Tests;

/**
 * ColumnMapper 단위 테스트.
 * O(1) Dictionary 캐시 구현 검증, 컬럼-프로퍼티 매핑 정확성 검증.
 *
 * @author   최진호
 * @date     2026-02-27
 */
public class ColumnMapperTests {

    // -------------------------------------------------------------------------
    // FakeDataReader
    // -------------------------------------------------------------------------

    /**
     * 테스트용 DbDataReader 구현체.
     * 컬럼명-값 쌍을 정적으로 보유하며 단일 행을 반환한다.
     */
    private sealed class FakeDataReader : DbDataReader {

        private readonly string[]  _names;
        private readonly object?[] _values;
        private bool               _read = false;

        public FakeDataReader(string[] names, object?[] values) {
            _names  = names;
            _values = values;
        }

        public override int    FieldCount          => _names.Length;
        public override bool   HasRows             => true;
        public override bool   IsClosed            => false;
        public override int    RecordsAffected     => -1;
        public override int    Depth               => 0;

        public override bool Read() {
            if (_read) return false;
            _read = true;
            return true;
        }

        public override string GetName(int ordinal)          => _names[ordinal];
        public override object GetValue(int ordinal)         => _values[ordinal]!;
        public override bool   IsDBNull(int ordinal)         => _values[ordinal] is null or DBNull;
        public override int    GetOrdinal(string name)       => Array.IndexOf(_names, name);
        public override Type   GetFieldType(int ordinal)     => _values[ordinal]?.GetType() ?? typeof(object);
        public override string GetDataTypeName(int ordinal)  => GetFieldType(ordinal).Name;

        public override bool   GetBoolean(int ordinal)       => (bool)_values[ordinal]!;
        public override byte   GetByte(int ordinal)          => (byte)_values[ordinal]!;
        public override char   GetChar(int ordinal)          => (char)_values[ordinal]!;
        public override Guid   GetGuid(int ordinal)          => (Guid)_values[ordinal]!;
        public override short  GetInt16(int ordinal)         => (short)_values[ordinal]!;
        public override int    GetInt32(int ordinal)         => (int)_values[ordinal]!;
        public override long   GetInt64(int ordinal)         => (long)_values[ordinal]!;
        public override float  GetFloat(int ordinal)         => (float)_values[ordinal]!;
        public override double GetDouble(int ordinal)        => (double)_values[ordinal]!;
        public override string GetString(int ordinal)        => (string)_values[ordinal]!;
        public override decimal GetDecimal(int ordinal)      => (decimal)_values[ordinal]!;
        public override DateTime GetDateTime(int ordinal)    => (DateTime)_values[ordinal]!;

        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer,
                                       int bufferOffset, int length) => 0;
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer,
                                       int bufferOffset, int length) => 0;

        public override int GetValues(object[] values) {
            for (var i = 0; i < _values.Length; i++) values[i] = _values[i]!;
            return _values.Length;
        }

        public override object  this[int ordinal]  => _values[ordinal]!;
        public override object  this[string name]  => _values[GetOrdinal(name)]!;

        public override bool NextResult()           => false;
        public override IEnumerator<DbDataRecord> GetEnumerator() =>
            throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // DTOs
    // -------------------------------------------------------------------------

    private class UserDto {
        public string? UserName  { get; set; }
        public int     UserId    { get; set; }
        public string? Email     { get; set; }
    }

    private class DuplicateNormalizedDto {
        /** UserName과 User_Name은 정규화 시 동일하므로 First-win 정책 적용 */
        public string? UserName  { get; set; }
        public string? User_Name { get; set; }
    }

    // -------------------------------------------------------------------------
    // 테스트
    // -------------------------------------------------------------------------

    /**
     * snake_case 컬럼명이 CamelCase 프로퍼티에 매핑되는지 검증.
     * user_name -> UserName
     */
    [Fact]
    public void MapRow_SnakeCaseColumn_MapsToCamelCaseProperty() {
        var reader = new FakeDataReader(
            names:  ["user_name"],
            values: ["Alice"]
        );

        reader.Read();
        var result = ColumnMapper.MapRow<UserDto>(reader);

        Assert.Equal("Alice", result.UserName);
    }

    /**
     * 정확히 일치하는 컬럼명이 있을 때 매핑되는지 검증.
     * UserId -> UserId (exact match)
     */
    [Fact]
    public void MapRow_ExactMatchColumn_MapsCorrectly() {
        var reader = new FakeDataReader(
            names:  ["UserId"],
            values: [42]
        );

        reader.Read();
        var result = ColumnMapper.MapRow<UserDto>(reader);

        Assert.Equal(42, result.UserId);
    }

    /**
     * 존재하지 않는 컬럼은 조용히 무시되어야 한다 (예외 없음).
     */
    [Fact]
    public void MapRow_UnknownColumn_SilentlyIgnored() {
        var reader = new FakeDataReader(
            names:  ["unknown_col"],
            values: ["ignored"]
        );

        reader.Read();
        var ex = Record.Exception(() => ColumnMapper.MapRow<UserDto>(reader));

        Assert.Null(ex);
    }

    /**
     * 동일 타입 두 번째 호출 시 캐시에서 Dictionary를 재사용하는지 검증.
     * PropertyCache의 값 타입이 Dictionary<string, PropertyInfo>여야 한다.
     */
    [Fact]
    public void MapRow_SameType_ReusesDictionaryCache() {
        // 캐시가 생성되도록 한 번 실행
        var reader1 = new FakeDataReader(
            names:  ["UserId"],
            values: [1]
        );
        reader1.Read();
        ColumnMapper.MapRow<UserDto>(reader1);

        // PropertyCache 필드 접근
        var cacheField = typeof(ColumnMapper)
            .GetField("PropertyCache", BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.NotNull(cacheField);

        var cache = (System.Collections.Concurrent.ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>>)cacheField.GetValue(null)!;
        Assert.NotNull(cache);

        // 첫 번째 호출 후 캐시에서 Dictionary 참조 획득
        Assert.True(cache.TryGetValue(typeof(UserDto), out var dictBefore));

        // 동일 타입으로 두 번째 호출
        var reader2 = new FakeDataReader(
            names:  ["UserId"],
            values: [2]
        );
        reader2.Read();
        ColumnMapper.MapRow<UserDto>(reader2);

        // 두 번째 호출 후 동일 Dictionary 인스턴스가 재사용되어야 한다
        Assert.True(cache.TryGetValue(typeof(UserDto), out var dictAfter));
        Assert.Same(dictBefore, dictAfter);

        // ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>>이어야 한다
        var cacheType = cache!.GetType();

        // TValue는 Dictionary<string, PropertyInfo>여야 한다
        Assert.Contains("Dictionary", cacheType.GenericTypeArguments[1].Name);
    }

    /**
     * 정규화 시 동일한 키를 생성하는 프로퍼티가 둘 있을 때
     * First-win 정책으로 예외 없이 처리되어야 한다.
     */
    [Fact]
    public void MapRow_DuplicateNormalizedKey_FirstWinNoException() {
        var reader = new FakeDataReader(
            names:  ["user_name"],
            values: ["Bob"]
        );

        reader.Read();
        DuplicateNormalizedDto? result = null;
        var ex = Record.Exception(() => result = ColumnMapper.MapRow<DuplicateNormalizedDto>(reader));

        Assert.Null(ex);
        Assert.NotNull(result);
        Assert.True(result.UserName == "Bob" || result.User_Name == "Bob",
            "Either UserName or User_Name should have been mapped to 'Bob'");
    }

    /**
     * 스칼라 int 타입 매핑 검증.
     */
    [Fact]
    public void MapRow_ScalarInt_ReturnsValue() {
        var reader = new FakeDataReader(
            names:  ["count"],
            values: [99]
        );

        reader.Read();
        var result = ColumnMapper.MapRow<int>(reader);

        Assert.Equal(99, result);
    }
}
