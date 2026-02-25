# Session Lifecycle

## 세션 생명주기

```
SqlSessionFactory.OpenSession()
    |
    v
SqlSession 생성 (커넥션 미획득, Lazy)
    |
    v
첫 쿼리 실행 시 -> DbConnection 획득
    |
    v
쿼리 실행 (SelectOne, SelectList, Insert, Update, Delete, ...)
    |
    v
Commit() 또는 Rollback()
    |
    v
Dispose() -> 커넥션 반환, autoCommit=false이고 Commit 없으면 자동 Rollback
```

## 환경별 생명주기

| 환경 | 등록 방식 | 생명주기 |
|------|---------|---------|
| ASP.NET Core (DI) | Scoped | HTTP 요청 당 1개 세션 |
| Generic Host (DI) | Scoped | DI Scope 당 1개 |
| Console/Batch | Manual | using 블록 |

## Thread Safety

ISqlSession은 스레드 안전하지 않다. `Interlocked.CompareExchange`로 동시 접근을 감지하여 `InvalidOperationException`을 발생시킨다.

병렬 처리가 필요하면 각 스레드에서 별도 세션을 생성한다:

```csharp
await Parallel.ForEachAsync(items, async (item, ct) => {
    using var session = factory.OpenSession(autoCommit: true);
    var mapper = session.GetMapper<IItemMapper>();
    await mapper.ProcessAsync(item, ct);
});
```

## autoCommit 모드

### autoCommit: false (기본)

```csharp
using var session = factory.OpenSession();
session.Insert("...", data);
session.Commit();        // 명시적 Commit 필요
```

Commit 없이 Dispose되면 자동 Rollback + 경고 로그.

### autoCommit: true

```csharp
using var session = factory.OpenSession(autoCommit: true);
var data = session.SelectList<Item>("Items.GetAll");
// Dispose 시 별도 처리 없음
```

읽기 전용 세션에 적합하다.

## Interceptor 실행 순서

```
RunBefore(ctx)          -- 실행 전 인터셉터
    |
    v
Stopwatch.Start()
    |
    v
Executor 실행 (DB 쿼리)
    |
    v
Stopwatch.Stop()
    |
    v
ctx.ElapsedMilliseconds 설정
    |
    v
RunAfter(ctx)           -- 실행 후 인터셉터 (elapsed, 예외 정보 포함)
```

예외 발생 시에도 RunAfter가 호출된다 (ctx.Exception에 예외 정보 포함).

## Lazy Connection

세션 생성 시점에는 커넥션을 획득하지 않는다. 첫 쿼리 실행 시점에 커넥션을 열어 커넥션 풀 고갈을 방지한다.

```
OpenSession()      -- 커넥션: 없음
SelectOne(...)     -- 커넥션: 획득
SelectList(...)    -- 커넥션: 기존 것 재사용
Commit()           -- 커넥션: 트랜잭션 커밋
Dispose()          -- 커넥션: 반환
```
