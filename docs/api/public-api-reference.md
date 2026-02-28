# NuVatis Public API Reference

작성자: 최진호
작성일: 2026-02-26
수정일: 2026-03-01

---

## API 호환성 정책

NuVatis는 Semantic Versioning 2.0.0을 준수한다.

- Major (x.0.0): 하위 호환성이 깨지는 public API 변경
- Minor (0.x.0): 하위 호환되는 기능 추가
- Patch (0.0.x): 버그 수정

v1.0.0부터 `PublicApiAnalyzers`가 CI에서 모든 public API 변경을 감시한다. 의도하지 않은 public API 노출 시 빌드가 실패한다.

### API 변경 절차

1. `PublicAPI.Unshipped.txt`에 새 심볼 추가
2. PR 리뷰에서 API 변경 승인
3. 릴리스 시 `Unshipped.txt` → `Shipped.txt`로 이동
4. 삭제된 API는 `PublicAPI.Shipped.txt`에서 `*REMOVED*` 접두사 추가

---

## 패키지 구조

| 패키지 | 설명 | 지원 TF |
|--------|------|---------|
| `NuVatis.Core` | 핵심 런타임 (ISqlSession, 매핑, 캐시) | net7.0;net8.0;net9.0;net10.0 |
| `NuVatis.Generators` | Roslyn Source Generator (컴파일 타임) | netstandard2.0 |
| `NuVatis.PostgreSql` | PostgreSQL Provider (Npgsql) | net7.0~net10.0 |
| `NuVatis.MySql` | MySQL/MariaDB Provider (MySqlConnector) | net7.0~net10.0 |
| `NuVatis.SqlServer` | SQL Server Provider (Microsoft.Data.SqlClient) | net7.0~net10.0 |
| `NuVatis.Sqlite` | SQLite Provider (Microsoft.Data.Sqlite) | net7.0~net10.0 |
| `NuVatis.Extensions.DependencyInjection` | ASP.NET Core DI + Health Check | net7.0~net10.0 |
| `NuVatis.Extensions.OpenTelemetry` | OpenTelemetry 분산 추적 | net7.0~net10.0 |
| `NuVatis.Extensions.EntityFrameworkCore` | EF Core 트랜잭션 공유 | net7.0~net10.0 |
| `NuVatis.Extensions.Aspire` | .NET Aspire 통합 | net8.0~net10.0 |
| `NuVatis.Testing` | 테스트 유틸리티 | net7.0~net10.0 |

---

## ISqlSession

**네임스페이스**: `NuVatis.Session`
**상속**: `IDisposable`, `IAsyncDisposable`

SQL 세션의 핵심 인터페이스. 모든 DB 작업의 진입점. 스레드 안전하지 않다 (동시 접근 시 `InvalidOperationException` 발생).

```csharp
public interface ISqlSession : IDisposable, IAsyncDisposable
```

---

### SelectOne\<T\>

단일 행을 조회한다. 결과가 없으면 `null` 또는 `default(T)`를 반환한다. 결과가 2개 이상이어도 첫 번째 행만 반환한다.

```csharp
T? SelectOne<T>(string statementId, object? parameter = null)
```

**파라미터**

| 이름 | 타입 | 설명 |
|------|------|------|
| `statementId` | `string` | `namespace.statementId` 형태의 완전한 Statement 식별자. XML mapper의 `namespace + "." + id` |
| `parameter` | `object?` | SQL 파라미터 객체. `null` 가능. 익명 객체, POCO, primitive 타입 모두 가능 |

**반환값**: `T?` — 조회된 객체. 결과 없으면 `null`.

**예외**
- `NuVatisException` — Statement를 찾을 수 없는 경우
- `DbException` — DB 오류

**예제**

```csharp
// 단일 파라미터 (primitive)
var user = session.SelectOne<User>("UserMapper.GetById", new { Id = 42 });

// 파라미터 없음
var count = session.SelectOne<int>("UserMapper.Count");

// null 체크
if (session.SelectOne<User>("UserMapper.GetByEmail", new { Email = email }) is { } found)
{
    // found 사용
}
```

---

### SelectOneAsync\<T\>

`SelectOne<T>`의 비동기 버전.

```csharp
Task<T?> SelectOneAsync<T>(string statementId, object? parameter = null, CancellationToken ct = default)
```

