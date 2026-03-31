# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.7.0] - Unreleased

### Added

- **`MappedStatement.RowMapper`**: `resultType` 쿼리용 SG 생성 행 매퍼 델리게이트 프로퍼티 추가. `SqlSession.SelectOne/SelectList` 및 Async/Stream 변형 5개에서 null이 아니면 리플렉션 없이 SG 매퍼를 직접 호출한다.
- **`NuVatisTypeMappers` 공유 정적 클래스 SG 생성**: Source Generator가 `resultType`-only 스테이트먼트의 행 매핑 메서드를 프록시 내부가 아닌 `NuVatisTypeMappers.g.cs` 공유 클래스에 생성한다. 프록시와 레지스트리 양쪽에서 참조 가능하다.

### Changed

- **`ProxyEmitter`**: `resultType`-only 스테이트먼트의 매핑 메서드를 인라인 생성에서 `global::NuVatis.NuVatisTypeMappers.Map_T_XXX` 공유 클래스 참조로 전환.
- **`RegistryEmitter`**: `resultType` 스테이트먼트 등록 시 `RowMapper = reader => global::NuVatis.NuVatisTypeMappers.Map_T_XXX(reader)` 람다를 emit하도록 확장.

### Fixed

- **`PropertyReflectionCache.Build()` IL2070**: `[RequiresUnreferencedCode]` 어노테이션 추가로 AOT 어노테이션 체인 완성.
- **`ResultMapper.ProcessCollection()` IL3050**: `#pragma warning disable` 목록에 IL3050 추가 (`MakeGenericType` 호출 정적 분석 경고 해소).

---

## [2.6.0] - Unreleased

### Added

- **.editorconfig**: .NET 표준 코딩 스타일 규칙 추가 (indent, var 사용, naming, 패턴 매칭 등)

### Changed

- **Central Package Management(CPM)**: `Directory.Packages.props` 도입으로 18개 프로젝트 패키지 버전 중앙 관리 전환. TF-조건부 패키지는 `VersionOverride`로 per-TF 버전 유지
- **Microsoft.NET.Test.Sdk**: 17.6.0 → 17.13.0 통일 (4개 테스트 프로젝트 전체)
- **Microsoft.CodeAnalysis.CSharp**: Generators.Tests 4.8.0 → 5.3.0 (Generators와 버전 일치)
- **ResultMapper**: bare `catch {}` → `catch (IndexOutOfRangeException)` 한정 및 의도 주석 추가
- **TestExpressionEvaluator**: 타입 변환 `catch` 블록 의도 주석 추가
- **`NuVatis.QueryBuilder` PublicAPI**: `PublicAPI.Unshipped.txt` 228개 엔트리를 `PublicAPI.Shipped.txt`로 이관. v2.4.0에서 추가된 QB API가 처음으로 공식 Shipped API로 등록됨.

### Fixed

- **Generators.Tests 빌드 오류**: NuVatis.Generators(CodeAnalysis 5.3.0)와 Generators.Tests(CodeAnalysis 4.8.0) 버전 불일치로 인한 CS1705 오류 수정
- **Microsoft.SourceLink.GitHub**: 버전 `10.0.0` → `10.0.102` (NU1603 × 19 해소)
- **`LazyValue<T>` CS1587**: XML 문서 주석을 `[UnconditionalSuppressMessage]` 속성 앞으로 이동
- **`ResultMapper` CS8604**: `ProcessCollection` 호출 시 non-null 보장 변수에 null-forgiving 연산자 추가

## [2.5.0] - 2026-03-12

### Added

- **Enum 프로퍼티 SG 캐스트 생성**: Source Generator가 `Enum` 타입 프로퍼티에 대해 `(EnumType)reader.GetInt32(ordinal)` 캐스트 코드를 빌드타임에 생성한다. 런타임 리플렉션 불필요.

### Changed

- **AOT/IL2026 정리**: `RequiresDynamicCode` 어트리뷰트를 `#if NET7_0_OR_GREATER` 조건부 컴파일로 분리. `CacheKey`에 `IEquatable<CacheKey>` 명시 구현으로 AOT 호환성 향상. `ColumnMapper`/`ResultMapper` `IL2026` suppress 정리.
- **CI**: `.NET 11 SDK`를 모든 워크플로우 매트릭스에 추가.

### Fixed

- **PublicAPI.Unshipped.txt**: RS0025 중복 엔트리 제거.

## [2.4.0] - 2026-03-09

