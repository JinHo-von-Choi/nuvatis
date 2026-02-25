using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace NuVatis.Configuration;

/**
 * XML에서 사용하는 타입 별칭을 관리한다.
 * <typeAlias alias="User" type="Sample.Models.User" /> 형태의 매핑을 저장.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public sealed class TypeAliasRegistry {
    private readonly ConcurrentDictionary<string, Type> _aliases = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string alias, Type type) {
        if (!_aliases.TryAdd(alias, type)) {
            throw new InvalidOperationException(
                $"타입 별칭 '{alias}'는 이미 등록되어 있습니다.");
        }
    }

    [UnconditionalSuppressMessage("AOT", "IL2057", Justification = "SG가 빌드타임에 타입을 해석. 런타임 Type.GetType은 fallback")]
    public Type Resolve(string aliasOrTypeName) {
        if (_aliases.TryGetValue(aliasOrTypeName, out var type)) {
            return type;
        }

        var resolved = Type.GetType(aliasOrTypeName);
        if (resolved is not null) {
            return resolved;
        }

        throw new InvalidOperationException(
            $"타입 '{aliasOrTypeName}'을 해석할 수 없습니다. " +
            $"등록된 별칭: [{string.Join(", ", _aliases.Keys)}]");
    }

    [UnconditionalSuppressMessage("AOT", "IL2057", Justification = "SG가 빌드타임에 타입을 해석. 런타임 Type.GetType은 fallback")]
    public bool TryResolve(string aliasOrTypeName, out Type? type) {
        if (_aliases.TryGetValue(aliasOrTypeName, out type)) {
            return true;
        }

        type = Type.GetType(aliasOrTypeName);
        return type is not null;
    }
}
