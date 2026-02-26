# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0] - 2026-02-27

### Breaking Changes

#### NV004: `${}` string substitution is now a **compile error**

이전 버전에서 NV004는 경고(Warning)였다. 2.0.0부터 **빌드 오류(Error)**로 승격된다.
`${}` 파라미터의 타입이 `string`이면 코드가 컴파일되지 않는다.

**영향 범위**: XML 매퍼에서 `${}` 를 사용하고, 해당 파라미터의 C# 타입이 `string`인 경우.

**마이그레이션 — 3가지 경로 중 선택:**

경로 1. `#{}` 파라미터 바인딩으로 교체 (권장)

`${}` 가 실제로는 파라미터 바인딩으로도 충분한 경우 `#{}` 로 교체한다.

```xml
<!-- 변경 전 -->
<select id="GetUser">
  SELECT * FROM users WHERE name = ${name}
</select>

<!-- 변경 후 -->
<select id="GetUser">
  SELECT * FROM users WHERE name = #{name}
</select>
```

경로 2. `SqlIdentifier` 타입으로 교체 (런타임 검증 포함, 권장)

동적 테이블명·컬럼명처럼 `${}` 가 불가피한 경우 파라미터 타입을 `string` 대신 `SqlIdentifier`로 변경한다.
`SqlIdentifier`는 생성 시점에 SQL Injection 패턴을 검사하여 런타임에서도 안전하다.

```csharp
using NuVatis.Core.Sql;

// 변경 전
public record SortParam(string SortColumn);

// 변경 후: SqlIdentifier.FromEnum (enum 기반, 가장 안전)
public enum SortColumn { CreatedAt, UserName, Id }
public record SortParam(SqlIdentifier SortColumn);

// 사용 예시
mapper.GetSorted(new SortParam(SqlIdentifier.FromEnum(SortColumn.CreatedAt)));

// 또는 SqlIdentifier.FromAllowed (화이트리스트 기반)
mapper.GetSorted(new SortParam(
    SqlIdentifier.FromAllowed(userInput, "id", "created_at", "user_name")));
```

경로 3. `[SqlConstant]` 어트리뷰트로 억제 (컴파일타임 상수 전용)

값이 런타임에 변하지 않는 진짜 상수인 경우에만 사용한다. 이 경우 NV004가 억제되지만 런타임 검증은 없다.

```csharp
public static class TableRef {
    [SqlConstant] public const string Users  = "users";
    [SqlConstant] public const string Orders = "orders";
}
```

주의: `[SqlConstant]`를 런타임에 변경될 수 있는 값에 적용하면 SQL Injection에 노출된다.
`[SqlConstant]` 는 리터럴 상수 또는 컴파일타임 확정 값에만 사용하라.

### Added

- `SqlIdentifier` 타입 (`NuVatis.Core.Sql` 네임스페이스)
  - `SqlIdentifier.From(string)`: SQL Injection 패턴 런타임 검증 후 생성
  - `SqlIdentifier.FromEnum<T>(T)`: enum 기반 안전한 생성 (Flags enum 조합 거부)
  - `SqlIdentifier.FromAllowed(string, params string[])`: 화이트리스트 기반 생성
- .NET 9.0 / 10.0 멀티 타겟팅 추가 (`net7.0;net8.0;net9.0;net10.0`)
- `NuVatis.Extensions.Aspire`: `net8.0;net9.0;net10.0` 지원 확대

### Changed

- `ColumnMapper` 내부 최적화: 타입별 컬럼 룩업 딕셔너리 캐시 도입 (O(n²) → O(1))
  기존 API는 변경 없음. 런타임 성능 개선만 적용.
- NV004 진단 심각도: `Warning` → `Error`
  `[SqlConstant]` 또는 `SqlIdentifier` 타입 사용 시 억제 가능.

---

## [1.0.0] - 2026-02-26

### Added

- PublicApiAnalyzers 도입: 모든 public API 변경을 컴파일 타임에 감지
- PublicAPI.Shipped.txt / PublicAPI.Unshipped.txt 전 프로젝트 배치
- API 호환성 정책 문서 (docs/api/public-api-reference.md)
- Dapper -> NuVatis 마이그레이션 가이드 (docs/cookbook/migration-from-dapper.md)
- EF Core -> NuVatis 마이그레이션 가이드 (docs/cookbook/migration-from-efcore.md)
- EF Core + NuVatis 하이브리드 패턴 가이드 (docs/cookbook/hybrid-efcore-nuvatis.md)

