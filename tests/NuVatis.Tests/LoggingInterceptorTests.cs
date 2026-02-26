using Microsoft.Extensions.Logging;
using NuVatis.Interceptor;
using NuVatis.Statement;
using Xunit;

namespace NuVatis.Tests;

/**
 * LoggingInterceptor 단위 테스트.
 *
 * @author 최진호
 * @date   2026-02-26
 */
public class LoggingInterceptorTests {

    private sealed class TestLogger : ILogger {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            Messages.Add($"[{logLevel}] {formatter(state, exception)}");
        }
    }

    private static InterceptorContext CreateContext(Exception? ex = null) {
        return new InterceptorContext {
            StatementId         = "test.selectAll",
            Sql                 = "SELECT * FROM users",
            Parameters          = Array.Empty<System.Data.Common.DbParameter>(),
            Parameter           = null,
            StatementType       = StatementType.Select,
            ElapsedMilliseconds = 42,
            AffectedRows        = 10,
            Exception           = ex
        };
    }

    [Fact]
    public void BeforeExecute_Logs_Debug() {
        var logger      = new TestLogger();
        var interceptor = new LoggingInterceptor(logger);
        var ctx         = CreateContext();

        interceptor.BeforeExecute(ctx);

        Assert.Single(logger.Messages);
        Assert.Contains("test.selectAll", logger.Messages[0]);
        Assert.Contains("[Debug]", logger.Messages[0]);
    }

    [Fact]
    public void AfterExecute_Success_Logs_Debug() {
        var logger      = new TestLogger();
        var interceptor = new LoggingInterceptor(logger);
        var ctx         = CreateContext();

        interceptor.AfterExecute(ctx);

        Assert.Single(logger.Messages);
        Assert.Contains("42ms", logger.Messages[0]);
        Assert.Contains("[Debug]", logger.Messages[0]);
    }

    [Fact]
    public void AfterExecute_Error_Logs_Error() {
        var logger      = new TestLogger();
        var interceptor = new LoggingInterceptor(logger);
        var ctx         = CreateContext(new InvalidOperationException("DB error"));

        interceptor.AfterExecute(ctx);

        Assert.Single(logger.Messages);
        Assert.Contains("[Error]", logger.Messages[0]);
        Assert.Contains("test.selectAll", logger.Messages[0]);
    }

    [Fact]
    public async Task BeforeExecuteAsync_Calls_Sync() {
        var logger      = new TestLogger();
        var interceptor = new LoggingInterceptor(logger);
        var ctx         = CreateContext();

        await interceptor.BeforeExecuteAsync(ctx, CancellationToken.None);

        Assert.Single(logger.Messages);
    }

    [Fact]
    public async Task AfterExecuteAsync_Calls_Sync() {
        var logger      = new TestLogger();
        var interceptor = new LoggingInterceptor(logger);
        var ctx         = CreateContext();

        await interceptor.AfterExecuteAsync(ctx, CancellationToken.None);

        Assert.Single(logger.Messages);
    }
}
