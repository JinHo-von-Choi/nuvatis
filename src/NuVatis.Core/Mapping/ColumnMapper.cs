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
 * @modified 2026-02-27 O(n) → O(1) Dictionary cache for column mapping
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

    private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> PropertyCache = new();

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
        Justification = "런타임 자동 매핑. AOT 환경에서는 SG가 빌드타임에 매핑 코드를 생성한다.")]
    [UnconditionalSuppressMessage("AOT", "IL2091",
        Justification = "런타임 자동 매핑. AOT 환경에서는 SG가 빌드타임에 매핑 코드를 생성한다.")]
    private static T MapComplex<T>(DbDataReader reader) {
        var type      = typeof(T);
        var columnMap = PropertyCache.GetOrAdd(type, BuildColumnMap);
        var obj       = Activator.CreateInstance<T>()!;

        for (var i = 0; i < reader.FieldCount; i++) {
            if (reader.IsDBNull(i)) continue;

            var columnName = reader.GetName(i);
            if (string.IsNullOrEmpty(columnName)) continue;

            if (!columnMap.TryGetValue(columnName, out var prop)) {
                if (!columnName.Contains('_') ||
                    !columnMap.TryGetValue(columnName.Replace("_", ""), out prop))
                    continue;
            }

            var value      = reader.GetValue(i);
            var targetType = Nullable.GetUnderlyingType(prop!.PropertyType) ?? prop.PropertyType;

            if (targetType.IsEnum) {
                prop.SetValue(obj, Enum.ToObject(targetType, value));
            } else {
                prop.SetValue(obj, Convert.ChangeType(value, targetType));
            }
        }

        return obj;
    }

    /**
     * 타입의 프로퍼티를 OrdinalIgnoreCase Dictionary로 빌드한다.
     * 원본 이름과 언더스코어 제거 정규화 이름 모두 등록 (First-win).
     * 빌드 시점은 타입당 1회이며, 이후 O(1) 조회가 가능하다.
     */
    [UnconditionalSuppressMessage("AOT", "IL2070",
        Justification = "런타임 자동 매핑.")]
    private static Dictionary<string, PropertyInfo> BuildColumnMap(Type type) {
        var map = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                 .Where(p => p.CanWrite)) {
            // 원본 이름 등록 (First-win)
            map.TryAdd(prop.Name, prop);

            // 언더스코어 제거 정규화 이름 등록 (이미 존재하면 First-win 유지)
            var normalized = prop.Name.Replace("_", "");
            if (normalized != prop.Name)
                map.TryAdd(normalized, prop);
        }

        return map;
    }
}