**파라미터**

| 이름 | 타입 | 설명 |
|------|------|------|
| `statementId` | `string` | Statement 식별자 |
| `parameter` | `object?` | SQL 파라미터 |
| `ct` | `CancellationToken` | 취소 토큰 |

**예제**

```csharp
var user = await session.SelectOneAsync<User>(
    "UserMapper.GetById",
    new { Id = 42 },
    cancellationToken);
```

---

### SelectList\<T\>

여러 행을 조회하여 `IList<T>`로 반환한다. 결과가 없으면 빈 리스트를 반환한다. 모든 결과를 메모리에 적재한다. 대용량 데이터는 `SelectStream<T>` 사용을 권장한다.

```csharp
IList<T> SelectList<T>(string statementId, object? parameter = null)
```

**파라미터**

| 이름 | 타입 | 설명 |
|------|------|------|
| `statementId` | `string` | Statement 식별자 |
| `parameter` | `object?` | SQL 파라미터 |

**반환값**: `IList<T>` — 결과 목록. 빈 경우 `Count == 0`인 리스트.

**예제**

```csharp
var users = session.SelectList<User>("UserMapper.GetAll");
var filtered = session.SelectList<User>(
    "UserMapper.Search",
    new { UserName = "%jinho%", MinAge = 20 });
```

---

### SelectListAsync\<T\>

`SelectList<T>`의 비동기 버전.

```csharp
Task<IList<T>> SelectListAsync<T>(string statementId, object? parameter = null, CancellationToken ct = default)
```

---

### SelectStream\<T\>

결과를 `IAsyncEnumerable<T>`로 스트리밍하여 반환한다. 모든 결과를 메모리에 적재하지 않으므로 대용량 데이터 처리에 적합하다.

```csharp
IAsyncEnumerable<T> SelectStream<T>(string statementId, object? parameter = null, CancellationToken ct = default)
```

**파라미터**

| 이름 | 타입 | 설명 |
|------|------|------|
| `statementId` | `string` | Statement 식별자 |
| `parameter` | `object?` | SQL 파라미터 |
| `ct` | `CancellationToken` | 취소 토큰 (열거 중 취소 시 `OperationCanceledException`) |

**반환값**: `IAsyncEnumerable<T>` — 스트리밍 열거자.

**주의**: `await foreach`로 소비 완료 전에 세션을 Dispose하면 안 된다.

**예제**

```csharp
await foreach (var row in session.SelectStream<LogEntry>(
    "Logs.GetAll",
    new { Since = DateTime.UtcNow.AddDays(-7) },
    cancellationToken))
{
    await ProcessAsync(row);
}
```

---

### SelectMultiple / SelectMultipleAsync

하나의 SQL에서 여러 결과셋을 순차적으로 소비한다. `ResultSetGroup`은 `IAsyncDisposable`이므로 `await using`으로 사용한다.

```csharp
ResultSetGroup SelectMultiple(string statementId, object? parameter = null)
Task<ResultSetGroup> SelectMultipleAsync(string statementId, object? parameter = null, CancellationToken ct = default)
```

**예제**

```csharp
await using var rs = await session.SelectMultipleAsync("Dashboard.GetAll", param);
var summary = await rs.ReadAsync<DashboardSummary>();
var details = await rs.ReadListAsync<DashboardDetail>();
var trends  = await rs.ReadListAsync<TrendData>();
```

> 지원 DB: PostgreSQL, SQL Server, MySQL. SQLite는 다중 결과셋을 지원하지 않는다.

---

### Insert / InsertAsync

`INSERT` 문을 실행하고 영향받은 행 수를 반환한다.

```csharp
int Insert(string statementId, object? parameter = null)
Task<int> InsertAsync(string statementId, object? parameter = null, CancellationToken ct = default)
```

**반환값**: `int` — 영향받은 행 수 (INSERT 성공 시 통상 1).

**예제**

```csharp
int affected = session.Insert("UserMapper.Insert", new User {
    UserName = "jinho",
    Email    = "jinho@example.com"
});

// 비동기
int rows = await session.InsertAsync("UserMapper.InsertBatch", batchParam, ct);
```

---

### Update / UpdateAsync

`UPDATE` 문을 실행하고 영향받은 행 수를 반환한다.

