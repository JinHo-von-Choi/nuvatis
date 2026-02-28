# Custom Interceptor 작성 가이드

작성자: 최진호
작성일: 2026-03-01

SQL 실행 파이프라인에 횡단 관심사(로깅, 메트릭, 추적, 보안 감사)를 삽입하는 인터셉터 작성법.

---

## ISqlInterceptor 인터페이스

```csharp
public interface ISqlInterceptor
{
    void BeforeExecute(InterceptorContext context);
    void AfterExecute(InterceptorContext context);
    Task BeforeExecuteAsync(InterceptorContext context, CancellationToken ct);
    Task AfterExecuteAsync(InterceptorContext context, CancellationToken ct);
}
```

**실행 순서**

```
[등록 순서]     BeforeExecute(ctx)    → BeforeExecute(ctx)    → DB 실행
                                                                  ↓
[등록 역순]     AfterExecute(ctx)     ← AfterExecute(ctx)     ←
```

예외 발생 시에도 `AfterExecute`/`AfterExecuteAsync`는 반드시 호출된다. `context.Exception`에 예외가 담겨 있다.

---

## InterceptorContext 프로퍼티

| 프로퍼티 | 시점 | 수정 가능 | 설명 |
|----------|------|-----------|------|
| `StatementId` | Before/After | 아니오 | `"Namespace.MethodName"` |
| `Sql` | Before/After | 예 (Before) | 실행될 SQL 문자열 |
| `Parameters` | Before/After | 아니오 | 바인딩된 파라미터 목록 |
| `Parameter` | Before/After | 아니오 | 원본 파라미터 객체 |
| `StatementType` | Before/After | 아니오 | `Select`, `Insert`, `Update`, `Delete` |
| `ElapsedMilliseconds` | After만 | 아니오 | 실행 소요 시간 |
| `AffectedRows` | After만 | 아니오 | DML 영향 행 수 |
| `Exception` | After만 | 아니오 | 예외 객체. `null`이면 정상 실행 |
| `Items` | Before/After | 예 | 인터셉터 간 데이터 전달용 |

---

## 내장 인터셉터 살펴보기

### LoggingInterceptor

```csharp
public sealed class LoggingInterceptor : ISqlInterceptor
{
    private readonly ILogger _logger;

    public LoggingInterceptor(ILogger logger) => _logger = logger;

    public void BeforeExecute(InterceptorContext context)
    {
        _logger.LogDebug(
            "[NuVatis] Executing {StatementId}: {Sql}",
            context.StatementId, context.Sql);
    }

    public void AfterExecute(InterceptorContext context)
    {
        if (context.Exception is not null)
        {
            _logger.LogError(
                context.Exception,
                "[NuVatis] Error executing {StatementId} after {ElapsedMs}ms",
                context.StatementId, context.ElapsedMilliseconds);
        }
        else
        {
            _logger.LogDebug(
                "[NuVatis] Executed {StatementId} in {ElapsedMs}ms. Affected: {Rows}",
                context.StatementId, context.ElapsedMilliseconds, context.AffectedRows);
        }
    }

    public Task BeforeExecuteAsync(InterceptorContext context, CancellationToken ct)
    {
        BeforeExecute(context);
        return Task.CompletedTask;
    }

    public Task AfterExecuteAsync(InterceptorContext context, CancellationToken ct)
    {
        AfterExecute(context);
        return Task.CompletedTask;
    }
}
```

---

## 예제 1: 슬로우 쿼리 감지 인터셉터

지정된 임계값을 초과하는 쿼리를 Warning으로 기록한다.

```csharp
public sealed class SlowQueryInterceptor : ISqlInterceptor
{
    private readonly ILogger _logger;
    private readonly long    _thresholdMs;

    public SlowQueryInterceptor(ILogger<SlowQueryInterceptor> logger, long thresholdMs = 1000)
    {
        _logger      = logger;
        _thresholdMs = thresholdMs;
    }

    public void BeforeExecute(InterceptorContext context) { }

    public void AfterExecute(InterceptorContext context)
    {
        if (context.Exception is not null)
            return;

        if (context.ElapsedMilliseconds >= _thresholdMs)
        {
            _logger.LogWarning(
                "[NuVatis] SLOW QUERY detected: {StatementId} took {ElapsedMs}ms (threshold: {Threshold}ms). SQL: {Sql}",
                context.StatementId,
                context.ElapsedMilliseconds,
                _thresholdMs,
                context.Sql);
        }
    }

    public Task BeforeExecuteAsync(InterceptorContext context, CancellationToken ct)
    {
        BeforeExecute(context);
        return Task.CompletedTask;
    }

    public Task AfterExecuteAsync(InterceptorContext context, CancellationToken ct)
    {
        AfterExecute(context);
        return Task.CompletedTask;
    }
}
```

