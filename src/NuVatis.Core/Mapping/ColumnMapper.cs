using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NuVatis.Mapping;

/**
 * DbDataReader의 컬럼을 대상 타입의 프로퍼티에 자동으로 매핑한다.
 * 스칼라 타입(int, long, string 등)은 첫 번째 컬럼 값을 직접 반환한다.
 * 복합 타입은 컬럼명-프로퍼티명 매칭(case-insensitive, 언더스코어 무시)으로 매핑한다.
 *
 * NuGet AutoMapper 패키지와의 네이밍 혼동을 방지하기 위해
 * ColumnMapper로 명명한다.
 *
 * @author 최진호
 * @date   2026-02-24
 * @modified 2026-02-25 AutoMapper -> ColumnMapper 리네이밍
 */
public static class ColumnMapper {

    private static readonly HashSet<Type> ScalarTypes = new() {
        typeof(bool),
        typeof(byte), typeof(sbyte),
        typeof(short), typeof(ushort),
        typeof(int), typeof(uint),
        typeof(long), typeof(ulong),
        typeof(float), typeof(double), typeof(decimal),
        typeof(string),
        typeof(char),
        typeof(DateTime), typeof(DateTimeOffset),
        typeof(Guid),
        typeof(byte[]),
        typeof(TimeSpan)
    };

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

    /**
     * 지정된 타입이 스칼라(단일 값) 타입인지 판별한다.
     * Nullable<T>도 내부 타입이 스칼라이면 스칼라로 취급한다.
     */
    public static bool IsScalar(Type type) {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return ScalarTypes.Contains(underlying) || underlying.IsEnum;
    }

    /**
     * DbDataReader의 현재 행을 T 타입으로 매핑한다.
     * 스칼라 타입이면 첫 번째 컬럼을 직접 변환한다.
     * 복합 타입이면 컬럼명-프로퍼티명 자동 매칭.
     */
    [UnconditionalSuppressMessage("AOT", "IL2070",
        Justification = "런타임 자동 매핑. AOT 환경에서는 SG가 빌드타임에 매핑 코드를 생성한다.")]
    public static T MapRow<T>(DbDataReader reader) {
        var type = typeof(T);

        if (IsScalar(type)) {
            return MapScalar<T>(reader);
        }

        return MapComplex<T>(reader);
    }

    private static T MapScalar<T>(DbDataReader reader) {
        var value = reader.GetValue(0);
        if (value is DBNull) {
            return default!;
        }

        var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        return (T)Convert.ChangeType(value, targetType);
    }

    [UnconditionalSuppressMessage("AOT", "IL2070",
        Justification = "런타임 자동 매핑.")]
    [UnconditionalSuppressMessage("AOT", "IL2091",
        Justification = "런타임 자동 매핑. AOT 환경에서는 SG가 빌드타임에 매핑 코드를 생성한다.")]
    private static T MapComplex<T>(DbDataReader reader) {
        var type       = typeof(T);
        var properties = PropertyCache.GetOrAdd(type, t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite)
                .ToArray());

        var obj = Activator.CreateInstance<T>()!;

        for (var i = 0; i < reader.FieldCount; i++) {
            if (reader.IsDBNull(i)) continue;

            var columnName = reader.GetName(i);
            var prop       = FindMatchingProperty(properties, columnName);

            if (prop is null) continue;

            var value      = reader.GetValue(i);
            var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

            if (targetType.IsEnum) {
                prop.SetValue(obj, Enum.ToObject(targetType, value));
            } else {
                prop.SetValue(obj, Convert.ChangeType(value, targetType));
            }
        }

        return obj;
    }

    /**
     * 컬럼명과 프로퍼티명 매칭. case-insensitive + 언더스코어 제거.
     * user_name -> UserName, userId -> UserId 등.
     */
    private static PropertyInfo? FindMatchingProperty(PropertyInfo[] properties, string columnName) {
        var normalized = columnName.Replace("_", "");

        foreach (var prop in properties) {
            if (string.Equals(prop.Name, columnName, StringComparison.OrdinalIgnoreCase)) {
                return prop;
            }
            if (string.Equals(prop.Name, normalized, StringComparison.OrdinalIgnoreCase)) {
                return prop;
            }
        }

        return null;
    }
}
