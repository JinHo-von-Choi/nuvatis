# Troubleshooting Guide

작성자: 최진호
작성일: 2026-03-01

NuVatis 사용 중 자주 만나는 문제와 해결 방법.

---

## 빌드 오류

### "No statement found for method" (NV002)

**증상**: 빌드 시 NV002 오류 발생.

```
error NV002: No statement found for method 'MyApp.Mappers.IUserMapper.GetById'
```

**원인 체크리스트**

1. XML 파일이 빌드에 포함되어 있는가?

```xml
<!-- .csproj -->
<ItemGroup>
  <AdditionalFiles Include="Mappers/**/*.xml" />
</ItemGroup>
```

2. XML의 `namespace`가 C# 인터페이스 FQN과 정확히 일치하는가?

```xml
<!-- 오류 예: 네임스페이스 불일치 -->
<mapper namespace="Mappers.IUserMapper">    <!-- 틀림 -->
<mapper namespace="MyApp.Mappers.IUserMapper">  <!-- 맞음 -->
```

3. XML의 `id`가 C# 메서드명과 정확히 일치하는가? (대소문자 포함)

```xml
<select id="getById">  <!-- 틀림: GetById이어야 함 -->
<select id="GetById">  <!-- 맞음 -->
```

4. `[NuVatisMapper]` 어트리뷰트가 인터페이스에 붙어 있는가?

**해결**: 위 항목을 확인 후 `dotnet build --no-incremental`로 증분 빌드 캐시를 비우고 재빌드.

---

### "${param} is vulnerable to SQL injection" (NV004)

```
error NV004: ${Column} uses string substitution which is vulnerable to SQL injection
```

**v2.0.0부터 `string` 타입의 `${}` 파라미터는 빌드 오류**다.

**해결 방법 선택**

| 상황 | 해결 |
|------|------|
| 값이 파라미터 바인딩으로 충분한 경우 | `${name}` → `#{name}`으로 교체 |
| 동적 컬럼명/테이블명 (enum 기반) | `SqlIdentifier.FromEnum()` 사용 |
| 동적 컬럼명/테이블명 (사용자 입력) | `SqlIdentifier.FromAllowed()` 사용 |
| 코드에 하드코딩된 리터럴 상수 | `[SqlConstant]` 어트리뷰트 사용 |

자세한 내용: [SQL Injection Prevention](security/sql-injection-prevention.md)

---

### "ResultMap 'X' not found" (NV001)

**해결**: `<resultMap id="X">` 정의가 같은 XML 파일에 있는지 확인.

```xml
<!-- 반드시 같은 파일에 정의 -->
<resultMap id="UserResult" type="User">
  <id column="id" property="Id" />
</resultMap>

<select id="GetById" resultMap="UserResult">
  SELECT id FROM users WHERE id = #{Id}
</select>
```

---

## 런타임 오류

### InvalidOperationException: Concurrent access detected

```
InvalidOperationException: ISqlSession does not support concurrent access.
```

**원인**: 하나의 `ISqlSession`을 여러 스레드에서 동시에 사용했다.

`ISqlSession`은 스레드 안전하지 않다. ASP.NET Core에서는 DI Scoped로 등록되어 HTTP 요청당 하나의 세션이 생성되므로 일반적으로 문제가 없다. 그러나 `Parallel.ForEachAsync` 등에서 세션을 공유하면 이 오류가 발생한다.

**해결**: 각 스레드에서 별도 세션 생성.

```csharp
// 잘못된 코드
using var session = factory.OpenSession();
await Parallel.ForEachAsync(items, async (item, ct) => {
    await session.Insert("Items.Insert", item);  // 동시 접근 → 오류
});

// 올바른 코드
await Parallel.ForEachAsync(items, async (item, ct) => {
    using var session = factory.OpenSession(autoCommit: true);
    await session.Insert("Items.Insert", item);  // 각자 별도 세션
});
```

---

### ArgumentException: SqlIdentifier에 허용되지 않은 값

```
ArgumentException: 허용되지 않은 SQL 식별자입니다: 'unknown_col'. 허용 목록: [id, user_name, created_at]
```

**원인**: `SqlIdentifier.FromAllowed()`의 허용 목록에 없는 값이 전달됨.

**해결**:

1. 허용 목록을 확장하거나
2. 프론트엔드 검증을 강화하여 허용된 값만 전송하도록 수정

```csharp
// 허용 목록 상수로 관리
private static readonly string[] AllowedSortColumns = { "id", "user_name", "created_at", "email" };

public IList<User> GetSorted(string sortBy)
{
    var col = SqlIdentifier.FromAllowed(sortBy, AllowedSortColumns);
    return _mapper.GetSorted(new { Column = col });
}
```