```csharp
int Update(string statementId, object? parameter = null)
Task<int> UpdateAsync(string statementId, object? parameter = null, CancellationToken ct = default)
```

**반환값**: `int` — 업데이트된 행 수. 조건에 맞는 행이 없으면 `0`.

---

### Delete / DeleteAsync

`DELETE` 문을 실행하고 영향받은 행 수를 반환한다.

```csharp
int Delete(string statementId, object? parameter = null)
Task<int> DeleteAsync(string statementId, object? parameter = null, CancellationToken ct = default)
```

---

### Commit / CommitAsync

현재 트랜잭션을 커밋한다. `autoCommit: false`(기본) 세션에서 반드시 호출해야 변경사항이 DB에 반영된다.

```csharp
void Commit()
Task CommitAsync(CancellationToken ct = default)
```

**주의**: `autoCommit: true` 세션에서 호출해도 오류 없이 무시된다.

---

### Rollback / RollbackAsync

현재 트랜잭션을 롤백한다. Dispose 시 Commit되지 않은 트랜잭션은 자동 롤백된다.

```csharp
void Rollback()
Task RollbackAsync(CancellationToken ct = default)
```

---

### ExecuteInTransactionAsync

트랜잭션 내에서 비동기 액션을 실행한다. 성공 시 자동 Commit, 예외 발생 시 자동 Rollback.

```csharp
Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default)
```

**파라미터**

| 이름 | 타입 | 설명 |
|------|------|------|
| `action` | `Func<Task>` | 트랜잭션 내에서 실행할 비동기 액션 |
| `ct` | `CancellationToken` | 취소 토큰 |

**예제**

```csharp
await session.ExecuteInTransactionAsync(async () => {
    await mapper.InsertAsync(user, ct);
    await mapper.InsertAsync(order, ct);
    await mapper.UpdateAsync(inventory, ct);
}, cancellationToken);
```

---

### FlushStatements / FlushStatementsAsync

BatchSession 모드에서 누적된 SQL을 DB로 전송한다. 일반 세션에서는 no-op.

```csharp
int FlushStatements()
Task<int> FlushStatementsAsync(CancellationToken ct = default)
```

**반환값**: `int` — 전송된 statement 수.

---

### GetMapper\<T\>

Mapper 인터페이스의 구현체를 가져온다. Source Generator가 생성한 구현체를 DI 없이 직접 사용할 때 활용한다.

```csharp
T GetMapper<T>() where T : class
```

**파라미터**: `T` — `[NuVatisMapper]` 어트리뷰트가 붙은 Mapper 인터페이스 타입.

**예외**: `InvalidOperationException` — 등록되지 않은 Mapper 타입을 요청한 경우.

**예제**

```csharp
using var session = factory.OpenSession();
var mapper = session.GetMapper<IUserMapper>();
var user = mapper.GetById(42);
session.Commit();
```

---

### Connection 프로퍼티

현재 세션이 사용하는 `DbConnection`을 반환한다. 외부 DB 라이브러리와 연동할 때 사용한다.

```csharp
DbConnection Connection { get; }
```

**주의**: Lazy Connection 방식으로 첫 쿼리 전에는 `null`일 수 있다.

---

## ISqlSessionFactory

**네임스페이스**: `NuVatis.Session`

세션 팩토리. Singleton으로 등록된다.

```csharp
public interface ISqlSessionFactory
```

---

### OpenSession

새로운 SQL 세션을 열고 반환한다. 커넥션은 첫 쿼리 실행 시점에 Lazy하게 획득된다.

```csharp
ISqlSession OpenSession(bool autoCommit = false)
```

**파라미터**

| 이름 | 타입 | 기본값 | 설명 |
|------|------|--------|------|
| `autoCommit` | `bool` | `false` | `true`이면 매 쿼리 후 자동 커밋. 읽기 전용 세션에 적합 |

**반환값**: `ISqlSession` — 새 세션 인스턴스. `using` 블록 내에서 사용한다.

**예제**

```csharp
// 쓰기 세션 (명시적 Commit 필요)
using var session = factory.OpenSession();
session.Insert("Users.Insert", user);
session.Commit();

// 읽기 전용 세션 (autoCommit)
using var session = factory.OpenSession(autoCommit: true);
var list = session.SelectList<User>("Users.GetAll");
```

