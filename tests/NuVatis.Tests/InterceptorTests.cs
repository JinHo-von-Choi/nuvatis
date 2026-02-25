using System.Data.Common;
using Microsoft.Extensions.Logging;
using NuVatis.Interceptor;
using NuVatis.Statement;

namespace NuVatis.Tests;

/**
 * Interceptor 시스템 테스트.
 * InterceptorPipeline, InterceptorContext, LoggingInterceptor 검증.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public class InterceptorTests {

    [Fact]
    public void Pipeline_ExecuteBefore_CallsAllInterceptorsInOrder() {
        var pipeline = new InterceptorPipeline();
        var order    = new List<string>();

        pipeline.Add(new TestInterceptor("A", order));
        pipeline.Add(new TestInterceptor("B", order));
        pipeline.Add(new TestInterceptor("C", order));

        var ctx = CreateContext();
        pipeline.ExecuteBefore(ctx);

        Assert.Equal(new[] { "A:Before", "B:Before", "C:Before" }, order);
    }

    [Fact]
    public void Pipeline_ExecuteAfter_CallsAllInterceptorsInReverseOrder() {
        var pipeline = new InterceptorPipeline();
        var order    = new List<string>();

        pipeline.Add(new TestInterceptor("A", order));
        pipeline.Add(new TestInterceptor("B", order));
        pipeline.Add(new TestInterceptor("C", order));

        var ctx = CreateContext();
        pipeline.ExecuteAfter(ctx);

        Assert.Equal(new[] { "C:After", "B:After", "A:After" }, order);
    }

    [Fact]
    public async Task Pipeline_ExecuteBeforeAsync_CallsAllInOrder() {
        var pipeline = new InterceptorPipeline();
        var order    = new List<string>();

        pipeline.Add(new TestInterceptor("X", order));
        pipeline.Add(new TestInterceptor("Y", order));

        var ctx = CreateContext();
        await pipeline.ExecuteBeforeAsync(ctx, CancellationToken.None);

        Assert.Equal(new[] { "X:Before", "Y:Before" }, order);
    }

    [Fact]
    public async Task Pipeline_ExecuteAfterAsync_CallsAllInReverseOrder() {
        var pipeline = new InterceptorPipeline();
        var order    = new List<string>();

        pipeline.Add(new TestInterceptor("X", order));
        pipeline.Add(new TestInterceptor("Y", order));

        var ctx = CreateContext();
        await pipeline.ExecuteAfterAsync(ctx, CancellationToken.None);

        Assert.Equal(new[] { "Y:After", "X:After" }, order);
    }

    [Fact]
    public void Pipeline_HasInterceptors_ReturnsFalseWhenEmpty() {
        var pipeline = new InterceptorPipeline();
        Assert.False(pipeline.HasInterceptors);
    }

    [Fact]
    public void Pipeline_HasInterceptors_ReturnsTrueAfterAdd() {
        var pipeline = new InterceptorPipeline();
        pipeline.Add(new TestInterceptor("A", new List<string>()));
        Assert.True(pipeline.HasInterceptors);
    }

    [Fact]
    public void Pipeline_Add_ThrowsOnNull() {
        var pipeline = new InterceptorPipeline();
        Assert.Throws<ArgumentNullException>(() => pipeline.Add(null!));
    }

    [Fact]
    public void Context_SqlCanBeModifiedByBeforeInterceptor() {
        var pipeline = new InterceptorPipeline();
        pipeline.Add(new SqlRewriteInterceptor());

        var ctx = CreateContext("SELECT * FROM users");
        pipeline.ExecuteBefore(ctx);

        Assert.Equal("SELECT * FROM users /* intercepted */", ctx.Sql);
    }

    [Fact]
    public void Context_ExceptionIsAvailableInAfterInterceptor() {
        var pipeline  = new InterceptorPipeline();
        var collector = new ExceptionCollectorInterceptor();
        pipeline.Add(collector);

        var ctx        = CreateContext();
        ctx.Exception = new InvalidOperationException("test error");
        pipeline.ExecuteAfter(ctx);

        Assert.NotNull(collector.CapturedContext);
        Assert.IsType<InvalidOperationException>(collector.CapturedContext!.Exception);
    }

    [Fact]
    public void LoggingInterceptor_BeforeExecute_LogsDebug() {
        var logger      = new TestLogger();
        var interceptor = new LoggingInterceptor(logger);
        var ctx         = CreateContext("SELECT 1");

        interceptor.BeforeExecute(ctx);

        Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, logger.Entries[0].Level);
        Assert.Contains("Executing", logger.Entries[0].Message);
    }

    [Fact]
    public void LoggingInterceptor_AfterExecute_WithException_LogsError() {
        var logger      = new TestLogger();
        var interceptor = new LoggingInterceptor(logger);
        var ctx         = CreateContext();
        ctx.Exception  = new Exception("boom");

        interceptor.AfterExecute(ctx);

        Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, logger.Entries[0].Level);
    }

    [Fact]
    public void LoggingInterceptor_AfterExecute_WithoutException_LogsDebug() {
        var logger      = new TestLogger();
        var interceptor = new LoggingInterceptor(logger);
        var ctx         = CreateContext();
        ctx.ElapsedMilliseconds = 42;
        ctx.AffectedRows        = 3;

        interceptor.AfterExecute(ctx);

        Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, logger.Entries[0].Level);
        Assert.Contains("42ms", logger.Entries[0].Message);
    }

    private static InterceptorContext CreateContext(string sql = "SELECT 1") {
        return new InterceptorContext {
            StatementId   = "test.select",
            Sql           = sql,
            Parameters    = Array.Empty<DbParameter>(),
            Parameter     = null,
            StatementType = StatementType.Select
        };
    }

    private sealed class TestInterceptor : ISqlInterceptor {
        private readonly string _name;
        private readonly List<string> _order;

        public TestInterceptor(string name, List<string> order) {
            _name  = name;
            _order = order;
        }

        public void BeforeExecute(InterceptorContext context)             => _order.Add($"{_name}:Before");
        public void AfterExecute(InterceptorContext context)              => _order.Add($"{_name}:After");
        public Task BeforeExecuteAsync(InterceptorContext context, CancellationToken ct) { BeforeExecute(context); return Task.CompletedTask; }
        public Task AfterExecuteAsync(InterceptorContext context, CancellationToken ct)  { AfterExecute(context);  return Task.CompletedTask; }
    }

    private sealed class SqlRewriteInterceptor : ISqlInterceptor {
        public void BeforeExecute(InterceptorContext context)             => context.Sql += " /* intercepted */";
        public void AfterExecute(InterceptorContext context)              { }
        public Task BeforeExecuteAsync(InterceptorContext context, CancellationToken ct) { BeforeExecute(context); return Task.CompletedTask; }
        public Task AfterExecuteAsync(InterceptorContext context, CancellationToken ct)  => Task.CompletedTask;
    }

    private sealed class ExceptionCollectorInterceptor : ISqlInterceptor {
        public InterceptorContext? CapturedContext { get; private set; }
        public void BeforeExecute(InterceptorContext context)             { }
        public void AfterExecute(InterceptorContext context)              => CapturedContext = context;
        public Task BeforeExecuteAsync(InterceptorContext context, CancellationToken ct) => Task.CompletedTask;
        public Task AfterExecuteAsync(InterceptorContext context, CancellationToken ct)  { AfterExecute(context); return Task.CompletedTask; }
    }

    private sealed class TestLogger : ILogger {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
