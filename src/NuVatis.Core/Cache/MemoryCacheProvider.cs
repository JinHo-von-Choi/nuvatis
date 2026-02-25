using System.Collections.Concurrent;

namespace NuVatis.Cache;

/**
 * 인메모리 2차 캐시 제공자.
 * Namespace 별로 독립적인 LRU 캐시를 관리한다.
 *
 * Thread-safety: ConcurrentDictionary + 개별 lock으로 동시 접근 안전.
 * 외부 패키지 의존성 없이 NuVatis.Core만으로 동작한다.
 *
 * @author 최진호
 * @date   2026-02-25
 */
public sealed class MemoryCacheProvider : ICacheProvider, IDisposable {

    private readonly ConcurrentDictionary<string, NamespaceCache> _caches = new();
    private readonly ConcurrentDictionary<string, CacheConfig>    _configs = new();
    private readonly ConcurrentDictionary<string, Timer?>         _timers = new();

    public void RegisterNamespace(string namespace_, CacheConfig config) {
        _configs[namespace_] = config;
        _caches.GetOrAdd(namespace_, _ => new NamespaceCache(config.Size));

        if (config.FlushIntervalMs is > 0) {
            var timer = new Timer(
                _ => Flush(namespace_),
                null,
                config.FlushIntervalMs.Value,
                config.FlushIntervalMs.Value);
            var old = _timers.GetValueOrDefault(namespace_);
            _timers[namespace_] = timer;
            old?.Dispose();
        }
    }

    public object? Get(string namespace_, string cacheKey) {
        if (!_caches.TryGetValue(namespace_, out var cache)) return null;
        return cache.Get(cacheKey);
    }

    public void Put(string namespace_, string cacheKey, object value) {
        if (!_caches.TryGetValue(namespace_, out var cache)) return;
        cache.Put(cacheKey, value);
    }

    public void Flush(string namespace_) {
        if (_caches.TryGetValue(namespace_, out var cache)) {
            cache.Clear();
        }
    }

    public void Dispose() {
        foreach (var timer in _timers.Values) {
            timer?.Dispose();
        }
        _timers.Clear();
        _caches.Clear();
        _configs.Clear();
    }

    /**
     * Namespace별 LRU 캐시.
     * LinkedList로 접근 순서를 추적하고,
     * 용량 초과 시 가장 오래 미사용된 항목을 축출한다.
     */
    private sealed class NamespaceCache {
        private readonly int                                              _maxSize;
        private readonly Dictionary<string, LinkedListNode<CacheEntry>>   _map  = new();
        private readonly LinkedList<CacheEntry>                           _list = new();
        private readonly object                                           _lock = new();

        public NamespaceCache(int maxSize) {
            _maxSize = maxSize > 0 ? maxSize : 1024;
        }

        public object? Get(string key) {
            lock (_lock) {
                if (!_map.TryGetValue(key, out var node)) return null;
                _list.Remove(node);
                _list.AddFirst(node);
                return node.Value.Value;
            }
        }

        public void Put(string key, object value) {
            lock (_lock) {
                if (_map.TryGetValue(key, out var existing)) {
                    _list.Remove(existing);
                    _map.Remove(key);
                }

                var entry = new CacheEntry(key, value);
                var node  = _list.AddFirst(entry);
                _map[key] = node;

                while (_map.Count > _maxSize) {
                    var last = _list.Last;
                    if (last is null) break;
                    _list.RemoveLast();
                    _map.Remove(last.Value.Key);
                }
            }
        }

        public void Clear() {
            lock (_lock) {
                _map.Clear();
                _list.Clear();
            }
        }
    }

    private sealed record CacheEntry(string Key, object Value);
}