---

### OpenReadOnlySession

읽기 전용 세션을 열어 반환한다. 내부적으로 `autoCommit: true`로 동작한다.

```csharp
ISqlSession OpenReadOnlySession()
```

---

### OpenBatchSession

DbBatch API(.NET 8+)를 활용하는 배치 세션을 열어 반환한다. 여러 INSERT/UPDATE를 단일 라운드트립으로 처리한다. .NET 7에서는 일반 세션으로 폴백된다.

```csharp
ISqlSession OpenBatchSession()
```

**예제**

```csharp
using var batch = factory.OpenBatchSession();
batch.Insert("Items.Insert", item1);
batch.Insert("Items.Insert", item2);
batch.Insert("Items.Insert", item3);
batch.FlushStatements(); // 3건이 1 라운드트립으로 전송됨
batch.Commit();
```

---

### FromExistingConnection

외부에서 관리하는 `DbConnection`을 사용하는 세션을 생성한다. 커넥션/트랜잭션의 수명 관리는 호출자에게 위임된다. NuVatis가 커넥션을 닫거나 트랜잭션을 커밋/롤백하지 않는다.

```csharp
ISqlSession FromExistingConnection(DbConnection connection, DbTransaction? transaction = null)
```

**파라미터**

| 이름 | 타입 | 설명 |
|------|------|------|
| `connection` | `DbConnection` | 이미 열려 있는 DB 커넥션 |
| `transaction` | `DbTransaction?` | 진행 중인 트랜잭션. `null`이면 트랜잭션 없이 실행 |

**예제**

```csharp
await using var conn = new NpgsqlConnection(connectionString);
await conn.OpenAsync();
await using var tx = await conn.BeginTransactionAsync();

using var session = factory.FromExistingConnection(conn, tx);
session.Insert("Users.Insert", user);

await tx.CommitAsync(); // 외부에서 커밋
```

---

### AddInterceptor

SQL 실행 파이프라인에 인터셉터를 추가한다. 인터셉터는 등록 순서대로 `Before`가 실행되고, 역순으로 `After`가 실행된다.

```csharp
void AddInterceptor(ISqlInterceptor interceptor)
```

**예제**

```csharp
factory.AddInterceptor(new LoggingInterceptor(logger));
factory.AddInterceptor(new MetricsInterceptor());
factory.AddInterceptor(new OpenTelemetryInterceptor());
```

---

### Configuration 프로퍼티

팩토리에 적용된 설정을 반환한다.

```csharp
NuVatisConfiguration Configuration { get; }
```

---

## IDbProvider

**네임스페이스**: `NuVatis.Provider`

DB 프로바이더 인터페이스. DB 종류별로 구현된다.

```csharp
public interface IDbProvider
```

| 멤버 | 타입 | 설명 |
|------|------|------|
| `Name` | `string` | 프로바이더 식별명. 예: `"PostgreSql"`, `"MySql"` |
| `CreateConnection(string)` | `DbConnection` | 커넥션 문자열로 DB 커넥션 생성 |
| `ParameterPrefix` | `string` | SQL 파라미터 접두사. PostgreSQL: `"@"`, Oracle: `":"` |
| `GetParameterName(int)` | `string` | 인덱스 기반 파라미터 이름 생성. 예: `"@p0"`, `"@p1"` |
| `WrapIdentifier(string)` | `string` | 식별자 인용. PostgreSQL: `"\"name\""`, MySQL: `` "`name`" `` |

**내장 구현체**

| 클래스 | 패키지 | DB |
|--------|--------|-----|
| `PostgreSqlProvider` | NuVatis.PostgreSql | PostgreSQL (Npgsql) |
| `MySqlProvider` | NuVatis.MySql | MySQL / MariaDB (MySqlConnector) |
| `SqlServerProvider` | NuVatis.SqlServer | SQL Server (Microsoft.Data.SqlClient) |
| `SqliteProvider` | NuVatis.Sqlite | SQLite (Microsoft.Data.Sqlite) |

**커스텀 프로바이더 작성 예제**

