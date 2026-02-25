using System.Diagnostics.Metrics;

namespace NuVatis.Interceptor;

/**
 * Prometheus/OpenTelemetry Metrics 수집 인터셉터.
 * System.Diagnostics.Metrics (.NET 8 빌트인)를 사용하여 외부 의존성 없이
 * 쿼리 실행 횟수, 응답 시간, 에러율을 계측한다.
 *
 * Meter 이름: "NuVatis"
 * 메트릭:
 *   nuvatis.query.total         - Counter (statement_id, statement_type)
 *   nuvatis.query.duration      - Histogram (statement_id) [seconds]
 *   nuvatis.query.errors.total  - Counter (statement_id, error_type)
 *
 * ASP.NET Core의 OpenTelemetry Metrics exporter 또는
 * prometheus-net의 MeterAdapter와 자동 통합된다.
 *
 * @author 최진호
 * @date   2026-02-25
 */
public sealed class MetricsInterceptor : ISqlInterceptor, IDisposable {

    private readonly Meter      _meter;
    private readonly Counter<long>    _queryTotal;
    private readonly Counter<long>    _errorTotal;
    private readonly Histogram<double> _queryDuration;

    /**
     * @param meterName Meter 이름 (기본: "NuVatis")
     */
    public MetricsInterceptor(string meterName = "NuVatis") {
        _meter         = new Meter(meterName);
        _queryTotal    = _meter.CreateCounter<long>(
            "nuvatis.query.total",
            description: "Total number of SQL queries executed");
        _errorTotal    = _meter.CreateCounter<long>(
            "nuvatis.query.errors.total",
            description: "Total number of SQL query errors");
        _queryDuration = _meter.CreateHistogram<double>(
            "nuvatis.query.duration",
            unit: "s",
            description: "SQL query execution duration in seconds");
    }

    public void BeforeExecute(InterceptorContext context) { }

    public void AfterExecute(InterceptorContext context) {
        RecordMetrics(context);
    }

    public Task BeforeExecuteAsync(InterceptorContext context, CancellationToken ct) {
        return Task.CompletedTask;
    }

    public Task AfterExecuteAsync(InterceptorContext context, CancellationToken ct) {
        RecordMetrics(context);
        return Task.CompletedTask;
    }

    private void RecordMetrics(InterceptorContext context) {
        var stmtIdTag   = new KeyValuePair<string, object?>("statement_id", context.StatementId);
        var stmtTypeTag = new KeyValuePair<string, object?>("statement_type", context.StatementType.ToString().ToLowerInvariant());

        _queryTotal.Add(1, stmtIdTag, stmtTypeTag);
        _queryDuration.Record(context.ElapsedMilliseconds / 1000.0, stmtIdTag);

        if (context.Exception is not null) {
            _errorTotal.Add(1,
                stmtIdTag,
                new KeyValuePair<string, object?>("error_type", context.Exception.GetType().Name));
        }
    }

    public void Dispose() {
        _meter.Dispose();
    }
}
