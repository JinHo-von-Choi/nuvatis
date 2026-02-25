using Microsoft.Extensions.Logging;

namespace NuVatis.Interceptor;

/**
 * 빌트인 SQL 로깅 인터셉터.
 * BeforeExecute에서 SQL 출력, AfterExecute에서 소요 시간/에러를 기록한다.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public sealed class LoggingInterceptor : ISqlInterceptor {
    private readonly ILogger _logger;

    public LoggingInterceptor(ILogger logger) {
        _logger = logger;
    }

    public void BeforeExecute(InterceptorContext context) {
        _logger.LogDebug(
            "[NuVatis] Executing {StatementId}: {Sql}",
            context.StatementId, context.Sql);
    }

    public void AfterExecute(InterceptorContext context) {
        if (context.Exception is not null) {
            _logger.LogError(
                context.Exception,
                "[NuVatis] Error executing {StatementId} after {ElapsedMs}ms",
                context.StatementId, context.ElapsedMilliseconds);
        } else {
            _logger.LogDebug(
                "[NuVatis] Executed {StatementId} in {ElapsedMs}ms. Affected: {Rows}",
                context.StatementId, context.ElapsedMilliseconds, context.AffectedRows);
        }
    }

    public Task BeforeExecuteAsync(InterceptorContext context, CancellationToken ct) {
        BeforeExecute(context);
        return Task.CompletedTask;
    }

    public Task AfterExecuteAsync(InterceptorContext context, CancellationToken ct) {
        AfterExecute(context);
        return Task.CompletedTask;
    }
}