### Changed

- Version bump: 0.8.0-rc -> 1.0.0 (GA)
- API 동결: v1.0.0 이후 모든 public API 변경은 SemVer 정책 준수

---

## [0.8.0-rc] - 2026-02-26

### Added

- NuVatis.Sqlite 패키지: SQLite Provider (Microsoft.Data.Sqlite 기반)
- Object Pooling: StringBuilderCache, DbParameterListPool, InterceptorContextPool
- 벤치마크 CI 워크플로우 (.github/workflows/benchmark.yml)
- Write 벤치마크 시나리오 (Insert x100, BatchInsert x100)
- Testcontainers 기반 다중 DB 버전 E2E 테스트 (PostgreSQL 12-16, MySQL 5.7-8.4)
- E2E Testcontainers CI 워크플로우 (.github/workflows/e2e-testcontainers.yml)
- SourceLink + embedded PDB + snupkg 심볼 패키지
- Deterministic 빌드 지원
- NuVatis.Extensions.Aspire 패키지: .NET Aspire 통합 컴포넌트
  - 자동 Health Check 등록
  - OpenTelemetry 트레이싱 자동 구성
  - Aspire 설정 바인딩 (NuVatisAspireSettings)
- 테스트 커버리지: 라인 91.2%, 브랜치 81.4% 달성
  - Provider 단위 테스트 (4개 DB)
  - Attribute 단위 테스트
  - TypeHandler/TypeHandlerRegistry 테스트
  - MemoryCacheProvider LRU 캐시 테스트
  - LoggingInterceptor 테스트
  - DI 확장 통합 테스트
  - TestExpressionEvaluator 브랜치 커버리지 강화

### Changed

- InterceptorContext 프로퍼티: `required init` -> `required set` (오브젝트 풀링 지원)
- ParameterBinder: List 생성 대신 DbParameterListPool.Rent() 사용
- SqlSession: BuildSql에서 풀링된 List 반환 관리
- BenchmarkRunner -> BenchmarkSwitcher (전체 벤치마크 어셈블리 실행)
- Microsoft.Data.Sqlite 패키지 참조: 8.0.0 -> 8.* (호환성 개선)
- publish.yml: snupkg 파일 artifact에 포함

---

## [0.5.0-rc] - 2026-02-26

### Added

- DynamicSqlEmitter: 컴파일 타임 동적 SQL C# 코드 생성
  - if, choose/when, where, set, foreach, bind 태그 지원
  - 완전한 런타임 리플렉션 제거 (Source Generator 경로)
- MappingEmitter: 컴파일 타임 타입 안전 DbDataReader -> T 매핑
  - ResultMap 기반 매핑 코드 SG 생성
  - Nullable, 컬럼 인덱스 캐싱, ordinal 기반 접근
- ISqlSession.SelectOne/SelectList 커스텀 매퍼 오버로드 (Func<DbDataReader, T>)
- [SqlConstant] 어트리뷰트: SG가 SQL 안전 상수로 인식
- NV004 진단 강화: [SqlConstant] 필드 참조 시 경고 억제
- ITypeHandler 시스템: DateOnlyTypeHandler, TimeOnlyTypeHandler, EnumStringTypeHandler, JsonTypeHandler
- TypeHandlerRegistry: 타입/이름 기반 핸들러 등록/조회
- <bind> 태그 파서 및 SG 코드 생성 (로컬 변수 바인딩)
- ResultMapper: 런타임 ResultMap 기반 복합 매핑 (Association, Collection, Discriminator)

### Changed

- Source Generator 파이프라인: Incremental Generator 패턴으로 전면 리팩토링
- XmlMapperParser: <bind> 노드 파싱 추가
- ParameterEmitter: [SqlConstant] 인식 코드 생성

---

## [0.2.0-beta] - 2026-02-26

### Added