### Added

- **NuVatis.QueryBuilder**: jOOQ 스타일 타입 안전 SQL DSL. PostgreSQL, MySQL, SQL Server, Oracle 4종 방언을 지원한다. `DslContext` + Fluent Step API로 Select/Insert/Update/Delete 쿼리를 빌드타임에 구성한다.
- **NuVatis.QueryBuilder.Tools**: `dotnet tool`로 DB 스키마를 스캔하여 테이블/컬럼 메타데이터 C# 클래스를 생성하는 코드 생성기.
- **NuVatis.Oracle**: Oracle 12c+ Provider (Oracle.ManagedDataAccess.Core 23.*). Double-quote 인용, colon 파라미터, OFFSET/FETCH 페이지네이션.
- **`<selectKey>` 지원**: XML 매퍼에서 `<selectKey>` 태그를 통해 Insert 후 자동 생성 키를 반환한다.
- **ProxyEmitter `BuildSql_XXX` 인라인 방출**: SG가 `ParsedStatement` 기반 SQL 빌드 메서드를 프록시 내부에 직접 생성한다. 레지스트리 조회 없이 SQL을 인라인으로 구성하여 `MappedStatement` 런타임 의존성을 제거한다.
- **`InMemorySqlSession` SQL-direct 메서드**: `SelectOneSql`, `SelectListSql`, `ExecuteSql` 및 Async 변형 6개 추가. SG 생성 프록시가 SQL-direct 경로를 사용할 때 `InMemorySqlSession`으로 테스트 가능하다.
- **PostgreSQL Testcontainers 통합 테스트**: Docker 기반 실제 DB로 엔드투엔드 검증.
- **QueryBuilder GroupBy/Having**: `SelectStep`에 `GroupBy()`, `Having()` 메서드 추가. `AggregateField<T>` 및 `Agg` 팩토리 (Count, Sum, Avg, Min, Max).
- **QueryBuilder BULK INSERT**: `InsertStep`에 `AddRow()` 메서드 추가. 다중 행 INSERT VALUES 지원.

### Changed

- **ParameterBinder PropertyCache 통합**: `PropertyCache` → `PropertyReflectionCache.GetProperty`로 통합. 중복 캐시 제거.
- **CS1591 XML 문서화**: 인터페이스, 구현체, 모델 전체에 XML 문서 주석 추가. `CS1591` NoWarn 제거.
- **CI**: benchmark/docs 워크플로에 .NET 11 SDK 추가.

### Fixed

- 로고 이미지 512x512 압축 (1.1MB -> 83KB, NuGet 1MB 한도 초과 해소)
- `TableNode.As()` 불변성 수정 -- 새 인스턴스를 반환하도록 변경
- XSD 스키마 파일 `netis-*.xsd` -> `nuvatis-*.xsd` 리네이밍
- 통합 테스트 상태 격리 -- 뮤테이션 테스트 클래스 분리

## [2.3.0] - 2026-03-06

### Added

- **동적 SQL 런타임 실행 — DynamicSqlBuilder**: `<foreach>`, `<if>`, `<where>`, `<set>`, `<choose>` 등 동적 태그가 포함된 XML Mapper statement에 대해 Source Generator가 `DynamicSqlBuilder` 람다를 빌드타임에 생성한다. 런타임 리플렉션 없이 동적 SQL이 평가된다.
  - `MappedStatement.DynamicSqlBuilder`: `Func<object?, (string Sql, List<DbParameter> Parameters)>?` 프로퍼티 추가
  - `ParameterBinder.CreateParameter(string name, object? value)`: SG 생성 람다에서 사용하는 `DbParameter` 팩토리 메서드 추가
  - `ParameterEmitter.EmitDynamicBuilderLambda(ParsedSqlNode rootNode)`: 동적 SQL 람다 코드 생성 진입점 추가
- **`RegistryEmitter.RegisterXmlStatements`**: SG가 `NuVatisMapperRegistry.RegisterXmlStatements(Dictionary<string, MappedStatement>)` 정적 메서드를 생성한다. XML 매퍼의 정적 statement는 `SqlSource` 경로로, 동적 statement는 `DynamicSqlBuilder` 람다 경로로 등록된다.
- **`<foreach>` 내 중첩 프로퍼티 접근**: `#{user.UserName}` 형태의 중첩 접속을 `<foreach>` 바디 내에서 정상 처리한다. SG가 `__getprop_` 로컬 함수를 생성하여 `BindingFlags.IgnoreCase` 기반 런타임 접근을 수행한다.
- **`<choose>/<when>/<otherwise>` 동적 람다 지원**: `ChooseNode`를 `EmitDynamicBuilderLambda` 내에서 if/else-if/else 체인으로 코드 생성한다.
- **`${}` 치환 동적 람다 가드**: 동적 SQL 내 `${}` 파라미터가 `SqlIdentifier` 타입인지 람다 내에서 런타임 검증한다. 타입 불일치 시 `InvalidOperationException` 즉시 발생.

