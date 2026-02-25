# Configuration

## DI 기반 설정

```csharp
builder.Services.AddNuVatis(options => {
    options.ConnectionString = builder.Configuration.GetConnectionString("Default");
    options.Provider         = new PostgreSqlProvider();
    options.AutoCommit       = false;    // 기본값: false (명시적 Commit 필요)
    options.DefaultTimeout   = 30;       // 기본 커맨드 타임아웃 (초)
    options.RegisterMappers(NuVatisMapperRegistry.RegisterAll);
    options.RegisterAttributeStatements(NuVatisMapperRegistry.RegisterAttributeStatements);
});
```

## 수동 설정 (Non-DI)

```csharp
var config = new NuVatisConfigurationBuilder()
    .ConnectionString("Host=localhost;Database=mydb;Username=app;Password=***")
    .Provider(new PostgreSqlProvider())
    .AddXmlMapper("Mappers/UserMapper.xml")
    .Build();

var factory = new SqlSessionFactory(config);
using var session = factory.OpenSession();
```

## Provider 목록

| Provider | Class | NuGet Package |
|----------|-------|---------------|
| PostgreSQL | `PostgreSqlProvider` | NuVatis.PostgreSql |
| MySQL/MariaDB | `MySqlProvider` | NuVatis.MySql |
| SQL Server | `SqlServerProvider` | NuVatis.SqlServer |

## autoCommit 모드

| 모드 | 동작 |
|------|------|
| `false` (기본) | Commit 없이 Dispose 시 자동 Rollback. 안전한 기본값 |
| `true` | 매 실행 후 자동 커밋. 읽기 전용 세션에 적합 |

## Command Timeout

Statement 단위로 타임아웃을 설정할 수 있다. 우선순위: Statement > Session Default.

```xml
<select id="HeavyReport" commandTimeout="120">
  SELECT ... FROM large_aggregate_table ...
</select>
```

## Second-Level Cache

```xml
<cache eviction="LRU" flushInterval="600000" size="512" />

<select id="GetStats" useCache="true">
  SELECT ... FROM stats WHERE month = #{Month}
</select>
```

- `eviction`: LRU (Least Recently Used)
- `flushInterval`: 자동 갱신 주기 (밀리초)
- `size`: 최대 캐시 항목 수

Insert/Update/Delete 실행 시 해당 namespace의 캐시가 자동 무효화된다.

## Interceptors

```csharp
builder.Services.AddNuVatis(options => {
    options.AddInterceptor(new MetricsInterceptor());
    options.AddInterceptor(new OpenTelemetryInterceptor());
});
```

## Health Check

```csharp
builder.Services.AddHealthChecks().AddNuVatis();
```

`SELECT 1` 핑 쿼리로 DB 연결 상태를 검증한다.
