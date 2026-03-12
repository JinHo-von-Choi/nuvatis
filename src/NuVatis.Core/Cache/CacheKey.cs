using System.Security.Cryptography;
using System.Text;

namespace NuVatis.Cache;

/**
 * 캐시 키 생성 유틸리티.
 * statementId + 파라미터를 조합하여 고유한 캐시 키를 생성한다.
 *
 * JsonSerializer.Serialize(object) 대신 결정적(deterministic) 문자열 빌더를 사용하므로
 * AOT/trim 환경에서 IL2026 경고가 발생하지 않는다.
 *
 * @author 최진호
 * @date   2026-03-12
 */
public static class CacheKey {
    /**
     * statementId와 파라미터 객체로부터 캐시 키를 생성한다.
     * 파라미터가 null이면 statementId만 키가 된다.
     *
     * @param statementId SQL 구문 ID (namespace.id)
     * @param parameter   바인딩 파라미터 (nullable)
     * @returns 캐시 키 문자열
     */
    public static string Generate(string statementId, object? parameter) {
        if (parameter is null) {
            return statementId;
        }

        var raw       = BuildDeterministicString(parameter);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        var hash      = Convert.ToHexString(hashBytes)[..16];

        return $"{statementId}:{hash}";
    }

#pragma warning disable IL2026 // PropertyReflectionCache.GetOrBuild은 캐시 키 생성 전용 런타임 경로
    private static string BuildDeterministicString(object parameter) {
        if (parameter is string s)
            return s;

        if (parameter.GetType().IsPrimitive || parameter is decimal || parameter is Enum)
            return parameter.ToString()!;

        if (parameter is System.Collections.IDictionary dict) {
            var sb = new StringBuilder();
            foreach (System.Collections.DictionaryEntry kv in dict)
                sb.Append(kv.Key).Append('=').Append(kv.Value ?? "null").Append(';');
            return sb.ToString();
        }

        if (parameter is System.Collections.IEnumerable seq)
            return string.Join(",", seq.Cast<object?>().Select(o => o?.ToString() ?? "null"));

        var props   = NuVatis.Internal.PropertyReflectionCache.GetOrBuild(
            parameter.GetType(), normalizeUnderscore: false);
        var ordered = props.OrderBy(kv => kv.Key, StringComparer.Ordinal);
        var builder = new StringBuilder();
        foreach (var kv in ordered)
            builder.Append(kv.Key).Append('=').Append(kv.Value.GetValue(parameter) ?? "null").Append(';');
        return builder.ToString();
    }
#pragma warning restore IL2026
}
