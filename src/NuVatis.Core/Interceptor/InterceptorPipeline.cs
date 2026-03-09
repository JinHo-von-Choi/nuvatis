namespace NuVatis.Interceptor;

/**
 * 등록된 ISqlInterceptor들을 순차 실행하는 파이프라인.
 * Before는 등록 순서대로, After는 역순으로 호출한다 (스택 구조).
 *
 * @author 최진호
 * @date   2026-02-24
 */
public sealed class InterceptorPipeline {
    private readonly List<ISqlInterceptor> _interceptors = new();

    /// <summary>등록된 인터셉터가 하나 이상 있는지 여부.</summary>
    public bool HasInterceptors => _interceptors.Count > 0;

    /// <summary>인터셉터를 파이프라인에 추가한다.</summary>
    public void Add(ISqlInterceptor interceptor) {
        ArgumentNullException.ThrowIfNull(interceptor);
        _interceptors.Add(interceptor);
    }

    /// <summary>등록 순서대로 모든 인터셉터의 BeforeExecute를 호출한다.</summary>
    public void ExecuteBefore(InterceptorContext ctx) {
        foreach (var interceptor in _interceptors) {
            interceptor.BeforeExecute(ctx);
        }
    }

    /// <summary>역순으로 모든 인터셉터의 AfterExecute를 호출한다.</summary>
    public void ExecuteAfter(InterceptorContext ctx) {
        for (var i = _interceptors.Count - 1; i >= 0; i--) {
            _interceptors[i].AfterExecute(ctx);
        }
    }

    /// <summary>등록 순서대로 모든 인터셉터의 BeforeExecuteAsync를 호출한다.</summary>
    public async Task ExecuteBeforeAsync(InterceptorContext ctx, CancellationToken ct) {
        foreach (var interceptor in _interceptors) {
            await interceptor.BeforeExecuteAsync(ctx, ct).ConfigureAwait(false);
        }
    }

    /// <summary>역순으로 모든 인터셉터의 AfterExecuteAsync를 호출한다.</summary>
    public async Task ExecuteAfterAsync(InterceptorContext ctx, CancellationToken ct) {
        for (var i = _interceptors.Count - 1; i >= 0; i--) {
            await _interceptors[i].AfterExecuteAsync(ctx, ct).ConfigureAwait(false);
        }
    }
}