**등록**

```csharp
builder.Services.AddNuVatis(options => {
    options.AddInterceptor(
        new SlowQueryInterceptor(loggerFactory.CreateLogger<SlowQueryInterceptor>(), thresholdMs: 500));
});
```

---

## 예제 2: 보안 감사 인터셉터

INSERT/UPDATE/DELETE 실행 시 감사 로그를 기록한다.

```csharp
public sealed class AuditInterceptor : ISqlInterceptor
{
    private readonly IAuditLogRepository _auditLog;
    private readonly ICurrentUser        _currentUser;

    public AuditInterceptor(IAuditLogRepository auditLog, ICurrentUser currentUser)
    {
        _auditLog    = auditLog;
        _currentUser = currentUser;
    }

    public void BeforeExecute(InterceptorContext context) { }

    public void AfterExecute(InterceptorContext context)
    {
        if (context.StatementType == StatementType.Select)
            return;
        if (context.Exception is not null)
            return;

        // 비동기 감사 로그 (fire-and-forget)
        _ = _auditLog.RecordAsync(new AuditEntry {
            UserId        = _currentUser.Id,
            StatementId   = context.StatementId,
            Operation     = context.StatementType.ToString(),
            AffectedRows  = context.AffectedRows ?? 0,
            ExecutedAt    = DateTime.UtcNow
        });
    }

    public Task BeforeExecuteAsync(InterceptorContext context, CancellationToken ct)
    {
        BeforeExecute(context);
        return Task.CompletedTask;
    }

    public async Task AfterExecuteAsync(InterceptorContext context, CancellationToken ct)
    {
        if (context.StatementType == StatementType.Select)
            return;
        if (context.Exception is not null)
            return;

        await _auditLog.RecordAsync(new AuditEntry {
            UserId       = _currentUser.Id,
            StatementId  = context.StatementId,
            Operation    = context.StatementType.ToString(),
            AffectedRows = context.AffectedRows ?? 0,
            ExecutedAt   = DateTime.UtcNow
        }, ct);
    }
}
```

---

## 예제 3: Items를 활용한 OpenTelemetry 인터셉터 패턴

`context.Items`로 `BeforeExecute`에서 시작한 `Activity`를 `AfterExecute`에서 종료한다.

```csharp
public sealed class CustomTracingInterceptor : ISqlInterceptor
{
    private const string ActivityKey = "tracing.activity";
    private readonly ActivitySource _source = new("MyApp.Database");

    public void BeforeExecute(InterceptorContext context)
    {
        var activity = _source.StartActivity(
            $"db.{context.StatementType.ToString().ToLower()}",
            ActivityKind.Client);

        if (activity is null)
            return;

        activity.SetTag("db.statement_id", context.StatementId);
        activity.SetTag("db.operation",    context.StatementType.ToString());
        activity.SetTag("db.statement",    context.Sql);

        // AfterExecute에서 사용하기 위해 Items에 저장
        context.Items[ActivityKey] = activity;
    }

    public void AfterExecute(InterceptorContext context)
    {
        if (context.Items.TryGetValue(ActivityKey, out var obj) && obj is Activity activity)
        {
            if (context.Exception is not null)
            {
                activity.SetStatus(ActivityStatusCode.Error, context.Exception.Message);
                activity.RecordException(context.Exception);
            }
            else
            {
                activity.SetTag("db.rows_affected", context.AffectedRows);
                activity.SetStatus(ActivityStatusCode.Ok);
            }

            activity.Dispose();
        }
    }

    public Task BeforeExecuteAsync(InterceptorContext context, CancellationToken ct)
    {
        BeforeExecute(context);
        return Task.CompletedTask;
    }

    public Task AfterExecuteAsync(InterceptorContext context, CancellationToken ct)
    {
        AfterExecute(context);
        return Task.CompletedTask;
    }
}
```

---

