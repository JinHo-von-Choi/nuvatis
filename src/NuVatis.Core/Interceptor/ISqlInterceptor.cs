namespace NuVatis.Interceptor;

/**
 * SQL 실행 전후에 로직을 삽입하는 인터셉터 인터페이스.
 * BeforeExecute에서 SQL/파라미터 수정, AfterExecute에서 로깅/메트릭 수집 등 활용.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public interface ISqlInterceptor {
    void BeforeExecute(InterceptorContext context);
    void AfterExecute(InterceptorContext context);
    Task BeforeExecuteAsync(InterceptorContext context, CancellationToken ct);
    Task AfterExecuteAsync(InterceptorContext context, CancellationToken ct);
}
