using System.Collections.Concurrent;

namespace NuVatis.Provider;

/**
 * 등록된 DB Provider를 관리하는 레지스트리.
 * Provider 이름으로 IDbProvider 인스턴스를 조회한다.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public sealed class DbProviderRegistry {
    private readonly ConcurrentDictionary<string, IDbProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Provider를 레지스트리에 등록한다. 동일 이름이 이미 등록된 경우 예외를 던진다.
    /// </summary>
    /// <param name="provider">등록할 <see cref="IDbProvider"/> 구현체.</param>
    /// <exception cref="InvalidOperationException">동일 이름의 Provider가 이미 존재할 때.</exception>
    public void Register(IDbProvider provider) {
        if (!_providers.TryAdd(provider.Name, provider)) {
            throw new InvalidOperationException(
                $"Provider '{provider.Name}'는 이미 등록되어 있습니다.");
        }
    }

    /// <summary>
    /// 이름으로 등록된 Provider를 조회한다. 없으면 예외를 던진다.
    /// </summary>
    /// <param name="name">Provider 식별 이름 (대소문자 무시).</param>
    /// <returns>등록된 <see cref="IDbProvider"/> 인스턴스.</returns>
    /// <exception cref="InvalidOperationException">해당 이름의 Provider가 없을 때.</exception>
    public IDbProvider Get(string name) {
        if (_providers.TryGetValue(name, out var provider)) {
            return provider;
        }

        throw new InvalidOperationException(
            $"Provider '{name}'를 찾을 수 없습니다. 등록된 Provider: [{string.Join(", ", _providers.Keys)}]");
    }

    /// <summary>
    /// 지정 이름의 Provider가 등록되어 있는지 확인한다.
    /// </summary>
    /// <param name="name">확인할 Provider 이름 (대소문자 무시).</param>
    /// <returns>등록되어 있으면 <see langword="true"/>.</returns>
    public bool IsRegistered(string name) => _providers.ContainsKey(name);
}