## 예제 4: SQL 수정 인터셉터

`BeforeExecute`에서 SQL을 변환한다. 예: 모든 쿼리에 힌트 추가.

```csharp
public sealed class QueryHintInterceptor : ISqlInterceptor
{
    public void BeforeExecute(InterceptorContext context)
    {
        // PostgreSQL: 특정 Statement에만 쿼리 힌트 삽입
        if (context.StatementId.Contains("HeavyReport"))
        {
            context.Sql = $"/*+ SeqScan(large_table) */ {context.Sql}";
        }
    }

    public void AfterExecute(InterceptorContext context) { }

    public Task BeforeExecuteAsync(InterceptorContext context, CancellationToken ct)
    {
        BeforeExecute(context);
        return Task.CompletedTask;
    }

    public Task AfterExecuteAsync(InterceptorContext context, CancellationToken ct)
    {
        AfterExecute(context);
        return Task.CompletedTask;
    }
}
```

---

## 예제 5: Rate Limiting 인터셉터

특정 Statement의 호출 빈도를 제한한다.

```csharp
public sealed class RateLimitInterceptor : ISqlInterceptor
{
    private readonly ConcurrentDictionary<string, long> _callCount = new();
    private readonly ConcurrentDictionary<string, long> _windowStart = new();
    private readonly long _maxCallsPerMinute;

    public RateLimitInterceptor(long maxCallsPerMinute = 1000)
    {
        _maxCallsPerMinute = maxCallsPerMinute;
    }

    public void BeforeExecute(InterceptorContext context)
    {
        var now    = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var window = now / 60;  // 1분 윈도우

        _windowStart.AddOrUpdate(
            context.StatementId, window,
            (_, old) => {
                if (old != window) {
                    _callCount[context.StatementId] = 0;  // 윈도우 초기화
                    return window;
                }
                return old;
            });

        var count = _callCount.AddOrUpdate(context.StatementId, 1, (_, c) => c + 1);

        if (count > _maxCallsPerMinute)
        {
            throw new InvalidOperationException(
                $"Rate limit exceeded for statement '{context.StatementId}'. " +
                $"Max {_maxCallsPerMinute} calls/min.");
        }
    }

    public void AfterExecute(InterceptorContext context) { }

    public Task BeforeExecuteAsync(InterceptorContext context, CancellationToken ct)
    {
        BeforeExecute(context);
        return Task.CompletedTask;
    }

    public Task AfterExecuteAsync(InterceptorContext context, CancellationToken ct)
    {
        AfterExecute(context);
        return Task.CompletedTask;
    }
}
```

---

## 등록 및 실행 순서

```csharp
builder.Services.AddNuVatis(options => {
    // 등록 순서: Logging → Metrics → Tracing
    options.AddInterceptor(new LoggingInterceptor(logger));
    options.AddInterceptor(new MetricsInterceptor());
    options.AddInterceptor(new CustomTracingInterceptor());
});
```

**실행 순서**

```
Before: LoggingInterceptor.BeforeExecute
Before: MetricsInterceptor.BeforeExecute
Before: CustomTracingInterceptor.BeforeExecute
  → DB 쿼리 실행
After:  CustomTracingInterceptor.AfterExecute   (역순)
After:  MetricsInterceptor.AfterExecute
After:  LoggingInterceptor.AfterExecute
```

---

## 주의사항

1. **Thread Safety**: 인터셉터는 싱글턴으로 등록된다. 상태(필드)를 갖는 경우 반드시 Thread-safe하게 설계한다.
2. **예외 처리**: `BeforeExecute`/`AfterExecute`에서 예외가 발생하면 쿼리 실행이 중단된다. 인터셉터 내부 예외를 감싸서 전파를 막는 방어 코드를 작성한다.
3. **AfterExecute 보장**: DB 쿼리 실패 시에도 `AfterExecute`는 호출된다. `context.Exception`으로 성공/실패를 구분한다.
4. **비동기 일관성**: `BeforeExecuteAsync`와 `AfterExecuteAsync`는 비동기 경로에서 호출된다. 동기 메서드를 그대로 호출하거나 실제 비동기 작업으로 구현한다.
5. **Items 키 충돌**: 여러 인터셉터가 같은 키를 사용하면 덮어쓰기가 발생한다. 고유한 키(예: 클래스명 접두사)를 사용한다.
