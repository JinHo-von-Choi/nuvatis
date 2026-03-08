using System.Collections.Concurrent;
using System.Data.Common;
using System.Reflection;

namespace NuVatis.QueryBuilder.Execution;

public static class RecordMapper {
    private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> _cache = new();

    public static T MapRow<T>(DbDataReader reader) {
        var underlyingType = Nullable.GetUnderlyingType(typeof(T));
        var effectiveType  = underlyingType ?? typeof(T);

        if (IsScalar(effectiveType)) {
            if (reader.IsDBNull(0)) return default!;
            return (T)Convert.ChangeType(reader.GetValue(0), effectiveType)!;
        }

        var map = _cache.GetOrAdd(typeof(T), BuildMap);
        var obj = Activator.CreateInstance<T>();

        for (int i = 0; i < reader.FieldCount; i++) {
            if (reader.IsDBNull(i)) continue;
            var col = reader.GetName(i);
            if (map.TryGetValue(Normalize(col), out var prop)) {
                var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                prop.SetValue(obj, Convert.ChangeType(reader.GetValue(i), targetType));
            }
        }

        return obj;
    }

    private static Dictionary<string, PropertyInfo> BuildMap(Type type) {
        var map = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
            if (!p.CanWrite) continue;
            map.TryAdd(Normalize(p.Name), p);
        }
        return map;
    }

    private static string Normalize(string name)
        => name.Replace("_", "", StringComparison.Ordinal).ToLowerInvariant();

    private static bool IsScalar(Type t)
        => t.IsPrimitive || t == typeof(string) || t == typeof(decimal)
        || t == typeof(DateTime) || t == typeof(Guid);
}
