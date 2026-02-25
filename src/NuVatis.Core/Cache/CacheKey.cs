using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NuVatis.Cache;

/**
 * 캐시 키 생성 유틸리티.
 * statementId + 파라미터를 조합하여 고유한 캐시 키를 생성한다.
 *
 * @author 최진호
 * @date   2026-02-25
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

        var paramJson = JsonSerializer.Serialize(parameter);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(paramJson));
        var hash      = Convert.ToHexString(hashBytes)[..16];

        return $"{statementId}:{hash}";
    }
}
