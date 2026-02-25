using System.Collections;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace NuVatis.Mapping;

/**
 * ResultMap 정의를 기반으로 DbDataReader에서 객체를 매핑하는 런타임 엔진.
 * Association(1:1)과 Collection(1:N) 중첩 매핑을 지원한다.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public sealed class ResultMapper {

    private readonly Dictionary<string, ResultMapDefinition> _resultMaps;
    private readonly ConcurrentDictionary<Type, PropertyInfo[]> _propertyCache = new();

    public ResultMapper(Dictionary<string, ResultMapDefinition> resultMaps) {
        _resultMaps = resultMaps;
    }

    [UnconditionalSuppressMessage("AOT", "IL2070",
        Justification = "런타임 ResultMap 매핑은 reflection 사용이 불가피. SG 경로에서는 정적 매핑 코드 사용.")]
    public T MapRow<T>(DbDataReader reader, string resultMapId) where T : new() {
        if (!_resultMaps.TryGetValue(resultMapId, out var def))
            throw new KeyNotFoundException($"ResultMap '{resultMapId}'을 찾을 수 없습니다.");

        return (T)MapRowInternal(reader, def, null, typeof(T));
    }

    public IList<T> MapRows<T>(DbDataReader reader, string resultMapId) where T : new() {
        if (!_resultMaps.TryGetValue(resultMapId, out var def))
            throw new KeyNotFoundException($"ResultMap '{resultMapId}'을 찾을 수 없습니다.");

        var results    = new List<T>();
        var idMappings = def.Mappings.Where(m => m.IsId).ToList();

        if (def.Collections.Count == 0 || idMappings.Count == 0) {
            while (reader.Read()) {
                results.Add(MapRow<T>(reader, resultMapId));
            }
            return results;
        }

        var lookup = new Dictionary<object, T>();
        while (reader.Read()) {
            var id = GetIdKey(reader, idMappings, null);
            if (id is null) continue;

            if (!lookup.TryGetValue(id, out var root)) {
                root       = MapRow<T>(reader, resultMapId);
                lookup[id] = root;
                results.Add(root);
            }

            foreach (var coll in def.Collections) {
                ProcessCollection(reader, coll, root);
            }
        }

        return results;
    }

    [UnconditionalSuppressMessage("AOT", "IL2070", Justification = "런타임 매핑 경로")]
    [UnconditionalSuppressMessage("AOT", "IL2067", Justification = "런타임 매핑 경로: Activator.CreateInstance")]
    private object MapRowInternal(
        DbDataReader reader,
        ResultMapDefinition def,
        string? columnPrefix,
        Type targetType) {

        var instance = Activator.CreateInstance(targetType)!;
        var props    = _propertyCache.GetOrAdd(targetType,
            t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));

        foreach (var mapping in def.Mappings) {
            var colName = (columnPrefix ?? "") + mapping.Column;
            int ordinal;
            try { ordinal = reader.GetOrdinal(colName); } catch { continue; }

            if (!reader.IsDBNull(ordinal)) {
                var val  = reader.GetValue(ordinal);
                var prop = props.FirstOrDefault(p => p.Name == mapping.Property);
                if (prop is { CanWrite: true }) {
                    prop.SetValue(instance, ConvertValue(val, prop.PropertyType));
                }
            }
        }

        foreach (var assoc in def.Associations) {
            if (string.IsNullOrEmpty(assoc.ResultMapId)
                || !_resultMaps.TryGetValue(assoc.ResultMapId, out var assocDef))
                continue;

            var prop = props.FirstOrDefault(p => p.Name == assoc.Property);
            if (prop is { CanWrite: true }) {
                var nestedPrefix = (columnPrefix ?? "") + (assoc.ColumnPrefix ?? "");
                var nestedObj    = MapRowInternal(reader, assocDef, nestedPrefix, prop.PropertyType);
                prop.SetValue(instance, nestedObj);
            }
        }

        return instance;
    }

    [UnconditionalSuppressMessage("AOT", "IL2070", Justification = "런타임 매핑 경로")]
    [UnconditionalSuppressMessage("AOT", "IL2067", Justification = "런타임 매핑 경로: Activator.CreateInstance")]
    private void ProcessCollection(
        DbDataReader reader,
        CollectionMapping mapping,
        object root) {

        if (string.IsNullOrEmpty(mapping.ResultMapId)
            || !_resultMaps.TryGetValue(mapping.ResultMapId, out var def))
            return;

        var rootProps  = _propertyCache.GetOrAdd(root.GetType(),
            t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));
        var prop       = rootProps.FirstOrDefault(p => p.Name == mapping.Property);
        if (prop is null) return;

        var collection = (IList?)prop.GetValue(root);
        if (collection is null) {
            var itemType = prop.PropertyType.IsGenericType
                ? prop.PropertyType.GetGenericArguments()[0]
                : typeof(object);
            collection = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType))!;
            prop.SetValue(root, collection);
        }

        var itemTypeInColl = collection.GetType().GetGenericArguments()[0];
        var child          = MapRowInternal(reader, def, mapping.ColumnPrefix, itemTypeInColl);
        collection.Add(child);
    }

    private object? GetIdKey(DbDataReader reader, List<ResultMapping> idMappings, string? prefix) {
        if (idMappings.Count == 0) return null;

        if (idMappings.Count == 1) {
            int ord;
            try { ord = reader.GetOrdinal((prefix ?? "") + idMappings[0].Column); }
            catch { return null; }
            return reader.IsDBNull(ord) ? null : reader.GetValue(ord);
        }

        return string.Join("|", idMappings.Select(m => {
            int ord;
            try { ord = reader.GetOrdinal((prefix ?? "") + m.Column); }
            catch { return "MISSING"; }
            return reader.IsDBNull(ord) ? "NULL" : reader.GetValue(ord).ToString();
        }));
    }

    private static object? ConvertValue(object val, Type targetType) {
        if (val is null or DBNull) return null;
        var t = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (t.IsEnum) return Enum.ToObject(t, val);
        return Convert.ChangeType(val, t, CultureInfo.InvariantCulture);
    }
}