```csharp
[NuVatisProvider("Oracle")]
public class OracleProvider : IDbProvider {
    public string Name            => "Oracle";
    public string ParameterPrefix => ":";

    public DbConnection CreateConnection(string connectionString)
        => new OracleConnection(connectionString);

    public string GetParameterName(int index) => $":p{index}";
    public string WrapIdentifier(string name) => $"\"{name}\"";
}
```

---

## ISqlInterceptor

**네임스페이스**: `NuVatis.Interceptor`

SQL 실행 전후에 횡단 관심사(로깅, 메트릭, 트레이싱)를 처리하는 인터페이스.

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
BeforeExecute (등록 순서대로)
    ↓
DB 쿼리 실행
    ↓
AfterExecute (등록 역순으로, 예외 시에도 호출됨)
```

---

## InterceptorContext

**네임스페이스**: `NuVatis.Interceptor`

인터셉터에 전달되는 SQL 실행 컨텍스트.

```csharp
public sealed class InterceptorContext
{
    public required string                     StatementId         { get; set; }
    public required string                     Sql                 { get; set; }
    public required IReadOnlyList<DbParameter> Parameters          { get; set; }
    public object?                             Parameter           { get; set; }
    public StatementType                       StatementType       { get; set; }
    public long                                ElapsedMilliseconds { get; set; }  // AfterExecute에서 설정됨
    public int?                                AffectedRows        { get; set; }  // AfterExecute에서 설정됨
    public Exception?                          Exception           { get; set; }  // 예외 발생 시 AfterExecute에서 설정됨
    public Dictionary<string, object?>         Items               { get; }       // 인터셉터 간 데이터 전달
}
```

| 프로퍼티 | 시점 | 설명 |
|----------|------|------|
| `StatementId` | Before/After | `"Namespace.MethodName"` 형태의 Statement ID |
| `Sql` | Before/After | 실행될 SQL 문자열. Before에서 수정 가능 |
| `Parameters` | Before/After | 바인딩된 DB 파라미터 목록 |
| `Parameter` | Before/After | 원본 파라미터 객체 |
| `StatementType` | Before/After | `Select`, `Insert`, `Update`, `Delete` |
| `ElapsedMilliseconds` | After만 | 실행 소요 시간 (밀리초) |
| `AffectedRows` | After만 | DML 영향 행 수 (`null`이면 Select) |
| `Exception` | After만 | 예외 발생 시 예외 객체. `null`이면 정상 실행 |
| `Items` | Before/After | 인터셉터 간 데이터 전달용 딕셔너리 |

**내장 인터셉터**

| 클래스 | 패키지 | 기능 |
|--------|--------|------|
| `LoggingInterceptor` | NuVatis.Core | SQL 및 소요시간 로깅 |
| `MetricsInterceptor` | NuVatis.Core | Prometheus 메트릭 수집 |
| `OpenTelemetryInterceptor` | NuVatis.Extensions.OpenTelemetry | 분산 추적 (Activity) |

---

## ITypeHandler

**네임스페이스**: `NuVatis.Mapping`

DB 타입과 .NET 타입 간 변환을 담당하는 인터페이스. 기본 타입 변환이 지원되지 않는 경우 구현한다.

```csharp
public interface ITypeHandler
{
    Type    TargetType { get; }
    object? GetValue(DbDataReader reader, int ordinal);
    void    SetParameter(DbParameter parameter, object? value);
}
```

| 멤버 | 설명 |
|------|------|
| `TargetType` | 이 핸들러가 처리하는 .NET 타입 |
| `GetValue` | DB에서 읽어온 값을 .NET 타입으로 변환. `reader.IsDBNull(ordinal)` 확인 필수 |
| `SetParameter` | .NET 값을 DB 파라미터로 변환. `parameter.Value = ...` 설정 |

**내장 TypeHandler**

| 클래스 | TargetType | 변환 |
|--------|-----------|------|
| `DateOnlyTypeHandler` | `DateOnly` | `DateTime` ↔ `DateOnly` |
| `TimeOnlyTypeHandler` | `TimeOnly` | `TimeSpan` ↔ `TimeOnly` |
| `EnumStringTypeHandler<T>` | `Enum` | `string` ↔ `Enum` (이름 기반) |
| `JsonTypeHandler<T>` | `T` | JSON 문자열 ↔ POCO |

**등록 방법**

```csharp
// DI 방식
builder.Services.AddNuVatis(options => {
    options.RegisterTypeHandler<DateOnly>(new DateOnlyTypeHandler());
    options.RegisterTypeHandler<MyEnum>(new EnumStringTypeHandler<MyEnum>());
});

