using System.Diagnostics.CodeAnalysis;

namespace NuVatis.Mapping;

/**
 * 지연 로딩 래퍼. 첫 Value 접근 시 valueFactory를 실행하여 결과를 캐싱한다.
 * Thread-safe: LazyThreadSafetyMode.ExecutionAndPublication 적용.
 *
 * @author 최진호
 * @date   2026-02-24
 */
[UnconditionalSuppressMessage("AOT", "IL2091",
    Justification = "LazyValue<T>는 Func<T>로 값을 생성하므로 기본 생성자가 불필요하다.")]
public sealed class LazyValue<T> {
    private readonly Lazy<T> _lazy;

    public LazyValue(Func<T> valueFactory) {
        _lazy = new Lazy<T>(valueFactory, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public T Value         => _lazy.Value;
    public bool IsLoaded   => _lazy.IsValueCreated;

    public static implicit operator T(LazyValue<T> lazyValue) => lazyValue.Value;

    public override string? ToString() => IsLoaded ? Value?.ToString() : "[Not Loaded]";
}