- BatchExecutor: DbBatch API 기반 배치 실행 (FlushStatements, FlushStatementsAsync)
- SqlSessionFactory.OpenBatchSession(): 배치 모드 세션 생성
- NV005 진단: 미사용 ResultMap 경고
- NV006 진단: ResultMap 프로퍼티 불일치 경고
- Codecov CI 연동: PR별 커버리지 리포트
- Dependabot 설정: NuGet/GitHub Actions 자동 업데이트
- DocFX 기반 문서 사이트 구조 (getting-started, cookbook, security, api)
- SqlSessionFactoryBuilder: Fluent API 빌더 패턴
- DbProviderRegistry: Provider 등록/조회 레지스트리

### Changed

- SimpleExecutor: IExecutor 인터페이스 분리 (단일 책임)
- SqlSession: IExecutor 주입 방식으로 리팩토링

---

## [0.1.0-alpha.1] - 2026-02-25

### Added

- Core runtime: ISqlSession, SqlSessionFactory, SimpleExecutor, ParameterBinder, ColumnMapper
- XML Mapper parser with dynamic SQL tags (if, choose/when/otherwise, where, set, foreach, sql/include)
- Roslyn Source Generator: compile-time proxy generation, mapper registry, attribute-based SQL
- ResultMap: explicit column-to-property mapping with association/collection support
- [NuVatisMapper] attribute for explicit opt-in Source Generator scanning
- NV004 compile-time warning for ${} string substitution (SQL injection risk)
- Diagnostic codes NV001-NV006 for compile-time validation
- PostgreSQL provider (Npgsql)
- MySQL provider (MySqlConnector)
- SQL Server provider (Microsoft.Data.SqlClient)
- Microsoft DI integration (AddNuVatis, Scoped ISqlSession)
- ASP.NET Core Health Check (AddNuVatis for IHealthChecksBuilder)
- OpenTelemetry distributed tracing (ActivitySource "NuVatis.SqlSession")
- Prometheus metrics via System.Diagnostics.Metrics (MetricsInterceptor)
- EF Core integration: DbConnection/DbTransaction sharing (AddNuVatisEntityFrameworkCore)
- IAsyncEnumerable streaming (SelectStream)
- Multi-ResultSet support (SelectMultiple, ResultSetGroup)
- Second-Level Cache: namespace-scoped LRU with auto-invalidation on writes
- Command timeout per statement
- External connection/transaction sharing (FromExistingConnection)
- Interceptor pipeline (Before/After with elapsed time, exception context)
- Lazy connection acquisition (first query triggers connection open)
- Thread safety guard (Interlocked-based concurrent access detection)
- autoCommit mode with automatic rollback on uncommitted dispose
- ExecuteInTransactionAsync helper
- InMemorySqlSession and QueryCapture for unit testing
- XML Schema files (nuvatis-mapper.xsd, nuvatis-config.xsd) for IDE auto-completion
- Custom DB provider support via IDbProvider
- .NET 7.0 / .NET 8.0 multi-targeting
- Native AOT compatibility (.NET 8)
- pack.sh packaging script (build, test, pack, verify 9 packages)
- DocFX documentation site structure with cookbook and security guides
- GitHub Actions CI matrix (2 OS x 2 .NET x 3 DB)
- GitHub Actions Trusted Publishing workflow (OIDC NuGet.org auto-deploy)

### Changed

- Renamed AutoMapper to ColumnMapper to avoid naming confusion with AutoMapper NuGet package
- Refactored SqlSession: extracted ExecuteTimed/ExecuteTimedAsync

### Fixed

- Source Generator scanning conflict with AutoMapper: [NuVatisMapper] attribute opt-in

### Security

- #{} parameter binding as default (SQL injection prevention)
- ${} string substitution detected at compile-time with NV004 warning
- Security documentation with whitelist validation guide

---

## Packages

| Package | Version |
|---------|---------|
| NuVatis.Core | 1.0.0 |
| NuVatis.Generators | 1.0.0 |
| NuVatis.PostgreSql | 1.0.0 |
| NuVatis.MySql | 1.0.0 |
| NuVatis.SqlServer | 1.0.0 |
| NuVatis.Sqlite | 1.0.0 |
| NuVatis.Extensions.DependencyInjection | 1.0.0 |
| NuVatis.Extensions.OpenTelemetry | 1.0.0 |
| NuVatis.Extensions.EntityFrameworkCore | 1.0.0 |
| NuVatis.Extensions.Aspire | 1.0.0 |
| NuVatis.Testing | 1.0.0 |
