# NuVatis

[![CI](https://github.com/JinHo-von-Choi/nuvatis/actions/workflows/ci.yml/badge.svg)](https://github.com/JinHo-von-Choi/nuvatis/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/vpre/NuVatis.Core)](https://www.nuget.org/packages/NuVatis.Core)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

MyBatis-style SQL Mapper for .NET, powered by Roslyn Source Generators.

## Overview

NuVatis는 Entity Framework의 성능 오버헤드와 인라인 SQL의 유지보수성 문제를 동시에 해결하는 SQL Mapper 프레임워크다.

- SQL은 XML 또는 C# Attribute로 별도 관리
- Roslyn Source Generator가 빌드타임에 매핑 코드를 자동 생성
- 런타임 리플렉션 제로, Native AOT 호환 (.NET 8+)
- ADO.NET 기반 최소 추상화, 최대 성능
- .NET 7 / 8 / 9 / 10 동시 지원 (멀티 타겟)
- `SqlIdentifier` 타입으로 `${}` 문자열 치환 런타임 검증 (SQL Injection 방어)

## When NOT to Use NuVatis

NuVatis가 모든 상황에 적합하지는 않다. 아래 표를 보고 올바른 도구를 선택하라.

**EF Core가 더 적합한 경우:**

| 케이스 | 이유 |
|--------|------|
| 5개 이상의 optional filter를 동적으로 조합 | EF Core의 IQueryable 체이닝이 압도적으로 편리하다 |
| 단순 CRUD 위주, 복잡한 쿼리 없음 | EF Core + Repository 패턴으로 충분하다 |
| 팀에 SQL 전문가가 없음 | EF Core의 자동 쿼리 생성이 더 안전하다 |
| Code-first 마이그레이션이 워크플로의 중심 | EF Core Migrations가 이를 직접 지원한다 |

**Dapper가 더 적합한 경우:**

| 케이스 | 이유 |
|--------|------|
| 쿼리 수가 적고 XML 관리가 부담 | Dapper는 인라인 SQL을 직접 작성한다 |
| 라이브러리를 최소한으로 유지하고 싶음 | Dapper는 단일 파일 수준의 단순성을 제공한다 |

**NuVatis가 강한 케이스:**

| 케이스 | 이유 |
|--------|------|
| Java MyBatis 경험자의 .NET 이식 | XML 매퍼 문법이 동일하다 |
| 수백 개의 레거시 SQL을 그대로 관리 | SQL을 코드에서 분리하여 버전 관리한다 |
| 동적 SQL이 많지만 타입 안전성 필요 | `<if>`/`<where>`/`<foreach>` + NV004 컴파일 에러 |
| 복잡한 JOIN + 집계 쿼리를 직접 제어 | SQL을 수정하면 즉시 반영된다 |
| Native AOT 환경 | Source Generator가 리플렉션 없는 코드를 생성한다 |

> **TL;DR**: EF Core는 동적 쿼리 조합에, NuVatis는 복잡한 정적 SQL 관리에 사용하라.
> 동일 프로젝트에서 함께 쓰는 하이브리드 패턴도 지원한다.
> → [EF Core + NuVatis 하이브리드 가이드](docs/cookbook/hybrid-efcore-nuvatis.md)

## Packages

| Package | Description |
|---------|------------|
| `NuVatis.Core` | 핵심 런타임 (Session, Executor, Transaction, Mapping, Cache) |
| `NuVatis.Generators` | Roslyn Source Generator (XML 파싱, 분석, 코드 생성) |
| `NuVatis.PostgreSql` | PostgreSQL Provider (Npgsql) |
| `NuVatis.MySql` | MySQL/MariaDB Provider (MySqlConnector) |
| `NuVatis.SqlServer` | SQL Server Provider (Microsoft.Data.SqlClient) |
| `NuVatis.Sqlite` | SQLite Provider (Microsoft.Data.Sqlite) |
| `NuVatis.Extensions.DependencyInjection` | Microsoft DI 통합 + Health Check |
| `NuVatis.Extensions.OpenTelemetry` | OpenTelemetry 분산 추적 (ActivitySource) |
| `NuVatis.Extensions.EntityFrameworkCore` | EF Core DbContext 커넥션/트랜잭션 공유 |
| `NuVatis.Extensions.Aspire` | .NET Aspire 통합 (Health Check + OTel 자동 구성) |
| `NuVatis.Testing` | 테스트 지원 (InMemorySqlSession, QueryCapture) |

## Quick Start

### Installation

```bash
dotnet add package NuVatis.Core
dotnet add package NuVatis.Generators
dotnet add package NuVatis.PostgreSql
dotnet add package NuVatis.Extensions.DependencyInjection
```

### Mapper Interface

```csharp
public interface IUserMapper {
    User? GetById(int id);
    Task<User?> GetByIdAsync(int id, CancellationToken ct = default);
    IList<User> Search(UserSearchParam param);
    int Insert(User user);
    int Update(User user);
    int Delete(int id);
}
```

### XML Mapper

```xml
<?xml version="1.0" encoding="utf-8" ?>
<mapper namespace="Sample.Mappers.IUserMapper">

  <cache eviction="LRU" flushInterval="600000" size="512" />

  <resultMap id="UserResult" type="User">
    <id column="id" property="Id" />
    <result column="user_name" property="UserName" />
    <result column="email" property="Email" />
  </resultMap>

  <select id="GetById" resultMap="UserResult">
    SELECT id, user_name, email FROM users WHERE id = #{Id}
  </select>

  <select id="Search" resultMap="UserResult">
    SELECT id, user_name, email FROM users
    <where>
      <if test="UserName != null">
        AND user_name LIKE #{UserName}
      </if>
      <foreach collection="Ids" item="id"
               open="AND id IN (" separator="," close=")">
        #{id}
      </foreach>
    </where>
  </select>

  <insert id="Insert">
    INSERT INTO users (user_name, email) VALUES (#{UserName}, #{Email})
  </insert>

</mapper>
```

### C# Attribute (Static SQL)

```csharp
public interface IUserMapper {
    [Select("SELECT id, user_name, email FROM users WHERE id = #{Id}")]
    [ResultMap("UserResult")]
    User? GetById(int id);

    [Insert("INSERT INTO users (user_name, email) VALUES (#{UserName}, #{Email})")]
    int Insert(User user);
}
```

### DI Integration (ASP.NET Core)

```csharp
builder.Services.AddNuVatis(options => {
    options.ConnectionString = builder.Configuration.GetConnectionString("Default");
    options.Provider         = new PostgreSqlProvider();
    options.RegisterMappers(NuVatisMapperRegistry.RegisterAll);
    options.RegisterAttributeStatements(NuVatisMapperRegistry.RegisterAttributeStatements);
});

builder.Services.AddHealthChecks().AddNuVatis();
```

### Usage

```csharp
public class UserService {
    private readonly IUserMapper _mapper;

    public UserService(IUserMapper mapper) {
        _mapper = mapper;
    }

    public async Task<User?> GetUser(int id) {
        return await _mapper.GetByIdAsync(id);
    }
}
```

### Manual Session (Non-DI)

```csharp
using var session = factory.OpenSession();
var mapper = session.GetMapper<IUserMapper>();

var user = mapper.GetById(1);
mapper.Insert(newUser);
session.Commit();
```

## Session Lifecycle

| Environment | Registration | Lifecycle |
|------------|-------------|-----------|
| ASP.NET Core (DI) | Scoped | Per HTTP request |
| Generic Host (DI) | Scoped | Per scope |
| Console/Batch | Manual | using block |

- `autoCommit: false` (default) - MyBatis 호환. Commit 없이 Dispose 시 자동 Rollback
- `autoCommit: true` - 각 쿼리 후 즉시 커밋
- Lazy Connection - 첫 쿼리 시점에 DB 연결 개시

## Transaction Management

```csharp
using var session = factory.OpenSession();
var mapper = session.GetMapper<IUserMapper>();

mapper.Insert(user);
mapper.Insert(order);
session.Commit();
```

ExecuteInTransactionAsync:

```csharp
await session.ExecuteInTransactionAsync(async () => {
    await mapper.InsertAsync(user);
    await mapper.InsertAsync(order);
});
```

## Streaming (IAsyncEnumerable)

대용량 결과를 메모리에 모두 적재하지 않고 스트리밍으로 소비한다.

```csharp
await foreach (var row in session.SelectStream<StatRow>("Stats.GetAll")) {
    Process(row);
}
```

## Multi-ResultSet

하나의 SQL에서 반환되는 여러 결과셋을 순차 소비한다.

```csharp
await using var results = await session.SelectMultipleAsync("Dashboard.Overview", param);
var summary = await results.ReadAsync<DashboardSummary>();
var details = await results.ReadListAsync<DashboardDetail>();
var trends  = await results.ReadListAsync<TrendData>();
```

## Second-Level Cache

Namespace 단위 LRU 캐시. Select 시 캐시 히트하면 DB를 건너뛴다. Insert/Update/Delete 실행 시 해당 namespace 캐시를 자동 무효화한다.

XML 설정:
```xml
<cache eviction="LRU" flushInterval="600000" size="512" />

<select id="GetMonthlyStats" resultMap="StatsResult" useCache="true">
    SELECT ... FROM monthly_stats WHERE month = #{Month}
</select>
```

ICacheProvider 인터페이스를 통해 Redis 등 외부 캐시로 교체 가능하다.

## EF Core Integration

EF Core와 동일 트랜잭션 내에서 NuVatis 쿼리를 실행한다. DbConnection/DbTransaction을 자동 공유한다.

```csharp
builder.Services.AddNuVatis(options => { ... });
builder.Services.AddNuVatisEntityFrameworkCore<AppDbContext>();
```

수동 사용:
```csharp
await using var nuvatisSession = await dbContext.OpenNuVatisSessionAsync(factory);
var stats = await nuvatisSession.SelectListAsync<MonthlyStats>("Stats.GetMonthly");
```

## Interceptors

SQL 실행 전후에 횡단 관심사를 처리한다.

### Prometheus Metrics (내장)

```csharp
factory.AddInterceptor(new MetricsInterceptor());
```

Meter "NuVatis": `nuvatis.query.total`, `nuvatis.query.duration`, `nuvatis.query.errors.total`

### OpenTelemetry Tracing

```csharp
factory.AddInterceptor(new OpenTelemetryInterceptor());
```

ActivitySource "NuVatis.SqlSession" 기반 분산 추적. 태그: `db.system`, `db.statement`, `db.operation`, `otel.status_code`

## Health Check (ASP.NET Core)

```csharp
builder.Services.AddHealthChecks().AddNuVatis();
```

`SELECT 1` 핑 쿼리로 DB 연결 상태를 검증한다. `__nuvatis_health` Statement가 자동 등록된다.

## CommandTimeout

Statement 단위로 SQL 실행 타임아웃을 설정할 수 있다. 우선순위: Statement > Session Default.

```xml
<select id="HeavyReport" commandTimeout="120">
    SELECT ... FROM large_table ...
</select>
```

## Dynamic SQL Tags

| Tag | Description |
|-----|------------|
| `<if test="...">` | 조건부 SQL |
| `<choose>/<when>/<otherwise>` | Switch-case |
| `<where>` | 자동 WHERE 절 처리 |
| `<set>` | 자동 SET 절 처리 |
| `<foreach>` | 컬렉션 반복 |
| `<bind>` | 변수 바인딩 (OGNL 표현식) |
| `<sql>/<include>` | SQL 프래그먼트 재사용 |

## SQL Injection Defense (SqlIdentifier)

`${}` 문자열 치환은 v2.0.0부터 파라미터 타입이 `string`이면 NV004 **빌드 오류**가 발생한다.
동적 테이블명·컬럼명처럼 `${}` 가 불가피한 경우 `SqlIdentifier` 타입을 사용한다.

```csharp
using NuVatis.Core.Sql;

// 1. enum 기반 (가장 안전)
public enum SortColumn { CreatedAt, UserName, Id }
mapper.GetSorted(new { Column = SqlIdentifier.FromEnum(SortColumn.CreatedAt) });

// 2. 화이트리스트 기반 (사용자 입력 허용)
mapper.GetSorted(new {
    Column = SqlIdentifier.FromAllowed(userInput, "id", "created_at", "user_name")
});
```

```xml
<select id="GetSorted" resultMap="UserResult">
  SELECT * FROM users ORDER BY ${Column}
</select>
```

마이그레이션 가이드: [CHANGELOG.md v2.0.0](CHANGELOG.md) | [SQL Injection Prevention](docs/security/sql-injection-prevention.md)

## External Connection Sharing

외부에서 관리하는 DbConnection/DbTransaction을 NuVatis에서 사용할 수 있다. 커넥션/트랜잭션 수명 관리는 외부 호출자에 위임된다.

```csharp
using var session = factory.FromExistingConnection(connection, transaction);
var data = session.SelectList<Item>("Items.GetAll");
```

## Testing

```csharp
var session = new InMemorySqlSession();
session.Setup("UserMapper.GetById", expectedUser);

var result = session.SelectOne<User>("UserMapper.GetById");

Assert.True(QueryCapture.HasQuery(session, "UserMapper.GetById"));
Assert.Equal(1, QueryCapture.QueryCount(session, "UserMapper.GetById"));
```

## Custom DB Provider

```csharp
[NuVatisProvider("CustomDb")]
public class CustomDbProvider : IDbProvider {
    public string Name => "CustomDb";
    public DbConnection CreateConnection(string connectionString)
        => new CustomDbConnection(connectionString);
    public string ParameterPrefix => "@";
    public string GetParameterName(int index) => $"@p{index}";
    public string WrapIdentifier(string name) => $"\"{name}\"";
}
```

## XML Schema Validation

NuVatis.Core 패키지에 XML 스키마 파일이 포함되어 있다. IDE에서 XML 매퍼 작성 시 자동완성 및 유효성 검사에 활용할 수 있다.

```xml
<?xml version="1.0" encoding="utf-8" ?>
<mapper namespace="..."
        xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
        xsi:noNamespaceSchemaLocation="schemas/nuvatis-mapper.xsd">
```

| Schema | Description |
|--------|------------|
| `schemas/nuvatis-mapper.xsd` | Mapper XML 스키마 (select, insert, update, delete, resultMap, cache, dynamic SQL) |
| `schemas/nuvatis-config.xsd` | Configuration XML 스키마 |

## Build & Test

```bash
dotnet build
dotnet test
```

Pack (수동):
```bash
dotnet pack --configuration Release --output ./nupkg
```

Pack (스크립트):
```bash
./pack.sh                       # Directory.Build.props 버전 사용
./pack.sh 1.0.1                 # 버전 지정
```

pack.sh는 빌드, 테스트, 패키징, 11개 패키지 검증을 자동 수행한다.

## CI/CD

GitHub Actions 기반 CI/CD 파이프라인:

| Workflow | Trigger | 역할 |
|----------|---------|------|
| `ci.yml` | push (main, develop), PR | 빌드, 테스트, 코드 커버리지, 패키지 생성 검증 |
| `publish.yml` | `v*` 태그 push | 빌드, 테스트, NuGet.org 배포, GitHub Release 생성 |
| `benchmark.yml` | push (main), PR | BenchmarkDotNet 성능 벤치마크 실행 및 회귀 감지 |
| `e2e-testcontainers.yml` | push (main), PR | Testcontainers 기반 PostgreSQL/MySQL 멀티버전 E2E 테스트 |
| `docs.yml` | push (main, docs/**) | DocFX 문서 빌드 및 GitHub Pages 배포 |

NuGet 배포는 Trusted Publishing (OIDC) 방식을 사용한다. API 키를 저장하지 않고, GitHub Actions가 발급하는 단기 OIDC 토큰으로 NuGet.org 임시 API 키를 획득하여 배포한다.

릴리스 방법:
```bash
git tag v2.0.0
git push origin v2.0.0
```

태그 push 시 publish.yml이 자동 실행되어 11개 패키지를 NuGet.org에 배포하고 GitHub Release를 자동 생성한다.

## Project Structure

```
NuVatis.sln
Directory.Build.props              # 공통 NuGet 메타데이터 + 버전
pack.sh                            # NuGet 패키징 스크립트
schemas/
  nuvatis-mapper.xsd                 # Mapper XML 스키마
  nuvatis-config.xsd                 # Config XML 스키마
src/
  NuVatis.Core/                      # 핵심 런타임
  NuVatis.Generators/                # Roslyn Source Generator
  NuVatis.PostgreSql/                # PostgreSQL Provider
  NuVatis.MySql/                     # MySQL/MariaDB Provider
  NuVatis.SqlServer/                 # SQL Server Provider
  NuVatis.Sqlite/                    # SQLite Provider
  NuVatis.Extensions.DependencyInjection/  # DI + Health Check
  NuVatis.Extensions.OpenTelemetry/  # OpenTelemetry 분산 추적
  NuVatis.Extensions.EntityFrameworkCore/  # EF Core 통합
  NuVatis.Extensions.Aspire/         # .NET Aspire 통합
  NuVatis.Testing/                   # 테스트 유틸리티
tests/
  NuVatis.Tests/                     # 단위/통합/E2E 테스트 (335개)
  NuVatis.Generators.Tests/          # Source Generator 테스트 (68개)
benchmarks/
  NuVatis.Benchmarks/                # 성능 벤치마크
samples/
  NuVatis.Sample/                    # 사용 예제
```

## Requirements

- .NET 7.0+ (.NET 7 / 8 / 9 / 10 멀티 타겟)
- C# 11+

## License

MIT License. Copyright (c) 2026 Jinho Choi.