### Changed

- **`SqlSession.BuildSql`**: `statement.DynamicSqlBuilder`가 설정된 경우 `ParameterBinder.Bind` 대신 람다를 우선 호출한다.
- **`RegistryEmitter.Emit` 시그니처**: `ImmutableArray<ParsedMapper> xmlMappers = default` 파라미터 추가 — XML 매퍼가 없는 프로젝트에서는 기존 동작과 동일하다.

### DI 마이그레이션 (XML 매퍼 사용 시)

XML 매퍼의 statement를 SG 레지스트리 경로로 등록하려면 `RegisterXmlStatements` 호출을 추가한다.

```csharp
builder.Services.AddNuVatis(options => {
    options.ConnectionString = builder.Configuration.GetConnectionString("Default");
    options.Provider         = new PostgreSqlProvider();
    options.RegisterMappers(NuVatisMapperRegistry.RegisterAll);
    options.RegisterAttributeStatements(stmts => {
        NuVatisMapperRegistry.RegisterAttributeStatements(stmts);
        NuVatisMapperRegistry.RegisterXmlStatements(stmts);   // 추가
    });
});
```

기존처럼 `SqlSessionFactoryBuilder.AddXmlMapper()` 런타임 파싱 경로를 사용하는 경우에는 변경 불필요.

### Tests

- `ParameterEmitterDynamicBuilderTests`: 동적 SQL 코드 생성 45개 단위 테스트 신규 추가
  - Lambda 보일러플레이트, TextNode, ParameterNode (`#{}` 단순/중첩/deep), StringSubstitution (`${}`) 가드
  - ForEachNode: 스칼라, open/close/sep, 중첩 프로퍼티, `${}` 내부, 첫 번째 플래그
  - IfNode, WhereNode, SetNode, ChooseNode 코드 생성 케이스
  - 복합 시나리오: where+if 조합, foreach+중첩 INSERT, set+update, 벌크 INSERT 스칼라
  - XML 파서 통합: 정적 statement, 동적 statement 판별 + 람다 생성
  - RegistryEmitter: SqlSource vs DynamicSqlBuilder 분기, StatementType 대문자화
- `GeneratorIntegrationTests`: SG 레지스트리 생성 검증 2개 테스트 추가
- 전체: `NuVatis.Tests` 349 Pass / `NuVatis.Generators.Tests` 134 Pass

---

## [2.2.0] - 2026-03-05

### Added

- **net6.0 / net11.0 멀티타겟 지원**: 모든 라이브러리 패키지가 `net6.0;net7.0;net8.0;net9.0;net10.0;net11.0`을 지원한다.
  - `NuVatis.Extensions.Aspire`는 Aspire 최소 요구사항에 따라 `net8.0+`를 유지한다.
  - net6.0 폴리필: `RequiredMemberAttribute`, `CompilerFeatureRequiredAttribute`, `ObjectDisposedException.ThrowIf` 조건부 컴파일 추가
  - CI 매트릭스 및 NuGet publish 워크플로우에 `6.0.x` / `11.0.x` 추가
- **SqlServer Testcontainers E2E 테스트**: `TestcontainersSqlServerE2ETests` 5개 — Insert/Count/Async/Update/Rollback/Delete 전 사이클 검증 (Docker 없는 환경 자동 Skip)
- **`SqlServerProvider.CreateConnection` 단위 테스트**: 실제 DB 연결 없이 `SqlConnection` 객체 생성 경로 커버

### Changed

- **내부 리팩토링**: `ColumnMapper`와 `TestExpressionEvaluator`의 중복 `PropertyCache` 필드를
  `NuVatis.Internal.PropertyReflectionCache` 공유 유틸리티로 통합 (public API 변경 없음)
  - `normalizeUnderscore: true` — ColumnMapper용, 언더스코어 제거 정규화 포함
  - `normalizeUnderscore: false` — TestExpressionEvaluator용, 익명 타입 지원 (`CanWrite` 필터 미적용)