---

### NullReferenceException: 매핑된 값이 null

**증상**: ResultMap으로 매핑한 객체의 프로퍼티가 `null`이거나 기본값.

**원인 체크리스트**

1. 컬럼명 오타: `<result column="user_nme" property="UserName" />` → NV006 경고 확인
2. SELECT 절에 해당 컬럼이 없음: `SELECT id FROM users` (user_name 누락)
3. ResultMap의 `type`이 잘못 지정됨

**진단 방법**

NV006 경고를 확인한다. 빌드 출력에서 `info NV006` 항목을 찾는다.

```bash
dotnet build 2>&1 | grep "NV006"
```

---

### 쿼리가 항상 빈 결과 반환

**체크리스트**

1. 파라미터 프로퍼티명이 XML의 `#{}` 파라미터명과 일치하는가?

```csharp
// C# 파라미터
var result = session.SelectOne<User>("Users.GetById", new { UserId = 42 });
```

```xml
<!-- XML: #{Id}가 아닌 #{UserId}이어야 함 -->
<select id="GetById">WHERE id = #{UserId}</select>
```

2. autoCommit이 `false`인 상태에서 `INSERT` 후 `Commit()`을 호출했는가?

```csharp
session.Insert("Users.Insert", user);
// session.Commit() 누락 → 다음 쿼리에서 해당 행이 안 보임
var user = session.SelectOne<User>("Users.GetById", new { Id = 1 });  // null
```

---

### Commit 없이 Dispose됨 (자동 Rollback)

**증상**: 데이터가 DB에 저장되지 않고, 로그에 경고 발생.

```
[NuVatis] Session disposed without Commit. Rolling back transaction.
```

**원인**: `using` 블록에서 예외 발생 또는 `Commit()` 누락.

**해결**:

```csharp
// 패턴 1: 명시적 Commit
using var session = factory.OpenSession();
try
{
    session.Insert("Users.Insert", user);
    session.Commit();  // 반드시 호출
}
catch
{
    session.Rollback();
    throw;
}

// 패턴 2: ExecuteInTransactionAsync 활용
await session.ExecuteInTransactionAsync(async () => {
    await session.InsertAsync("Users.Insert", user);
    // 성공 시 자동 Commit, 예외 시 자동 Rollback
});
```

---

## 성능 문제

### 쿼리가 느리다

**진단 단계**

1. SlowQueryInterceptor로 임계값 초과 쿼리 탐지:

```csharp
factory.AddInterceptor(new SlowQueryInterceptor(logger, thresholdMs: 500));
```

2. LoggingInterceptor로 실행된 SQL 확인:

```csharp
factory.AddInterceptor(new LoggingInterceptor(logger));
// appsettings.json: "NuVatis": "Debug" 레벨 설정
```

3. MetricsInterceptor로 Prometheus 메트릭 수집:

```csharp
factory.AddInterceptor(new MetricsInterceptor());
// Grafana에서 nuvatis_query_duration_ms 히스토그램 확인
```

**흔한 원인과 해결책**

| 원인 | 해결 |
|------|------|
| N+1 쿼리 | JOIN + resultMap으로 단일 쿼리화 |
| 결과 전체 메모리 적재 | `SelectStream<T>` 사용 |
| 불필요한 캐시 미적용 | `<cache>` + `useCache="true"` 추가 |
| `commandTimeout` 부족 | 복잡한 쿼리에 개별 타임아웃 설정 |

### 메모리 사용량이 높다

**원인**: 대용량 결과를 `SelectList<T>`로 가져오는 경우.

**해결**: `SelectStream<T>`으로 교체하여 스트리밍 처리.

```csharp
// 문제 코드: 100만 건이 메모리에 적재됨
var all = session.SelectList<LogEntry>("Logs.GetAll");

// 해결: 스트리밍 처리
await foreach (var entry in session.SelectStream<LogEntry>("Logs.GetAll", ct: ct))
{
    Process(entry);
}
```

---

## DI 설정 오류

### ISqlSession이 DI에서 Resolve되지 않음

**증상**: `InvalidOperationException: Unable to resolve service for type 'NuVatis.Session.ISqlSession'`

**원인**: `AddNuVatis()`를 `Program.cs`에서 호출하지 않았거나, `ISqlSessionFactory`에서 세션을 직접 받으려 하는 경우.

**해결**: ASP.NET Core DI에서는 `ISqlSession`을 직접 주입받는 대신 Mapper 인터페이스를 주입받는다.

