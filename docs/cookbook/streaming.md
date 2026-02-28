# Streaming & Multi-ResultSet Cookbook

작성자: 최진호
작성일: 2026-03-01

대용량 데이터 스트리밍과 단일 쿼리에서 여러 결과셋을 소비하는 방법.

---

## Streaming (IAsyncEnumerable)

### 언제 사용하는가

`SelectList<T>`는 전체 결과를 메모리에 적재한다. 수십만 건의 데이터를 처리할 때 OOM(Out of Memory)이 발생할 수 있다. `SelectStream<T>`은 한 번에 한 행씩 처리하여 메모리 사용량을 일정하게 유지한다.

| | SelectList | SelectStream |
|---|---|---|
| 메모리 사용 | 전체 결과 × 객체 크기 | O(1) - 단일 행만 |
| 처리 방식 | 전체 로드 후 처리 | 로드하면서 즉시 처리 |
| 취소 지원 | 제한적 | CancellationToken 완전 지원 |
| 적합한 케이스 | 수천 건 이하, 정렬/필터링 필요 | 수만 건 이상, 순차 처리 |

### 기본 사용법

**C# 인터페이스**

```csharp
[NuVatisMapper]
public interface ILogMapper {
    IAsyncEnumerable<LogEntry> StreamAll(CancellationToken ct = default);
    IAsyncEnumerable<LogEntry> StreamByDateRange(DateRangeParam param, CancellationToken ct = default);
}
```

**XML 매퍼**

```xml
<select id="StreamAll" resultType="LogEntry">
  SELECT id, message, level, source, created_at
  FROM logs
  ORDER BY created_at
</select>

<select id="StreamByDateRange" resultType="LogEntry">
  SELECT id, message, level, source, created_at
  FROM logs
  WHERE created_at BETWEEN #{Start} AND #{End}
  ORDER BY created_at
</select>
```

**소비**

```csharp
await foreach (var entry in mapper.StreamAll(cancellationToken))
{
    await ProcessLogAsync(entry);
}
```

### 파이프라인 처리

`IAsyncEnumerable`은 LINQ와 결합하여 스트리밍 파이프라인을 구성할 수 있다.

```csharp
// System.Linq.Async 패키지 사용
var errorCount = await mapper
    .StreamAll(ct)
    .Where(e => e.Level == "ERROR")
    .CountAsync(ct);

// 배치 단위로 처리
await foreach (var batch in mapper.StreamAll(ct).Buffer(1000))
{
    await BulkInsertElasticsearchAsync(batch);
}
```

### 취소 처리

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

try
{
    await foreach (var row in session.SelectStream<ReportRow>(
        "Reports.GetAll",
        param,
        cts.Token))
    {
        await writer.WriteAsync(row, cts.Token);
    }
}
catch (OperationCanceledException)
{
    // 타임아웃 또는 외부 취소
    logger.LogWarning("Streaming cancelled after {Elapsed}", stopwatch.Elapsed);
}
```

### 직접 API (Mapper 없이)

```csharp
using var session = factory.OpenSession(autoCommit: true);

await foreach (var row in session.SelectStream<StatRow>(
    "Stats.GetAll",
    new { Since = startDate },
    cancellationToken))
{
    await ProcessAsync(row);
}
```

### 성능 튜닝

**fetchSize**: DB 드라이버에서 한 번에 가져올 행 수를 늘려 라운드트립을 줄인다.

```xml
<select id="StreamAll" resultType="LogEntry" fetchSize="5000">
  SELECT * FROM logs ORDER BY created_at
</select>
```

**병렬 처리**: 스트리밍 결과를 `Channel<T>`로 넘겨 병렬 소비한다.

```csharp
var channel = Channel.CreateBounded<LogEntry>(capacity: 10000);

// 생산자
var producer = Task.Run(async () => {
    await foreach (var entry in mapper.StreamAll(ct))
    {
        await channel.Writer.WriteAsync(entry, ct);
    }
    channel.Writer.Complete();
}, ct);

// 소비자 (병렬)
var consumers = Enumerable.Range(0, 4).Select(_ => Task.Run(async () => {
    await foreach (var entry in channel.Reader.ReadAllAsync(ct))
    {
        await ProcessAsync(entry);
    }
}, ct)).ToArray();

await Task.WhenAll(producer);
await Task.WhenAll(consumers);
```

---

## Multi-ResultSet

단일 SQL 실행에서 여러 결과셋을 순차적으로 소비한다. 대시보드처럼 여러 집계 데이터를 한 번의 DB 라운드트립으로 가져올 때 유용하다.

### 지원 DB

| DB | 지원 여부 |
|----|----------|
| PostgreSQL | 예 |
| MySQL / MariaDB | 예 |
| SQL Server | 예 |
| SQLite | **아니오** (드라이버 제한) |

### 기본 사용법

**XML 매퍼 (여러 SELECT를 세미콜론으로 구분)**

```xml
<select id="GetDashboard" resultType="string">
  SELECT COUNT(*) AS total    FROM users WHERE is_active = true;
  SELECT COUNT(*) AS new_today FROM users WHERE created_at >= CURRENT_DATE;
  SELECT AVG(amount) AS avg_order FROM orders WHERE DATE(created_at) = CURRENT_DATE;