// Non-DI 방식
var factory = new SqlSessionFactoryBuilder()
    .RegisterTypeHandler<DateOnly>(new DateOnlyTypeHandler())
    .Build();
```

---

## SqlIdentifier

**네임스페이스**: `NuVatis.Core.Sql`

SQL 식별자(테이블명, 컬럼명 등)를 타입 안전하게 래핑하는 sealed 클래스. `${}` 문자열 치환 시 `string` 대신 사용하여 SQL Injection을 방지한다.

```csharp
public sealed class SqlIdentifier
```

생성자는 private이며 반드시 아래 팩토리 메서드로만 생성한다.

---

### SqlIdentifier.From

```csharp
public static SqlIdentifier From(string value)
```

**파라미터**: `value` — SQL 식별자로 사용할 문자열. 빈 문자열 불가.

**예외**
- `ArgumentNullException` — `value`가 `null`
- `ArgumentException` — 빈 문자열이거나 SQL Injection 패턴 감지

**차단하는 패턴**

| 종류 | 패턴 |
|------|------|
| 금지 문자 | `;` `'` `"` `\n` `\r` `\0` |
| 금지 시퀀스 | `--` `/*` `*/` |
| 금지 키워드 | `union` `select` `drop` `insert` `or` `and` (단어 경계 기준) |

**주의**: 리터럴 상수에만 사용한다. 사용자 입력에는 `FromAllowed`를 사용한다.

```csharp
// 올바른 사용: 코드에 하드코딩된 상수
var identifier = SqlIdentifier.From("created_at");
```

---

### SqlIdentifier.FromEnum\<T\>

```csharp
public static SqlIdentifier FromEnum<T>(T value) where T : struct, Enum
```

**파라미터**: `value` — SQL 식별자로 사용할 enum 값. Flags enum 조합 불가.

**예외**: `ArgumentException` — Flags enum 조합 값 (예: `ReadWrite = Read | Write`)

**사용 예제**

```csharp
public enum SortColumn { CreatedAt, UserName, Id }

// enum 이름이 그대로 SQL에 삽입됨
var col = SqlIdentifier.FromEnum(SortColumn.CreatedAt); // → "CreatedAt"
mapper.GetSorted(new { Column = col });
```

```xml
<select id="GetSorted" resultMap="UserResult">
  SELECT * FROM users ORDER BY ${Column}
</select>
```

---

### SqlIdentifier.FromAllowed

```csharp
public static SqlIdentifier FromAllowed(string value, params string[] allowedValues)
```

**파라미터**

| 이름 | 타입 | 설명 |
|------|------|------|
| `value` | `string` | 검증할 사용자 입력 문자열 |
| `allowedValues` | `string[]` | 허용된 식별자 목록. 대소문자 구분 없음 |

**예외**: `ArgumentException` — `value`가 `allowedValues`에 없거나 SQL Injection 패턴 감지

**사용 예제**

```csharp
// API에서 사용자가 정렬 컬럼을 선택하는 경우
public IList<User> GetSorted(string userInput) {
    var col = SqlIdentifier.FromAllowed(
        userInput,
        "id", "user_name", "created_at");  // 허용 목록에 없으면 ArgumentException
    return _mapper.GetSorted(new { Column = col });
}
```

---

### SqlIdentifier.JoinTyped\<T\>

`struct` 타입 컬렉션을 `WHERE IN` 절에 안전하게 인라인할 수 있는 쉼표 구분 문자열로 변환한다. `struct` 제약으로 컴파일 타임에 임의 문자열 입력을 차단하므로 SQL Injection이 불가능하다.

```csharp
public static string JoinTyped<T>(IEnumerable<T> values) where T : struct
```

**파라미터**: `values` — `WHERE IN` 절에 인라인할 `struct` 타입 컬렉션.

**반환값**: `string` — 쉼표로 구분된 SQL 리터럴.

**타입별 변환 규칙**