### Tests

- `ParameterEmitter.EmitBuildSqlMethod` 코드 생성 경로 4개 단위 테스트 추가 (`ParameterEmitterStringSubstitutionTests`)
- `PropertyReflectionCache` 5개 단위 테스트 추가
- 전체: `NuVatis.Tests` 300 Pass / `NuVatis.Generators.Tests` 87 Pass

---

## [2.1.1] - 2026-03-04

### Fixed

- `ProxyEmitter`: 프로젝트 루트 네임스페이스에 "NuVatis"가 포함될 때 `resultMap` 타입이
  중복 네임스페이스를 갖는 버그 수정 (예: `NuVatis.Benchmark.NuVatis.Benchmark.Core.Models.User`)
  — `GetTypeByMetadataName(resultMap.Type)` 실패 시 XML 원본 문자열 대신
  인터페이스 메서드 Roslyn FQN으로 폴백하도록 `BuildResultMapTypeOverrides` 추가

---

## [2.1.0] - 2026-03-01

### Added

- `SqlIdentifier.JoinTyped<T>(IEnumerable<T>) where T : struct`
  — struct 제약으로 컴파일타임에 문자열 주입 차단, WHERE IN 절 안전 인라인 생성
  — `Guid`/`DateTime`/`DateTimeOffset`/`DateOnly`/`TimeOnly`는 따옴표 자동 추가, 숫자형은 그대로 출력
  — 빈 컬렉션 전달 시 `ArgumentException` 즉시 발생 (SQL 런타임 오류 사전 차단)
- `helpLinkUri` 추가: NV001~NV008 모든 진단 코드에 문서 링크 삽입, IDE 클릭 한 번으로 가이드 접근 가능
- README "When NOT to Use NuVatis" 섹션: EF Core/Dapper/NuVatis 선택 기준표
- docs/RELEASE-CHECKLIST.md: 릴리스 절차 5-섹션 체크리스트 (코드 검증, 패키지 품질, 보안, 버전/태그, 배포)
- docs/cookbook/hybrid-efcore-nuvatis.md: 쿼리 유형별 EF Core vs NuVatis 의사결정 테이블 + 트랜잭션 공유 예제
- benchmarks/NuVatis.Benchmarks: Dapper / Raw ADO / NuVatis Runtime 3종 6개 벤치마크 (BenchmarkDotNet)
- XML 문서 주석: `ISqlSession`, `ISqlSessionFactory`, `SqlIdentifier` `///` XML doc 변환 완료

### Changed

- ParameterEmitter: `${}` 코드 생성 시 `SqlIdentifier` FQN 정확 비교로 전환
  — 기존 `EndsWith("SqlIdentifier")`는 `MySqlIdentifier` 등 유사명 타입이 우회 가능 → `== "NuVatis.Core.Sql.SqlIdentifier"` 정확 비교로 교체
  — 타입 불일치 시 런타임에 `InvalidOperationException` 발생 (Fail-secure default)
- PublicAPI.Shipped.txt: v2.1.0 기준 전체 847개 API 항목 Unshipped → Shipped 이관
- `NuVatis.Core.csproj`: `GenerateDocumentationFile=true` 활성화 (XML doc 생성 시작)

### Fixed

- `SqlIdentifier.From()`: 정규식 `\b` 단어 경계가 점(`.`) 앞에서 오발동 → `(?<![.\w])...(?![.\w])` lookbehind/lookahead로 교체
  — `schema.or_table`과 같은 스키마 한정 식별자가 잘못 거부되던 문제 수정
- `PublicAPI.Unshipped.txt`: `JoinTyped` 시그니처 파라미터명 누락(`IEnumerable<T>!` → `IEnumerable<T>! values`)으로 RS0016 발생하던 문제 수정

---

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
| NuVatis.Core | 2.3.0 |
| NuVatis.Generators | 2.3.0 |
| NuVatis.PostgreSql | 2.3.0 |
| NuVatis.MySql | 2.3.0 |
| NuVatis.SqlServer | 2.3.0 |
| NuVatis.Sqlite | 2.3.0 |
| NuVatis.Extensions.DependencyInjection | 2.3.0 |
| NuVatis.Extensions.OpenTelemetry | 2.3.0 |
| NuVatis.Extensions.EntityFrameworkCore | 2.3.0 |
| NuVatis.Extensions.Aspire | 2.3.0 |
| NuVatis.Testing | 2.3.0 |