</select>
```

**C# 인터페이스**

```csharp
[NuVatisMapper]
public interface IDashboardMapper {
    Task<ResultSetGroup> GetDashboardAsync(DashboardParam param, CancellationToken ct = default);
}
```

**소비**

```csharp
await using var rs = await mapper.GetDashboardAsync(param, ct);

var summary  = await rs.ReadAsync<DashboardSummary>();   // 첫 번째 결과셋
var details  = await rs.ReadListAsync<DailyDetail>();     // 두 번째 결과셋
var topUsers = await rs.ReadListAsync<TopUser>();         // 세 번째 결과셋
```

### ResultSetGroup API

```csharp
public sealed class ResultSetGroup : IAsyncDisposable
{
    // 단일 행 읽기 (null 가능)
    Task<T?> ReadAsync<T>(CancellationToken ct = default)

    // 여러 행 읽기
    Task<IList<T>> ReadListAsync<T>(CancellationToken ct = default)

    // 스트리밍 읽기
    IAsyncEnumerable<T> ReadStreamAsync<T>(CancellationToken ct = default)

    // 다음 결과셋으로 이동 (자동으로 처리되므로 직접 호출 불필요)
    Task<bool> NextResultAsync(CancellationToken ct = default)
}
```

**중요**: `ReadAsync`, `ReadListAsync`를 호출하는 순서가 SQL의 SELECT 순서와 정확히 일치해야 한다.

### 실전 예제: 대시보드 API

**SQL**

```xml
<select id="GetAdminDashboard">
  -- 1. 요약 통계
  SELECT
    (SELECT COUNT(*) FROM users WHERE is_active = true)      AS active_users,
    (SELECT COUNT(*) FROM orders WHERE DATE(created_at) = CURRENT_DATE) AS orders_today,
    (SELECT SUM(amount) FROM orders WHERE DATE(created_at) = CURRENT_DATE) AS revenue_today;

  -- 2. 최근 7일 일별 주문
  SELECT DATE(created_at) AS date, COUNT(*) AS count, SUM(amount) AS revenue
  FROM orders
  WHERE created_at >= CURRENT_DATE - INTERVAL '7 days'
  GROUP BY DATE(created_at)
  ORDER BY date;

  -- 3. 상위 10 고객
  SELECT u.id, u.user_name, SUM(o.amount) AS total_spent
  FROM users u
  JOIN orders o ON o.user_id = u.id
  GROUP BY u.id, u.user_name
  ORDER BY total_spent DESC
  LIMIT 10;
</select>
```

**C# 모델**

```csharp
public record DashboardSummary(int ActiveUsers, int OrdersToday, decimal RevenueToday);
public record DailyOrderStat(DateOnly Date, int Count, decimal Revenue);
public record TopCustomer(long Id, string UserName, decimal TotalSpent);
```

**Controller**

```csharp
[HttpGet("dashboard")]
public async Task<IActionResult> GetDashboard(CancellationToken ct)
{
    await using var rs = await _mapper.GetAdminDashboardAsync(ct);

    var summary  = await rs.ReadAsync<DashboardSummary>();
    var daily    = await rs.ReadListAsync<DailyOrderStat>();
    var topUsers = await rs.ReadListAsync<TopCustomer>();

    return Ok(new { Summary = summary, DailyStats = daily, TopCustomers = topUsers });
}
```

### 성능 비교: Multi-ResultSet vs 개별 쿼리

```
개별 쿼리 3회:       3 × (네트워크 왕복 + 쿼리 실행)
Multi-ResultSet 1회: 1 × (네트워크 왕복 + 3 쿼리 실행)
```

네트워크 레이턴시가 높을수록 Multi-ResultSet의 이점이 크다. 로컬 DB에서는 차이가 미미할 수 있다.

### 직접 API (Mapper 없이)

```csharp
using var session = factory.OpenSession(autoCommit: true);

await using var rs = await session.SelectMultipleAsync(
    "Dashboard.GetAll",
    new { Period = "monthly" },
    cancellationToken);

var summary = await rs.ReadAsync<Summary>();
var details = await rs.ReadListAsync<Detail>();
```

### 오류 처리

```csharp
try
{
    await using var rs = await session.SelectMultipleAsync("Reports.Complex", param, ct);

    // 각 읽기 단계에서 독립적으로 오류 처리
    var summary = await rs.ReadAsync<ReportSummary>()
        ?? throw new InvalidOperationException("Summary not found");

    var lines = await rs.ReadListAsync<ReportLine>();

    return new CompleteReport(summary, lines);
}
catch (DbException ex)
{
    logger.LogError(ex, "Multi-ResultSet query failed");
    throw;
}
```
