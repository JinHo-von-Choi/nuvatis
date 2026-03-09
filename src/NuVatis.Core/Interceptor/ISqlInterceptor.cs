namespace NuVatis.Interceptor;

/**
 * SQL 실행 전후에 로직을 삽입하는 인터셉터 인터페이스.
 * BeforeExecute에서 SQL/파라미터 수정, AfterExecute에서 로깅/메트릭 수집 등 활용.
 *
 * @author 최진호
 * @date   2026-02-24
 */
/// <summary>SQL 실행 전후에 로직을 삽입하는 인터셉터 인터페이스.</summary>
public interface ISqlInterceptor {
    /// <summary>SQL 실행 직전에 동기적으로 호출된다. SQL과 파라미터 수정에 활용할 수 있다.</summary>
    void BeforeExecute(InterceptorContext context);
    /// <summary>SQL 실행 완료 후 동기적으로 호출된다. 로깅, 메트릭 수집 등에 활용할 수 있다.</summary>
    void AfterExecute(InterceptorContext context);
    /// <summary>SQL 실행 직전에 비동기적으로 호출된다.</summary>
    Task BeforeExecuteAsync(InterceptorContext context, CancellationToken ct);
    /// <summary>SQL 실행 완료 후 비동기적으로 호출된다.</summary>
    Task AfterExecuteAsync(InterceptorContext context, CancellationToken ct);
}
