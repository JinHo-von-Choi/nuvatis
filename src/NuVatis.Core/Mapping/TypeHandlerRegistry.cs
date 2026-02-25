using System.Collections.Concurrent;

namespace NuVatis.Mapping;

/**
 * 타입 핸들러를 관리하는 레지스트리.
 * 빌트인 타입 핸들러를 기본 등록하고, 커스텀 핸들러 추가를 지원한다.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public sealed class TypeHandlerRegistry {
    private readonly ConcurrentDictionary<Type, ITypeHandler> _handlers = new();
    private readonly ConcurrentDictionary<string, ITypeHandler> _namedHandlers = new(StringComparer.OrdinalIgnoreCase);

    public void Register(ITypeHandler handler) {
        _handlers[handler.TargetType] = handler;
    }

    public void Register(string name, ITypeHandler handler) {
        _namedHandlers[name] = handler;
        _handlers[handler.TargetType] = handler;
    }

    public ITypeHandler? Get(Type type) {
        _handlers.TryGetValue(type, out var handler);
        return handler;
    }

    public ITypeHandler? Get(string name) {
        _namedHandlers.TryGetValue(name, out var handler);
        return handler;
    }
}
