using System.Diagnostics;
using NuVatis.Extensions.OpenTelemetry;
using NuVatis.Interceptor;
using NuVatis.Statement;

namespace NuVatis.Tests;

/**
 * OpenTelemetryInterceptor (Phase 6.3 C-1) 테스트.
 * ActivityListener로 Activity 생성/종료/태그를 검증한다.
 *
 * @author 최진호
 * @date   2026-02-25
 */
public class OpenTelemetryInterceptorTests : IDisposable {

    private readonly OpenTelemetryInterceptor _interceptor;
    private readonly ActivityListener         _activityListener;
    private readonly List<Activity>           _startedActivities = new();
    private readonly List<Activity>           _stoppedActivities = new();

    public OpenTelemetryInterceptorTests() {
        _interceptor = new OpenTelemetryInterceptor("NuVatis.Test.OTel");

        _activityListener = new ActivityListener {
            ShouldListenTo = source => source.Name == "NuVatis.Test.OTel",
            Sample         = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => _startedActivities.Add(activity),
            ActivityStopped = activity => _stoppedActivities.Add(activity)
        };

        ActivitySource.AddActivityListener(_activityListener);
    }

    [Fact]
    public void BeforeExecute_StartsActivity() {
        var ctx = CreateContext("User.SelectAll", StatementType.Select);

        _interceptor.BeforeExecute(ctx);

        Assert.Single(_startedActivities);
        var activity = _startedActivities[0];
        Assert.Equal("User.SelectAll", activity.OperationName);
        Assert.Equal(ActivityKind.Client, activity.Kind);

        Assert.True(ctx.Items.ContainsKey("__nuvatis_otel_activity"));

        _interceptor.AfterExecute(ctx);
    }

    [Fact]
    public void AfterExecute_StopsActivity_WithOkStatus() {
        var ctx = CreateContext("User.SelectAll", StatementType.Select);

        _interceptor.BeforeExecute(ctx);
        ctx.ElapsedMilliseconds = 50;
        _interceptor.AfterExecute(ctx);

        Assert.Single(_stoppedActivities);
        var activity = _stoppedActivities[0];
        Assert.Equal(ActivityStatusCode.Ok, activity.Status);
        Assert.False(ctx.Items.ContainsKey("__nuvatis_otel_activity"));
    }

    [Fact]
    public void AfterExecute_WithException_SetsErrorStatus() {
        var ctx = CreateContext("User.Insert", StatementType.Insert);
        ctx.Exception = new InvalidOperationException("Duplicate key");

        _interceptor.BeforeExecute(ctx);
        ctx.ElapsedMilliseconds = 10;
        _interceptor.AfterExecute(ctx);

        Assert.Single(_stoppedActivities);
        var activity = _stoppedActivities[0];
        Assert.Equal(ActivityStatusCode.Error, activity.Status);

        var errorTag = activity.Tags.FirstOrDefault(t => t.Key == "error.type");
        Assert.NotNull(errorTag.Value);
        Assert.Contains("InvalidOperationException", errorTag.Value);
    }

    [Fact]
    public void Activity_HasCorrectTags() {
        var ctx = CreateContext("Report.Monthly", StatementType.Select);

        _interceptor.BeforeExecute(ctx);
        ctx.AffectedRows = null;
        _interceptor.AfterExecute(ctx);

        var activity = _stoppedActivities[0];
        Assert.Equal("sql", activity.Tags.First(t => t.Key == "db.system").Value);
        Assert.Equal("SELECT SUM(amount) FROM orders", activity.Tags.First(t => t.Key == "db.statement").Value);
        Assert.Equal("Report.Monthly", activity.Tags.First(t => t.Key == "db.statement.id").Value);
        Assert.Equal("select", activity.Tags.First(t => t.Key == "db.operation").Value);
    }

    [Fact]
    public void AfterExecute_WithAffectedRows_SetsTag() {
        var ctx = CreateContext("User.Update", StatementType.Update);

        _interceptor.BeforeExecute(ctx);
        ctx.AffectedRows        = 42;
        ctx.ElapsedMilliseconds = 100;
        _interceptor.AfterExecute(ctx);

        var activity = _stoppedActivities[0];
        var rowsValue = activity.GetTagItem("nuvatis.affected_rows");
        Assert.NotNull(rowsValue);
        Assert.Equal(42, rowsValue);
    }

    [Fact]
    public async Task AsyncFlow_WorksCorrectly() {
        var ctx = CreateContext("User.SelectAsync", StatementType.Select);

        await _interceptor.BeforeExecuteAsync(ctx, CancellationToken.None);
        ctx.ElapsedMilliseconds = 30;
        await _interceptor.AfterExecuteAsync(ctx, CancellationToken.None);

        Assert.Single(_startedActivities);
        Assert.Single(_stoppedActivities);
        Assert.Equal(ActivityStatusCode.Ok, _stoppedActivities[0].Status);
    }

    [Fact]
    public void SourceName_ReturnsConfiguredName() {
        Assert.Equal("NuVatis.Test.OTel", _interceptor.SourceName);
    }

    [Fact]
    public void AfterExecute_WithoutBefore_IsNoOp() {
        var ctx = CreateContext("User.Select", StatementType.Select);

        _interceptor.AfterExecute(ctx);

        Assert.Empty(_stoppedActivities);
    }

    private static InterceptorContext CreateContext(string statementId, StatementType type) {
        return new InterceptorContext {
            StatementId   = statementId,
            Sql           = "SELECT SUM(amount) FROM orders",
            Parameters    = Array.Empty<System.Data.Common.DbParameter>(),
            StatementType = type
        };
    }

    public void Dispose() {
        _activityListener.Dispose();
        _interceptor.Dispose();
    }
}