| 타입 | 출력 예시 | 따옴표 |
|------|-----------|--------|
| `int`, `long`, `decimal`, 기타 숫자형 | `1,2,3` | 없음 |
| `Guid` | `'550e8400-e29b-41d4-a716-446655440000'` | 있음 |
| `DateTime`, `DateTimeOffset` | `'2026-03-01T00:00:00'` | 있음 |
| `DateOnly`, `TimeOnly` | `'2026-03-01'` | 있음 |

**예외**
- `ArgumentNullException` — `values`가 `null`
- `ArgumentException` — `values`가 빈 컬렉션

**사용 예제**

```csharp
var ids      = new List<int> { 1, 2, 3, 4 };
var inClause = SqlIdentifier.JoinTyped(ids); // → "1,2,3,4"
var sql      = $"SELECT * FROM orders WHERE id IN ({inClause})";

// Guid 컬렉션
var guids    = new[] { Guid.NewGuid(), Guid.NewGuid() };
var clause2  = SqlIdentifier.JoinTyped(guids); // → "'guid1','guid2'"
```

---

## NuVatisOptions (DI 설정)

**네임스페이스**: `NuVatis.Extensions.DependencyInjection`

`AddNuVatis()` 호출 시 전달하는 설정 객체.

```csharp
public sealed class NuVatisOptions
{
    public string       ConnectionString { get; set; }
    public IDbProvider  Provider         { get; set; }
    public bool         AutoCommit       { get; set; } = false;
    public int          DefaultTimeout   { get; set; } = 30;

    public void RegisterMappers(Action<Type, ISqlSession, object> factory) { ... }
    public void RegisterAttributeStatements(Action<...> registry) { ... }
    public void AddInterceptor(ISqlInterceptor interceptor) { ... }
    public void RegisterTypeHandler<T>(ITypeHandler handler) { ... }
}
```

**설정 항목**

| 항목 | 타입 | 기본값 | 설명 |
|------|------|--------|------|
| `ConnectionString` | `string` | (필수) | DB 연결 문자열 |
| `Provider` | `IDbProvider` | (필수) | DB 프로바이더 |
| `AutoCommit` | `bool` | `false` | `true`이면 모든 세션이 autoCommit 모드 |
| `DefaultTimeout` | `int` | `30` | 기본 커맨드 타임아웃 (초). Statement별 설정으로 오버라이드 가능 |

**전체 설정 예제**

```csharp
builder.Services.AddNuVatis(options => {
    options.ConnectionString = builder.Configuration.GetConnectionString("Default")!;
    options.Provider         = new PostgreSqlProvider();
    options.AutoCommit       = false;
    options.DefaultTimeout   = 30;

    // Source Generator가 생성한 Mapper 등록
    options.RegisterMappers(NuVatisMapperRegistry.RegisterAll);
    options.RegisterAttributeStatements(NuVatisMapperRegistry.RegisterAttributeStatements);

    // 인터셉터 등록 (등록 순서대로 Before 실행, 역순으로 After 실행)
    options.AddInterceptor(new LoggingInterceptor(loggerFactory.CreateLogger<LoggingInterceptor>()));
    options.AddInterceptor(new MetricsInterceptor());

    // 커스텀 TypeHandler 등록
    options.RegisterTypeHandler<DateOnly>(new DateOnlyTypeHandler());
    options.RegisterTypeHandler<OrderStatus>(new EnumStringTypeHandler<OrderStatus>());
});

// Health Check 등록
builder.Services.AddHealthChecks().AddNuVatis("database");
```

---

## Attributes

**네임스페이스**: `NuVatis.Attributes`

| Attribute | 적용 대상 | 설명 |
|-----------|----------|------|
| `[NuVatisMapper]` | `interface` | Source Generator 스캔 대상으로 명시적 opt-in |
| `[Select(sql)]` | `method` | 인라인 SELECT SQL. `sql`은 NuVatis 파라미터 문법(`#{}`, `${}`) 사용 |
| `[Insert(sql)]` | `method` | 인라인 INSERT SQL |
| `[Update(sql)]` | `method` | 인라인 UPDATE SQL |
| `[Delete(sql)]` | `method` | 인라인 DELETE SQL |
| `[ResultMap(id)]` | `method` | XML에 정의된 ResultMap 참조 |
| `[SqlConstant]` | `field`, `property` | NV004 억제. 이 필드/프로퍼티의 값은 안전한 SQL 상수로 간주 |
| `[NuVatisProvider(name)]` | `class` | Provider 자동 발견. DI 컨테이너에서 이름으로 resolve 가능 |

