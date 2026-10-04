# Performance Optimization Cookbook

## UNION ALL 패턴 - 다중 집계 단일 쿼리

EF Core에서 여러 집계 쿼리를 개별 실행하면 N번의 DB 라운드트립이 발생한다. UNION ALL로 단일 쿼리에 통합하면 1회 라운드트립으로 동일 결과를 얻는다.

```xml
<select id="GetDashboardStats" resultMap="StatsResult">
  SELECT 'total_users' AS metric, COUNT(*)::text AS value FROM users
  UNION ALL
  SELECT 'active_users', COUNT(*)::text FROM users WHERE is_active = true
  UNION ALL
  SELECT 'new_today', COUNT(*)::text FROM users
    WHERE created_at >= CURRENT_DATE
  UNION ALL
  SELECT 'avg_age', ROUND(AVG(age), 2)::text FROM users
</select>
```

벤치마크 결과 (EF Core 대비):
- 응답 시간: 2.35x 빠름
- 메모리 할당: 82% 감소
- GC 발생: 0회

## Streaming - 대용량 결과 처리

`IAsyncEnumerable`로 결과를 스트리밍하여 메모리 사용량을 일정하게 유지한다.

```csharp
await foreach (var row in session.SelectStream<LogEntry>("Logs.GetAll")) {
    await ProcessAsync(row);
}
```

`SelectList`는 전체 결과를 메모리에 적재하므로 대용량 데이터에는 `SelectStream`을 사용한다.

## Multi-ResultSet - 복합 대시보드

하나의 SQL에서 여러 결과셋을 반환받아 단일 라운드트립으로 처리한다.

```xml
<select id="GetDashboard">
  SELECT * FROM summary WHERE period = #{Period};
  SELECT * FROM details WHERE period = #{Period};
  SELECT * FROM trends WHERE period = #{Period};
</select>
```

```csharp
await using var rs = await session.SelectMultipleAsync("Dashboard.Get", param);
var summary = await rs.ReadAsync<Summary>();
var details = await rs.ReadListAsync<Detail>();
var trends  = await rs.ReadListAsync<Trend>();
```

## Second-Level Cache

읽기 위주의 쿼리에 캐시를 적용하여 DB 부하를 줄인다. 캐시는 namespace 단위로 `<cache>` 요소로 선언한다. 개별 statement에 `useCache` 속성은 지원하지 않는다.

```xml
<cache eviction="LRU" flushInterval="600000" size="512" />

<select id="GetMonthlyStats">
  SELECT * FROM monthly_stats WHERE month = #{Month}
</select>
```

Write 연산(Insert/Update/Delete) 실행 시 해당 namespace의 캐시가 자동 무효화된다.

## Command Timeout

장시간 실행되는 쿼리의 타임아웃은 DI 설정의 `DefaultCommandTimeout`으로 전역 지정한다. XML 속성으로는 지원하지 않는다.

```csharp
services.AddNuVatis(options => {
    options.DefaultCommandTimeout = 300; // 초 단위
});
```

## Object Pooling - GC 할당 감소

NuVatis 내부에서 빈번히 생성/해제되는 객체를 풀링하여 GC 압력을 줄인다.

| Pool | 대상 | 효과 |
|------|------|------|
| `StringBuilderCache` | 동적 SQL 문자열 빌드 | StringBuilder 할당 제거 |
| `DbParameterListPool` | DbParameter 리스트 | List 할당 제거 |
| `InterceptorContextPool` | Interceptor 컨텍스트 객체 | 컨텍스트 할당 제거 |

이 최적화는 자동 적용되며 별도 설정이 불필요하다. BenchmarkDotNet 기준 고부하 시나리오에서 Gen0 GC 발생을 50% 이상 감소시킨다.

## BatchExecutor - DbBatch API

`OpenBatchSession()`으로 배치 세션을 열어 여러 SQL을 버퍼링한 뒤 `FlushStatements()`로 한 번에 실행한다.

```csharp
using var batch = factory.OpenBatchSession();
batch.Insert("Users.Insert", user1);
batch.Insert("Users.Insert", user2);
batch.Insert("Users.Insert", user3);
await batch.FlushStatementsAsync();
```

`FlushStatements` 호출 전까지 쿼리는 버퍼에만 쌓이며 DB에 전송되지 않는다. .NET 8 이상 환경에서는 내부적으로 DbBatch API를 사용하여 단일 라운드트립으로 처리한다.

## Connection Pooling 최적화

NuVatis는 Lazy Connection을 사용한다. 세션 생성 시점이 아닌 첫 쿼리 시점에 커넥션을 획득하므로, 커넥션 풀 고갈 위험을 줄인다.

```csharp
using var session = factory.OpenSession();    // 커넥션 미획득
var user = session.SelectOne<User>("...");    // 이 시점에 커넥션 획득
session.Commit();                             // Commit 후 Dispose에서 반환
```

## 생성 코드의 파라미터 접근 (v2.8.0~)

XML/어트리뷰트 statement 의 `#{...}` 바인딩은 기본적으로 리플렉션
(`GetType().GetProperty`) 을 거친다. 매핑 파라미터의 정적 타입이 Roslyn 심볼로
해석되면 **해석된 프로퍼티에 대해서만** 컴파일된 직접 접근으로 대체된다.

| 측정 범위 | 리플렉션 | 직접 접근 | 개선 |
|-----------|---------|----------|------|
| SQL 조립 단독 | 1,536.3 ns | 706.8 ns | 2.17배 |
| 실제 쿼리까지 포함 | 28,152.3 ns | 26,753.1 ns | **약 5%** |

**"2배" 를 전체 쿼리 성능으로 읽으면 안 된다.** 조립 절감폭 829 ns 이 전체 쿼리의
약 3% 에 불과하며, 네트워크 DB(왕복 0.5~5 ms)에서는 상대 이득이 1% 미만으로 내려간다.

폴백으로 리플렉션에 남는 경우:
- 중첩 경로 `#{user.Address.City}`
- foreach 아이템 `#{it.Name}`
- 파라미터 타입이 `object` / `dynamic` / 미해결

따라서 **파라미터를 `object` 가 아니라 구체 타입으로 선언**하면 이 최적화가 발동한다.

```csharp
// 발동함 — OrderQuery 에 Name/MinAge 가 선언돼 있음
[Select("SELECT * FROM orders WHERE name = #{Name}")]
object Search(OrderQuery param);

// 발동하지 않음 — object 로 선언해 심볼 해석이 불가능
[Select("SELECT * FROM orders WHERE name = #{Name}")]
object Search(object param);
```

벤치마크는 `benchmarks/NuVatis.Benchmarks/DynamicSqlBuildBenchmark.cs` 에서 재현할 수 있다.
단, 이 파일은 생성 코드에서 옮긴 미러이므로 생성 출력이 바뀌면 함께 갱신해야 한다
(기준선 해시는 파일 주석에 기재).

## 성능 비교 기준

| 시나리오 | 권장 도구 | 이유 |
|---------|---------|------|
| 단일 엔티티 CRUD | EF Core | Change Tracking, 편의성 |
| 복합 집계/통계 | NuVatis | UNION ALL, 직접 SQL 제어 |
| 대용량 리스트 | NuVatis (Stream) | IAsyncEnumerable, 메모리 효율 |
| 복합 대시보드 | NuVatis (Multi-RS) | 단일 쿼리 다중 결과셋 |
| 읽기 위주 캐시 | NuVatis (L2 Cache) | Namespace 단위 자동 무효화 |
