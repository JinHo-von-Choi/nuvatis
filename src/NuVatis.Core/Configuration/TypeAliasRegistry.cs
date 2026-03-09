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

    /// <summary>
    /// 타입 별칭을 등록한다. 동일 별칭이 이미 존재하면 예외를 던진다.
    /// </summary>
    /// <param name="alias">XML에서 사용할 별칭 문자열.</param>
    /// <param name="type">별칭에 매핑할 CLR 타입.</param>
    /// <exception cref="InvalidOperationException">동일 별칭이 이미 등록된 경우.</exception>
    public void Register(string alias, Type type) {
        if (!_aliases.TryAdd(alias, type)) {
            throw new InvalidOperationException(
                $"타입 별칭 '{alias}'는 이미 등록되어 있습니다.");
        }
    }

    /// <summary>
    /// 별칭 또는 어셈블리 한정 타입 이름으로 CLR 타입을 해석한다.
    /// 등록된 별칭을 먼저 검색하고, 없으면 <see cref="Type.GetType(string)"/>으로 폴백한다.
    /// </summary>
    /// <param name="aliasOrTypeName">별칭 또는 어셈블리 한정 타입 이름.</param>
    /// <returns>해석된 <see cref="Type"/>.</returns>
    /// <exception cref="InvalidOperationException">해석할 수 없는 이름일 때.</exception>
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

    /// <summary>
    /// 별칭 또는 타입 이름으로 CLR 타입을 해석하고 성공 여부를 반환한다.
    /// </summary>
    /// <param name="aliasOrTypeName">별칭 또는 어셈블리 한정 타입 이름.</param>
    /// <param name="type">해석된 타입. 실패하면 <see langword="null"/>.</param>
    /// <returns>해석에 성공하면 <see langword="true"/>.</returns>
    [UnconditionalSuppressMessage("AOT", "IL2057", Justification = "SG가 빌드타임에 타입을 해석. 런타임 Type.GetType은 fallback")]
    public bool TryResolve(string aliasOrTypeName, out Type? type) {
        if (_aliases.TryGetValue(aliasOrTypeName, out type)) {
            return true;
        }

        type = Type.GetType(aliasOrTypeName);
        return type is not null;
    }
}
