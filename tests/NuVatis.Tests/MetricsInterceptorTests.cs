using System.Diagnostics.Metrics;
using NuVatis.Interceptor;
using NuVatis.Statement;

namespace NuVatis.Tests;

/**
 * MetricsInterceptor (Phase 6.3 C-2) 테스트.
 * System.Diagnostics.Metrics의 MeterListener로 메트릭 수집을 검증한다.
 *
 * @author 최진호
 * @date   2026-02-25
 */
public class MetricsInterceptorTests : IDisposable {

    private readonly MetricsInterceptor _interceptor;
    private readonly MeterListener     _listener;

    private readonly List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)>    _counters   = new();
    private readonly List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)> _histograms = new();

    public MetricsInterceptorTests() {
        _interceptor = new MetricsInterceptor("NuVatis.Test");
        _listener    = new MeterListener();

        _listener.InstrumentPublished = (instrument, listener) => {
            if (instrument.Meter.Name == "NuVatis.Test") {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) => {
            _counters.Add((instrument.Name, measurement, tags.ToArray()));
        });

        _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) => {
            _histograms.Add((instrument.Name, measurement, tags.ToArray()));
        });

        _listener.Start();
    }

    [Fact]
    public void AfterExecute_RecordsQueryTotalCounter() {
        var ctx = CreateContext("User.SelectAll", StatementType.Select, elapsed: 50);

        _interceptor.AfterExecute(ctx);

        var counter = _counters.Find(c => c.Name == "nuvatis.query.total");
        Assert.NotEqual(default, counter);
        Assert.Equal(1, counter.Value);
        Assert.Contains(counter.Tags, t => t.Key == "statement_id" && (string)t.Value! == "User.SelectAll");
        Assert.Contains(counter.Tags, t => t.Key == "statement_type" && (string)t.Value! == "select");
    }

    [Fact]
    public void AfterExecute_RecordsDurationHistogram() {
        var ctx = CreateContext("User.SelectAll", StatementType.Select, elapsed: 150);

        _interceptor.AfterExecute(ctx);

        var hist = _histograms.Find(h => h.Name == "nuvatis.query.duration");
        Assert.NotEqual(default, hist);
        Assert.Equal(0.15, hist.Value, precision: 3);
    }

    [Fact]
    public void AfterExecute_WithException_RecordsErrorCounter() {
        var ctx = CreateContext("User.Insert", StatementType.Insert, elapsed: 10);
        ctx.Exception = new InvalidOperationException("Duplicate key");

        _interceptor.AfterExecute(ctx);

        var error = _counters.Find(c => c.Name == "nuvatis.query.errors.total");
        Assert.NotEqual(default, error);
        Assert.Equal(1, error.Value);
        Assert.Contains(error.Tags, t => t.Key == "error_type" && (string)t.Value! == "InvalidOperationException");
    }

    [Fact]
    public void AfterExecute_WithoutException_NoErrorCounter() {
        var ctx = CreateContext("User.SelectAll", StatementType.Select, elapsed: 5);

        _interceptor.AfterExecute(ctx);

        var error = _counters.Find(c => c.Name == "nuvatis.query.errors.total");
        Assert.Equal(default, error);
    }

    [Fact]
    public async Task AfterExecuteAsync_RecordsMetrics() {
        var ctx = CreateContext("User.Update", StatementType.Update, elapsed: 200);
        ctx.AffectedRows = 5;

        await _interceptor.AfterExecuteAsync(ctx, CancellationToken.None);

        Assert.Contains(_counters, c => c.Name == "nuvatis.query.total");
        Assert.Contains(_histograms, h => h.Name == "nuvatis.query.duration");
    }

    [Fact]
    public void MultipleExecutions_CounterIncrements() {
        for (var i = 0; i < 5; i++) {
            _interceptor.AfterExecute(
                CreateContext("User.Count", StatementType.Select, elapsed: 10));
        }

        var totalCount = _counters.Where(c => c.Name == "nuvatis.query.total").Sum(c => c.Value);
        Assert.Equal(5, totalCount);
    }

    [Fact]
    public void BeforeExecute_IsNoOp() {
        var ctx = CreateContext("User.Select", StatementType.Select, elapsed: 0);

        _interceptor.BeforeExecute(ctx);

        Assert.Empty(_counters);
        Assert.Empty(_histograms);
    }

    private static InterceptorContext CreateContext(
        string statementId, StatementType type, long elapsed) {
        return new InterceptorContext {
            StatementId         = statementId,
            Sql                 = "SELECT 1",
            Parameters          = Array.Empty<System.Data.Common.DbParameter>(),
            StatementType       = type,
            ElapsedMilliseconds = elapsed
        };
    }

    public void Dispose() {
        _listener.Dispose();
        _interceptor.Dispose();
    }
}