**예제**

```csharp
[NuVatisMapper]
public interface IOrderMapper {
    [Select("SELECT * FROM orders WHERE id = #{Id}")]
    [ResultMap("OrderResult")]
    Order? GetById(int id);

    [Insert("INSERT INTO orders (user_id, amount) VALUES (#{UserId}, #{Amount})")]
    int Create(CreateOrderParam param);

    [Update("UPDATE orders SET status = #{Status} WHERE id = #{Id}")]
    int UpdateStatus(UpdateStatusParam param);

    [Delete("DELETE FROM orders WHERE id = #{Id}")]
    int Delete(int id);
}

// SqlConstant 사용 예
public static class TableRefs {
    [SqlConstant] public const string Orders = "orders";
    [SqlConstant] public const string Users  = "users";
}
```

---

## NuVatis.Testing

**패키지**: `NuVatis.Testing`

단위 테스트에서 실제 DB 없이 `ISqlSession`을 목(Mock)으로 대체한다.

### InMemorySqlSession

```csharp
public sealed class InMemorySqlSession : ISqlSession
```

| 메서드 | 설명 |
|--------|------|
| `Setup<T>(string statementId, T result)` | 단일 결과 사전 등록 |
| `SetupList<T>(string statementId, IList<T> results)` | 리스트 결과 사전 등록 |
| `RegisterMapper<T>(T mapper)` | 테스트용 Mapper 인스턴스 등록 |
| `ClearCaptures()` | 캡처된 쿼리 이력 초기화 |
| `CapturedQueries` | 실행된 쿼리 이력 (`IReadOnlyList<CapturedQuery>`) |

**제한사항**: `SelectMultiple`, `SelectMultipleAsync`는 `NotSupportedException` 발생.

### QueryCapture

```csharp
public static class QueryCapture
```

| 메서드 | 반환값 | 설명 |
|--------|--------|------|
| `HasQuery(session, statementId)` | `bool` | 해당 Statement가 1회 이상 실행됐는지 확인 |
| `QueryCount(session, statementId)` | `int` | 해당 Statement가 실행된 횟수 반환 |
| `GetCaptured(session, statementId)` | `IReadOnlyList<CapturedQuery>` | 실행 이력 전체 반환 |

### CapturedQuery

```csharp
public sealed record CapturedQuery(string StatementId, object? Parameter, string Operation)
```

**테스트 예제**

```csharp
[Fact]
public async Task CreateOrder_Inserts_And_Returns_Id() {
    // Arrange
    var session = new InMemorySqlSession();
    session.Setup("OrderMapper.Insert", 42);  // Insert가 42를 반환하도록 설정

    var service = new OrderService(session);

    // Act
    var id = await service.CreateOrderAsync(new CreateOrderDto { Amount = 100m });

    // Assert
    Assert.Equal(42, id);
    Assert.True(QueryCapture.HasQuery(session, "OrderMapper.Insert"));
    Assert.Equal(1, QueryCapture.QueryCount(session, "OrderMapper.Insert"));
}
```

---

## Source Generator 진단 코드

| 코드 | 심각도 | 발생 조건 |
|------|--------|-----------|
| NV001 | Error | XML 매퍼 파싱 실패 또는 ResultMap을 찾을 수 없음 |
| NV002 | Error | 인터페이스 메서드에 매칭되는 Statement가 없음 |
| NV003 | Error | 파라미터 타입에 지정된 프로퍼티가 없음 |
| NV004 | Error | `${}` 문자열 치환 파라미터 타입이 `string` — SQL Injection 위험 |
| NV005 | Error | `<if test="...">` 표현식 컴파일 실패 |
| NV006 | Info | ResultMap 컬럼이 타입 프로퍼티와 매칭되지 않음 |
| NV007 | Warning | 미사용 ResultMap (어떤 Statement에서도 참조되지 않음) |
| NV008 | Warning | `[NuVatisMapper]` 없이 SQL Attribute만 사용 (권장되지 않음) |

각 코드의 상세 설명, 재현 예제, 해결 방법은 [Diagnostic Codes 가이드](../reference/diagnostic-codes.md)를 참조한다.
