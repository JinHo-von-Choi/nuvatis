using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace NuVatis.Internal;

/**
 * 타입의 공개 인스턴스 프로퍼티를 캐시하는 공유 유틸리티.
 * ColumnMapper와 TestExpressionEvaluator에서 공통으로 사용한다.
 *
 * @author 최진호
 * @date   2026-03-05
 */
internal static class PropertyReflectionCache {

    // normalizeUnderscore=true (ColumnMapper 용)
    private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>>
        _normalizedCache = new();

    // normalizeUnderscore=false (TestExpressionEvaluator 용)
    private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>>
        _plainCache = new();

    /**
     * 지정 타입의 프로퍼티 사전을 반환한다. 타입당 1회 빌드 후 캐시에서 반환한다.
     *
     * @param type                대상 타입
     * @param normalizeUnderscore true이면 언더스코어 제거 정규화 이름도 등록한다
     *                            (ColumnMapper 전용: CanWrite=true 프로퍼티만 포함)
     *                            false이면 읽기 전용 포함 모든 프로퍼티 등록
     *                            (TestExpressionEvaluator 전용: 익명 타입 지원)
     */
    /// <summary>
    /// 지정 타입에서 이름으로 단일 프로퍼티를 조회한다.
    /// 대소문자 무관(OrdinalIgnoreCase). 없으면 null 반환.
    /// ParameterBinder의 경로 탐색에서 사용한다.
    /// </summary>
    public static PropertyInfo? GetProperty(Type type, string name) {
        var map = GetOrBuild(type, normalizeUnderscore: false);
        return map.TryGetValue(name, out var prop) ? prop : null;
    }

    [UnconditionalSuppressMessage("AOT", "IL2070",
        Justification = "런타임 리플렉션. AOT 환경에서는 SG가 빌드타임 코드를 생성한다.")]
    public static Dictionary<string, PropertyInfo> GetOrBuild(
        Type type,
        bool normalizeUnderscore = false) {

        var cache = normalizeUnderscore ? _normalizedCache : _plainCache;
        return cache.GetOrAdd(type, t => Build(t, normalizeUnderscore));
    }

    private static Dictionary<string, PropertyInfo> Build(Type type, bool normalizeUnderscore) {
        var map   = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // normalizeUnderscore=true(ColumnMapper): 쓰기 가능한 프로퍼티만 포함 (매핑 대상)
        // normalizeUnderscore=false(TestExpressionEvaluator): 읽기 전용 포함 (익명 타입 지원)
        var filtered = normalizeUnderscore ? props.Where(p => p.CanWrite) : props;

        foreach (var prop in filtered) {
            map.TryAdd(prop.Name, prop);

            if (normalizeUnderscore) {
                var normalized = prop.Name.Replace("_", "");
                if (normalized != prop.Name)
                    map.TryAdd(normalized, prop);
            }
        }

        return map;
    }
}