```csharp
// 잘못된 패턴
public class UserService {
    public UserService(ISqlSession session) { ... }  // 직접 주입 X
}

// 올바른 패턴
public class UserService {
    public UserService(IUserMapper mapper) { ... }   // Mapper 주입
}
```

또는 `ISqlSessionFactory`를 주입받아 직접 세션을 생성:

```csharp
public class BackgroundJobService {
    private readonly ISqlSessionFactory _factory;

    public BackgroundJobService(ISqlSessionFactory factory) {
        _factory = factory;
    }

    public async Task RunAsync(CancellationToken ct) {
        using var session = _factory.OpenSession(autoCommit: true);
        // 사용
    }
}
```

### Mapper가 DI에서 Resolve되지 않음

**원인**: `RegisterMappers`를 호출하지 않았거나 빌드 후 생성 코드가 없는 경우.

**체크리스트**

1. `dotnet build` 성공 여부 확인 (Source Generator가 실행되어야 함)
2. `options.RegisterMappers(NuVatisMapperRegistry.RegisterAll)` 호출 여부
3. `[NuVatisMapper]` 어트리뷰트가 인터페이스에 존재하는지 확인

---

## Source Generator 관련

### 생성된 코드가 반영되지 않는다

**해결**: 증분 빌드 캐시 초기화 후 재빌드.

```bash
dotnet build --no-incremental
```

또는 `obj/` 폴더 삭제:

```bash
find . -name obj -type d | xargs rm -rf
dotnet build
```

### IDE에서 생성된 코드를 찾을 수 없다

Visual Studio / Rider에서 `obj/GeneratedCode/` 또는 `obj/Debug/net8.0/generated/` 하위에 생성된 `.g.cs` 파일이 존재한다.

IntelliSense가 즉시 반영되지 않으면 IDE를 재시작하거나 솔루션을 다시 로드한다.

---

## 자주 묻는 질문 (FAQ)

**Q: Mapper 메서드의 반환 타입으로 어떤 타입을 사용할 수 있나?**

| 반환 타입 | 생성되는 코드 | 설명 |
|----------|-------------|------|
| `T?` | `SelectOne<T>` | 단일 행, null 가능 |
| `IList<T>` | `SelectList<T>` | 여러 행 |
| `int` (DML) | `Insert`/`Update`/`Delete` | 영향 행 수 |
| `Task<T?>` | `SelectOneAsync<T>` | 단일 행, 비동기 |
| `Task<IList<T>>` | `SelectListAsync<T>` | 여러 행, 비동기 |
| `IAsyncEnumerable<T>` | `SelectStream<T>` | 스트리밍 |
| `Task<int>` (DML) | `InsertAsync` 등 | 영향 행 수, 비동기 |

**Q: 같은 프로젝트에서 EF Core와 NuVatis를 함께 쓸 수 있나?**

가능하다. [EF Core Integration 가이드](cookbook/ef-core-integration.md)와 [Hybrid 가이드](cookbook/hybrid-efcore-nuvatis.md)를 참조한다.

**Q: DI 없이 Non-DI 환경에서 사용할 수 있나?**

```csharp
var config = new NuVatisConfigurationBuilder()
    .ConnectionString("Host=localhost;Database=mydb;Username=app;Password=***")
    .Provider(new PostgreSqlProvider())
    .AddXmlMapperDirectory("Mappers")
    .Build();

var factory = new SqlSessionFactory(config);
using var session = factory.OpenSession();
```

**Q: Native AOT 환경에서 사용 가능한가?**

.NET 8+ + Source Generator + `resultMap` 사용 시 가능하다. `resultType` (ColumnMapper 리플렉션)은 Native AOT에서 동작하지 않는다. 모든 매핑을 명시적 `resultMap`으로 정의해야 한다.

**Q: 트랜잭션 격리 수준을 설정할 수 있나?**

```csharp
// 팩토리에서 세션 열기 후 DB 커넥션에 직접 접근
using var session = factory.OpenSession();
// 첫 쿼리 실행 후 커넥션이 열림
session.SelectOne<int>("Ping.Check");

using var tx = session.Connection.BeginTransaction(IsolationLevel.Serializable);
// 이후 작업...
```

또는 `FromExistingConnection`으로 외부 트랜잭션 전달:

```csharp
using var conn = new NpgsqlConnection(connStr);
await conn.OpenAsync();
using var tx = conn.BeginTransaction(IsolationLevel.RepeatableRead);

using var session = factory.FromExistingConnection(conn, tx);
session.Insert("Users.Insert", user);

await tx.CommitAsync();
```
