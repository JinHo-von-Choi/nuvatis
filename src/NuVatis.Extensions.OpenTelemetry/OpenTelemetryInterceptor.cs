using System.Diagnostics;
using NuVatis.Interceptor;

namespace NuVatis.Extensions.OpenTelemetry;

/**
 * OpenTelemetry 분산 추적 인터셉터.
 * SQL 실행을 Activity/Span으로 기록하여 Jaeger, Zipkin 등에서 추적 가능.
 *
 * ActivitySource 이름: "NuVatis.SqlSession" (기본)
 * 사용자 애플리케이션에서 AddSource("NuVatis.SqlSession")으로 등록해야 한다.
 *
 * 기록 태그:
 *   db.system        - "sql" (고정)
 *   db.statement     - 실행 SQL
 *   db.statement.id  - Statement ID (Namespace.Id)
 *   db.operation     - StatementType (select, insert, update, delete)
 *   nuvatis.affected_rows - 영향 행수 (DML 시)
 *   otel.status_code - ERROR (예외 발생 시)
 *   error.type       - 예외 타입명
 *
 * @author 최진호
 * @date   2026-02-25
 */
public sealed class OpenTelemetryInterceptor : ISqlInterceptor, IDisposable {

    private const string ActivityKey = "__nuvatis_otel_activity";

    private readonly ActivitySource _source;

    /**
     * @param sourceName ActivitySource 이름 (기본: "NuVatis.SqlSession")
     */
    public OpenTelemetryInterceptor(string sourceName = "NuVatis.SqlSession") {
        _source = new ActivitySource(sourceName);
    }

    /** ActivitySource 이름. AddSource() 등록 시 사용. */
    public string SourceName => _source.Name;

    public void BeforeExecute(InterceptorContext context) {
        StartActivity(context);
    }

    public void AfterExecute(InterceptorContext context) {
        StopActivity(context);
    }

    public Task BeforeExecuteAsync(InterceptorContext context, CancellationToken ct) {
        StartActivity(context);
        return Task.CompletedTask;
    }

    public Task AfterExecuteAsync(InterceptorContext context, CancellationToken ct) {
        StopActivity(context);
        return Task.CompletedTask;
    }

    private void StartActivity(InterceptorContext context) {
        var activity = _source.StartActivity(
            context.StatementId,
            ActivityKind.Client);

        if (activity is null) return;

        activity.SetTag("db.system", "sql");
        activity.SetTag("db.statement", context.Sql);
        activity.SetTag("db.statement.id", context.StatementId);
        activity.SetTag("db.operation", context.StatementType.ToString().ToLowerInvariant());

        context.Items[ActivityKey] = activity;
    }

    private static void StopActivity(InterceptorContext context) {
        if (!context.Items.TryGetValue(ActivityKey, out var value) || value is not Activity activity) {
            return;
        }

        if (context.Exception is not null) {
            activity.SetStatus(ActivityStatusCode.Error, context.Exception.Message);
            activity.SetTag("otel.status_code", "ERROR");
            activity.SetTag("error.type", context.Exception.GetType().FullName);
        } else {
            activity.SetStatus(ActivityStatusCode.Ok);
            if (context.AffectedRows.HasValue) {
                activity.SetTag("nuvatis.affected_rows", context.AffectedRows.Value);
            }
        }

        activity.Dispose();
        context.Items.Remove(ActivityKey);
    }

    public void Dispose() {
        _source.Dispose();
    }
}
