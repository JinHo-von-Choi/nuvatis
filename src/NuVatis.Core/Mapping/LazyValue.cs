using System.Diagnostics.CodeAnalysis;

namespace NuVatis.Mapping;

/**
 * 지연 로딩 래퍼. 첫 Value 접근 시 valueFactory를 실행하여 결과를 캐싱한다.
 * Thread-safe: LazyThreadSafetyMode.ExecutionAndPublication 적용.
 *
 * @author 최진호
 * @date   2026-02-24
 */
/// <summary>지연 로딩 래퍼. 첫 <see cref="Value"/> 접근 시 valueFactory를 실행하여 결과를 캐싱한다. Thread-safe.</summary>
[UnconditionalSuppressMessage("AOT", "IL2091",
    Justification = "LazyValue<T>는 Func<T>로 값을 생성하므로 기본 생성자가 불필요하다.")]
public sealed class LazyValue<T> {
    private readonly Lazy<T> _lazy;

    /// <summary>지정한 팩토리 함수로 LazyValue를 초기화한다. 팩토리는 첫 Value 접근 시 한 번만 실행된다.</summary>
    public LazyValue(Func<T> valueFactory) {
        _lazy = new Lazy<T>(valueFactory, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>Gets the lazily-initialized value. 첫 접근 시 팩토리가 실행되며 이후 캐시된 값이 반환된다.</summary>
    public T Value         => _lazy.Value;
    /// <summary>Gets a value indicating whether the value has already been initialized.</summary>
    public bool IsLoaded   => _lazy.IsValueCreated;

    /// <summary>LazyValue&lt;T&gt;를 T로 암묵적 변환한다. Value 프로퍼티를 호출하는 것과 동일하다.</summary>
    public static implicit operator T(LazyValue<T> lazyValue) => lazyValue.Value;

    /// <inheritdoc/>
    public override string? ToString() => IsLoaded ? Value?.ToString() : "[Not Loaded]";
}
