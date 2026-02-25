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

    public void Register(IDbProvider provider) {
        if (!_providers.TryAdd(provider.Name, provider)) {
            throw new InvalidOperationException(
                $"Provider '{provider.Name}'는 이미 등록되어 있습니다.");
        }
    }

    public IDbProvider Get(string name) {
        if (_providers.TryGetValue(name, out var provider)) {
            return provider;
        }

        throw new InvalidOperationException(
            $"Provider '{name}'를 찾을 수 없습니다. 등록된 Provider: [{string.Join(", ", _providers.Keys)}]");
    }

    public bool IsRegistered(string name) => _providers.ContainsKey(name);
}
