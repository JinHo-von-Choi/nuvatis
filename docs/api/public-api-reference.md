# NuVatis Public API Reference

작성자: 최진호
작성일: 2026-02-26
버전: v1.0.0-GA

---

## API 호환성 정책

NuVatis는 Semantic Versioning 2.0.0을 준수한다.

- Major (x.0.0): 하위 호환성이 깨지는 public API 변경
- Minor (0.x.0): 하위 호환되는 기능 추가
- Patch (0.0.x): 버그 수정

v1.0.0부터 PublicApiAnalyzers가 CI에서 모든 public API 변경을 감시한다.
의도하지 않은 public API 노출이 발생하면 빌드가 실패한다.

### API 변경 절차

1. `PublicAPI.Unshipped.txt`에 새 심볼 추가
2. PR 리뷰에서 API 변경 승인
3. 릴리스 시 `Unshipped.txt` -> `Shipped.txt`로 이동
4. 삭제된 API는 `PublicAPI.Shipped.txt`에서 `*REMOVED*` 접두사 추가

---

## 패키지 구조

| 패키지 | 설명 | 대상 |
|--------|------|------|
| NuVatis.Core | 핵심 라이브러리 (ISqlSession, 매핑, 캐시) | net7.0; net8.0 |
| NuVatis.Generators | Roslyn Source Generator (컴파일 타임) | netstandard2.0 |
| NuVatis.PostgreSql | PostgreSQL Provider (Npgsql) | net7.0; net8.0 |
| NuVatis.MySql | MySQL/MariaDB Provider (MySqlConnector) | net7.0; net8.0 |
| NuVatis.SqlServer | SQL Server Provider (Microsoft.Data.SqlClient) | net7.0; net8.0 |
| NuVatis.Sqlite | SQLite Provider (Microsoft.Data.Sqlite) | net7.0; net8.0 |
| NuVatis.Extensions.DependencyInjection | ASP.NET Core DI 통합 | net7.0; net8.0 |
| NuVatis.Extensions.OpenTelemetry | OpenTelemetry 트레이싱 | net7.0; net8.0 |
| NuVatis.Extensions.EntityFrameworkCore | EF Core 하이브리드 | net7.0; net8.0 |
| NuVatis.Extensions.Aspire | .NET Aspire 통합 | net8.0 |
| NuVatis.Testing | 테스트 유틸리티 (InMemorySqlSession) | net7.0; net8.0 |

---

## 핵심 인터페이스

### ISqlSession

SQL 세션. 모든 DB 작업의 진입점.

```csharp
public interface ISqlSession : IDisposable, IAsyncDisposable {
    SelectOne<T>(string statementId, object? parameter = null)
    SelectList<T>(string statementId, object? parameter = null)
    SelectOneAsync<T>(string statementId, object? parameter = null, CancellationToken ct = default)
    SelectListAsync<T>(string statementId, object? parameter = null, CancellationToken ct = default)
    SelectStream<T>(string statementId, object? parameter = null)
    SelectMultiple(string statementId, object? parameter = null)
    SelectMultipleAsync(string statementId, object? parameter = null, CancellationToken ct = default)
    Insert(string statementId, object? parameter = null)
    InsertAsync(string statementId, object? parameter = null, CancellationToken ct = default)
    Update(string statementId, object? parameter = null)
    UpdateAsync(string statementId, object? parameter = null, CancellationToken ct = default)
    Delete(string statementId, object? parameter = null)
    DeleteAsync(string statementId, object? parameter = null, CancellationToken ct = default)
    Commit()
    CommitAsync(CancellationToken ct = default)
    Rollback()
    RollbackAsync(CancellationToken ct = default)
    FlushStatements()
    FlushStatementsAsync(CancellationToken ct = default)
    ExecuteInTransactionAsync(Func<ISqlSession, Task> action, CancellationToken ct = default)
    GetMapper<T>()
    DbConnection Connection { get; }
}
```

### ISqlSessionFactory

세션 팩토리. Singleton으로 등록.

```csharp
public interface ISqlSessionFactory {
    OpenSession(bool autoCommit = false)
    OpenReadOnlySession()
    OpenBatchSession()
    FromExistingConnection(DbConnection connection)
    SetMapperFactory(Func<Type, ISqlSession, object> factory)
    AddInterceptor(ISqlInterceptor interceptor)
    Configuration { get; }
}
```

### IDbProvider

DB 프로바이더 인터페이스. 각 DB별 패키지에서 구현.

```csharp
public interface IDbProvider {
    string Name { get; }
    DbConnection CreateConnection(string connectionString)
    string ParameterPrefix { get; }
    string GetParameterName(int index)
    string WrapIdentifier(string name)
}
```

### ISqlInterceptor

SQL 실행 전후 가로채기.

```csharp
public interface ISqlInterceptor {
    void BeforeExecute(InterceptorContext context)
    void AfterExecute(InterceptorContext context)
    Task BeforeExecuteAsync(InterceptorContext context, CancellationToken ct)
    Task AfterExecuteAsync(InterceptorContext context, CancellationToken ct)
}
```

### ITypeHandler

커스텀 타입 변환 핸들러.

```csharp
public interface ITypeHandler {
    Type TargetType { get; }
    object? GetValue(DbDataReader reader, int ordinal)
    void SetParameter(DbParameter parameter, object? value)
}
```

---

## Attributes (컴파일 타임)

| Attribute | 대상 | 설명 |
|-----------|------|------|
| `[NuVatisMapper]` | interface | Mapper 인터페이스 선언 |
| `[Select(sql)]` | method | 인라인 SELECT SQL |
| `[Insert(sql)]` | method | 인라인 INSERT SQL |
| `[Update(sql)]` | method | 인라인 UPDATE SQL |
| `[Delete(sql)]` | method | 인라인 DELETE SQL |
| `[ResultMap(id)]` | method | ResultMap 참조 |
| `[SqlConstant]` | field/property | 안전한 SQL 상수 표시 |
| `[NuVatisProvider(name)]` | class | Provider 자동 발견 |

---

## 빌트인 TypeHandler

| Handler | TargetType | 설명 |
|---------|-----------|------|
| `DateOnlyTypeHandler` | DateOnly | DateTime <-> DateOnly (.NET 8+) |
| `TimeOnlyTypeHandler` | TimeOnly | TimeSpan <-> TimeOnly (.NET 8+) |
| `EnumStringTypeHandler<T>` | Enum | Enum <-> string |
| `JsonTypeHandler<T>` | T | JSON 직렬화/역직렬화 |

---

## DI 확장

```csharp
services.AddNuVatis(options => {
    options.ConnectionString = "Host=localhost;...";
    options.Provider         = new PostgreSqlProvider();
    options.AddInterceptor(new OpenTelemetryInterceptor());
    options.RegisterMappers(NuVatisMapperRegistry.RegisterAll);
});

services.AddHealthChecks().AddNuVatis("nuvatis-db");
```

---

## Aspire 확장

```csharp
builder.AddNuVatis("nuvatis-db", new PostgreSqlProvider());
```

---

## 소스 생성기 진단 코드

| 코드 | 심각도 | 설명 |
|------|--------|------|
| NV001 | Warning | XML 매퍼 파싱 실패 |
| NV002 | Warning | ResultMap 미참조 |
| NV003 | Warning | ResultMap 프로퍼티 불일치 |
| NV004 | Warning | 문자열 치환 (${ }) SQL Injection 위험 |
| NV005 | Warning | 미사용 결과 매핑 |
| NV006 | Info | 매퍼 메서드 생성 정보 |
