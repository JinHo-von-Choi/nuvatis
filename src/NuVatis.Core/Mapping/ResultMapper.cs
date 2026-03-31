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

    /// <summary>지정된 ResultMap 정의로 ResultMapper를 초기화한다.</summary>
    /// <param name="resultMaps">ResultMap ID를 키로 하는 ResultMapDefinition 사전.</param>
    public ResultMapper(Dictionary<string, ResultMapDefinition> resultMaps) {
        _resultMaps = resultMaps;
    }

    /// <summary>
    /// DbDataReader의 현재 행을 지정된 resultMapId 정의에 따라 T 인스턴스로 매핑한다.
    /// SG(Source Generator) 경로에서는 정적 생성 코드를 사용하며, 이 메서드는 런타임 폴백 경로이다.
    /// </summary>
    [RequiresUnreferencedCode(
        "런타임 ResultMap 폴백 경로. Source Generator가 생성한 매핑 코드가 있으면 호출되지 않는다.")]
#if NET7_0_OR_GREATER
    [RequiresDynamicCode(
        "Activator.CreateInstance(Type) 호출을 포함한다. Source Generator 경로에서는 호출되지 않는다.")]
#endif
    public T MapRow<T>(DbDataReader reader, string resultMapId) where T : new() {
        if (!_resultMaps.TryGetValue(resultMapId, out var def))
            throw new KeyNotFoundException($"ResultMap '{resultMapId}'을 찾을 수 없습니다.");

        return (T)MapRowInternal(reader, def, null, typeof(T));
    }

    /// <summary>
    /// DbDataReader의 모든 행을 resultMapId 정의에 따라 T 리스트로 매핑한다.
    /// ID 컬럼이 정의된 경우 1:N Collection 그룹핑을 수행한다.
    /// </summary>
    [RequiresUnreferencedCode(
        "런타임 ResultMap 폴백 경로. Source Generator가 생성한 매핑 코드가 있으면 호출되지 않는다.")]
#if NET7_0_OR_GREATER
    [RequiresDynamicCode(
        "Activator.CreateInstance(Type) 호출을 포함한다. Source Generator 경로에서는 호출되지 않는다.")]
#endif
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
                ProcessCollection(reader, coll, root!);
            }
        }

        return results;
    }

#pragma warning disable IL2070, IL2067 // 호출자(MapRow<T>)가 RequiresUnreferencedCode 보장
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
            // GetOrdinal은 컬럼 미존재 시 IndexOutOfRangeException — optional 매핑이므로 건너뜀
            try { ordinal = reader.GetOrdinal(colName); } catch (IndexOutOfRangeException) { continue; }

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
#pragma warning restore IL2070, IL2067

#pragma warning disable IL2070, IL2067, IL3050 // 호출자(MapRow<T>)가 RequiresUnreferencedCode/RequiresDynamicCode 보장
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
#pragma warning restore IL2070, IL2067

    private object? GetIdKey(DbDataReader reader, List<ResultMapping> idMappings, string? prefix) {
        if (idMappings.Count == 0) return null;

        if (idMappings.Count == 1) {
            int ord;
            // ID 컬럼 미존재 시 null 반환 — resultMap에 정의된 컬럼이 SELECT에 없을 수 있음
            try { ord = reader.GetOrdinal((prefix ?? "") + idMappings[0].Column); }
            catch (IndexOutOfRangeException) { return null; }
            return reader.IsDBNull(ord) ? null : reader.GetValue(ord);
        }

        return string.Join("|", idMappings.Select(m => {
            int ord;
            // 복합 ID의 개별 컬럼 미존재 시 "MISSING" 마커 — 전체 키 구성에는 영향 없음
            try { ord = reader.GetOrdinal((prefix ?? "") + m.Column); }
            catch (IndexOutOfRangeException) { return "MISSING"; }
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
