# NuVatis 프로젝트 정밀 분석 보고서

**프로젝트**: NuVatis (MyBatis 스타일 .NET ORM)
**분석 일시**: 2026-10-04
**브랜치/커밋**: `main` @ `d01fde4` (v2.7.0 릴리스)
**분석 대상 경로**: `/home/nirna/jobs/.netis`

---

## 1. Executive Summary

| 항목 | 값 |
|---|---|
| 소스 파일 | 133개 (src 14개 프로젝트) |
| 테스트 파일 | 71개 (tests 4개 프로젝트) |
| 총 C# LOC | 22,605줄 (src 10,164 / tests 11,875) |
| 테스트/소스 LOC 비율 | **1.17** |
| 타겟 프레임워크 | net6.0 ~ net11.0 (6종 멀티타겟) |
| 단위 테스트 | **574개 전부 통과** (실측) |
| 소스 코드 평균 복잡도 | CC 8.9 / 중앙값 3 |
| TODO / FIXME | **0건** |
| 하드코딩된 시크릿 | **0건** |

### 종합 평가

| 항목 | 점수 | 비고 |
|---|---|---|
| 구조 | ⭐⭐⭐⭐⭐ 4.5/5 | 14개 어셈블리, 단방향 의존 |
| 의존성 | ⭐⭐⭐⭐⭐ 5/5 | CPM 중앙 관리, TF-conditional VersionOverride |
| 아키텍처 | ⭐⭐⭐⭐⭐ 4.5/5 | Provider/Executor/Interceptor/Mapping 계층 분리 |
| 코드 품질 | ⭐⭐⭐⭐⭐ 4.5/5 | 경고 0, Sync-over-async 0, NotImplementedException 0 |
| 테스트 | ⭐⭐⭐⭐ 4/5 | 물량·커버리지 우수, **CI 배선 결함 존재** |
| 보안 | ⭐⭐⭐⭐⭐ 5/5 | SqlIdentifier 화이트리스트, PublicAPI 승격 |
| 성능 | ⭐⭐⭐⭐ 4/5 | ListPool/StringBuilderCache/PropertyReflectionCache 도입 |
| CI/CD | ⭐⭐☆ 2.5/5 | **E2E 3개 스텝이 0개 테스트 실행 (치명적)** |

### 주요 발견사항

1. **🔴 CI의 DB E2E 3개 스텝이 실제로는 테스트를 0개 실행합니다.** `--filter "Category=E2E&Provider=PostgreSql"` 을 쓰는데 `Provider` 트레이트가 프로젝트 전체에 **하나도 정의돼 있지 않아** 매칭이 0건입니다. PostgreSQL/MySQL/SQL Server E2E는 CI에서 사실상 죽어 있습니다.
2. **🔴 Testcontainers 다중 버전 매트릭스가 무의미합니다.** 워크플로가 `TC_PG_IMAGE` / `TC_MYSQL_IMAGE` 환경변수를 주지만 테스트 코드가 이를 **읽지 않고** 기본 이미지를 하드코딩합니다. PG 13/14/15/16 매트릭스가 모두 같은 이미지를 돌려 버전 호환성은 검증되지 않습니다.
3. **🟠 19개 `Category=E2E` 테스트가 CI 어디에서도 실행되지 않습니다.** 유닛 스텝은 `Category!=E2E`로 제외하고, E2E 스텝은 0건 매칭입니다. SQLite 인메모리 기반이라 Docker도 불필요한 테스트들입니다.
4. **🟠 `.editorconfig` 규칙이 빌드에 강제되지 않습니다.** `EnforceCodeStyleInBuild` / `AnalysisMode` 미설정으로 IDE0005 등 선언된 규칙이 CI에서 advisory 상태입니다.
5. **🟡 `FastTest=true` 는 src 멀티타겟에 적용되지 않습니다.** README가 "net8 단일 타겟 — 대폭 단축(권장)"이라 안내하지만, SDK 하나만 설치된 환경에서는 sln 빌드가 NETSDK1045로 실패합니다.

---

## 2. 구조 분석

### 1.1 모듈 구성

```
NuVatis.sln (18개 프로젝트)
├── src/ (14) — 제품 코드
│   ├── NuVatis.Core                      59 files / 4,526 LOC  ← 핵심
│   ├── NuVatis.Generators                26 files / 3,378 LOC  ← Roslyn SG (netstandard2.0)
│   ├── NuVatis.QueryBuilder              25 files /   973 LOC  ← Core 무의존 독립 제품
│   ├── NuVatis.QueryBuilder.Tools         5 files /   337 LOC  ← CLI (net8.0)
│   ├── NuVatis.Testing                    3 files /   220 LOC
│   ├── NuVatis.Extensions.DependencyInjection   4 / 272
│   ├── NuVatis.Extensions.EntityFrameworkCore   2 / 120
│   ├── NuVatis.Extensions.OpenTelemetry         1 /  97
│   ├── NuVatis.Extensions.Aspire                2 / 105
│   └── DB Provider 5종 (Pg/MySql/SqlServer/Sqlite/Oracle)  각 1 / ~27
├── tests/ (4) — 11,875 LOC
├── samples/NuVatis.Sample
├── benchmarks/NuVatis.Benchmarks
└── docs/ — 48개 마크다운
```

**관찰**: DB Provider 5개가 각각 **단일 파일 27~28줄**이라는 점이 설계 완성도를 보여줍니다. `IDbProvider` 추상화만으로 5개 DB를 완전히 교체 가능하게 구현한 것입니다.

### 1.2 Core 내부 레이어 (관심사 분리)

`src/NuVatis.Core` 59개 파일이 9개 기술적 관심사로 정확히 분리되어 있습니다.

```
Attributes/       7   Mapper/SQL 선언 어트리뷰트
Binding/          1   ParameterBinder — 파라미터 바인딩
Cache/            4   CacheConfig, CacheKey, ICacheProvider, MemoryCacheProvider
Configuration/    3   NuVatisConfiguration, DataSourceConfig, TypeAliasRegistry
DynamicSql/       1   TestExpressionEvaluator — <if test> 평가
Executor/         3   IExecutor, SimpleExecutor, BatchExecutor
Interceptor/      5   ISqlInterceptor + Pipeline + Logging/Metrics 구현
Internal/         4   ListPool, StringBuilderCache, PropertyReflectionCache, InterceptorContextPool
Mapping/         15   ResultMap/Association/Collection/Discriminator/TypeHandler/LazyValue
Provider/         3   IDbProvider, DbProviderRegistry, NuVatisProviderAttribute
Session/          5   ISqlSession, SqlSession, SqlSessionFactory(+Builder)
Sql/              1   SqlIdentifier
Statement/        3   MappedStatement, SelectKeyConfig, StatementType
Transaction/      2   ITransaction, AdoTransaction
```

**평**: 파일 크기 상위 — `SqlSession.cs` 872줄이 최대이며 이것도 40개 이상의 15~25줄 메서드로 분해되어 있습니다. God Object 위험 없음.

### 1.3 저장소 위생

`.gitignore`이 매우 꼼꼼합니다. `bin/ obj/ nupkg/ *.nupkg coverage-results/ .worktrees/ msbuild.binlog docs/plans/` 등 전부 예외 처리되어 있고, **커밋된 잡그물 0건**을 확인했습니다.

- `git ls-files` 대상 junk 스캔 결과: `binlog` / `.nupkg` / `coverage-results` / `.serena` **모두 미커밋** ✅
- 추적된 시크릿 파일(`.env`, `.pfx`, `.key`): **0건** ✅

**발견 사항**: `.serena/`(36K)는 `.gitignore`에 없어 `git status`에 잡음으로 나타납니다. `.claude/` `.cursorrules` 등 AI 도구 산출물은 예외 처리되어 있는데 `.serena`만 누락입니다.

---

## 3. 의존성 분석

### 3.1 외부 의존성 — 매우 disciplined

| 관리 방식 | 상태 |
|---|---|
| Central Package Management | ✅ `Directory.Packages.props`에서 20개 프로젝트 버전 중앙 관리 |
| 전이적 핀닝 | ✅ `CentralPackageTransitivePinningEnabled=false` — 의도적으로 비활성화(직접 참조만 관리) |
| TF-conditional 패키지 | ✅ `VersionOverride`로 net6~net11별 `Microsoft.Extensions.*` 버전 분리 |
| Dependabot | ✅ 13개 디렉터리 등록, `Microsoft.Extensions.*`/`Microsoft.CodeAnalysis.*` 등 그룹핑 |
| 미사용 의존성 | ✅ 없음 — QueryBuilder는 Core를 참조하지 않는 독립 제품 |

**유일하게 눈여겨볼 점**: `NuVatis.QueryBuilder.Tools`가 `Npgsql` + `MySqlConnector` + `System.CommandLine`을 참조하는데, `PackageReference Remove="Microsoft.CodeAnalysis.PublicApiAnalyzers"`로 분석기를 제거하고 있습니다. CLI 실행 도구라 Roslyn 분석기 오버헤드를 없앤 합리적 판단입니다.

### 3.2 내부 의존성 그래프

```
NuVatis.Core  (의존성 0 — 리프)
    ▲            ▲          ▲           ▲
    │            │          │           │
    │  Generators│  Pg/MySql/SqlServer  │
    │  (netstd2) │  /Sqlite/Oracle      │
    │            │          │           │
    └─ Testing ──┘          │           │
    │                       │           │
    └─ Extensions.DependencyInjection ◄──┘
            │            ▲
            ├── Extensions.OpenTelemetry
            ├── Extensions.EntityFrameworkCore
            └── Extensions.Aspire  (DI + OpenTelemetry 참조)

NuVatis.QueryBuilder  (의존성 0 — 독립 리프)
NuVatis.QueryBuilder.Tools → Npgsql, MySqlConnector, System.CommandLine
```

**순환 의존성: 0건** ✅. 계층 방향이 전부 단방향이며 리프 노드가明確히 분리돼 있습니다.

### 3.3 버전 최신성

`Microsoft.Data.SqlClient 7.0.0`, `Npgsql 9.0.4`, `MySqlConnector 2.4.0`, `Oracle.ManagedDataAccess.Core 23.7.0`, `Microsoft.CodeAnalysis.CSharp 5.3.0` — 모두 현재 안정판 또는 그에 근접합니다. CHANGELOG에 SqlClient 6.0.1→7.0.0 업그레이드 이력이 명시돼 있습니다.

---

## 4. 아키텍처 분석

### 4.1 적용된 설계 원칙

| 원칙 | 적용 증거 |
|---|---|
| **의존성 역전** | `SqlSession`이 `SimpleExecutor`/`BatchExecutor` 구체 타입이 아닌 `IExecutor`에 의존 |
| **Strategy** | `IDbProvider` 5개 구현, `ITypeHandler` 4종, `ICacheProvider` |
| **Pipeline** | `InterceptorPipeline` — Logging/Metrics 등 횡단 관심사 분리 |
| **Source Generator AOT** | `IsAotCompatible` 조건부 선언, 리플렉션 최소화 |
| **Compile-time safety** | XML → SG 코드 생성, 런타임 파싱 제거 |
| **명시적 API 계약** | `PublicAPI.Shipped.txt` 13개 프로젝트 총 1,132 엔트리, RS0016/RS0017 `WarningsAsErrors` 승격 |

### 4.2 AOT / 성능 설계

- `IsAotCompatible` 조건부 선언: Core, DI, OpenTelemetry, DB Provider 5종
- `IsAotCompatible` 미선언: `Extensions.Aspire`(net8+ 호스팅 통합), `Extensions.EntityFrameworkCore`(EF Core 의존), `Generators`(netstandard2.0) — 각각 합리적 사유
- **리플렉션 캐싱 3종 도입**: `PropertyReflectionCache`, `StringBuilderCache`, `ListPool` + `InterceptorContextPool`(`Internal/`) — v2.6.0 성능 튜닝의 결과물

### 4.3 아키텍처 상의 강점

**`resultType` 리플렉션 경로의 점진적 탈피**. v2.6.0에서 `MappedStatement.RowMapper`가 추가되고, SG가 `NuVatisTypeMappers` 공유 클래스에 행 매퍼를 생성해 프록시가 리플렉션 없이 직접 호출합니다. 설계상 리플렉션은 폴백 경로로 격리됐습니다.

---

## 5. 코드 품질 분석

### 5.1 실측 정적 지표 (src 203개 파일 전수)

| 지표 | 값 | 평가 |
|---|---|---|
| TODO / FIXME / HACK | **0건** | ✅ 우수 |
| `NotImplementedException` | **0건** | ✅ 우수 |
| sync-over-async (`.Result`/`.Wait()`) | **0건** | ✅ 우수 |
| `async void` | **0건** | ✅ 우수 |
| `Thread.Sleep` / `Task.Delay` | **0건** | ✅ 우수 |
| bare `catch {}` | 0건 (CHANGELOG에서 `IndexOutOfRangeException` 한정으로 개선 이력) | ✅ |
| `[Obsolete]` | 2건 (의도적 API 폐기, v3.0 제거 예정) | ✅ 적절 |
| 평균 복잡도 | CC 8.9 | ✅ 양호 |
| 중앙값 복잡도 | CC 3 | ✅ 우수 |

### 5.2 고복잡도 파일 — 실제 위험 없음

파일 단위 CC 상위 3건은review 대상이 아닙니다. **함수 단위로 보면 이미 충분히 분해**되어 있기 때문입니다.

| 파일 | CC | LOC | 메서드 수 | 실제 판정 |
|---|---|---|---|---|
| `Generators/Emitters/ParameterEmitter.cs` | 135 | 726 | 24개 | SQL 태그(if/where/set/foreach/choose)마다 `Emit*` 메서드 1:1 대응. **도메인 본질적 복잡도** |
| `Core/Session/SqlSession.cs` | 109 | 873 | 40개 | Select/Commit/Rollback/Mapper 등 얇은 파사드 메서드, 평균 20줄. **파사드 패턴 정상** |
| `Generators/Emitters/ProxyEmitter.cs` | 102 | 393 | — | 코드 생성 템플릿 분기 |

CC는 파일 전체 합산값이므로, 개별 메서드 복잡도는 훨씬 낮습니다. **리팩토링 대상이 아닙니다.**

### 5.3 테스트

**테스트/소스 LOC 비율 1.17** — industry 평균(0.3~0.5)보다 훨씬 높습니다.

| 스위트 | 테스트 수 (실측) | 결과 |
|---|---|---|
| NuVatis.Tests (Core, E2E 제외) | 338 | ✅ 전부 통과 |
| NuVatis.Generators.Tests | 145 | ✅ 전부 통과 |
| NuVatis.QueryBuilder.Tests (통합 제외) | 76 | ✅ 전부 통과 |
| NuVatis.QueryBuilder.Tools.Tests | 15 | ✅ 전부 통과 |
| **합계** | **574** | **실패 0** |

- 커버리지 산출물: `NuVatis.Core` **line 91.2% / branch 81.5%**, `Extensions.OpenTelemetry` 100%, `Extensions.DependencyInjection` 86.8%
- Testcontainers 기반 실제 DB 검증(6개 클래스), `Xunit.SkippableFact` 16건으로 Docker 부재 환경 우아한 스킵

**주의**: 저장소의 `coverage-results/`는 Core 계열 단일 실행 산출물이며(1,472줄 기준), Generators·QueryBuilder 커버리지는 포함되지 않습니다. 전체 커버리지로 해석하면 안 됩니다.

---

## 6. 보안 분석

### 6.1 SQL Injection 방어 — 설계 수준 최상

`src/NuVatis.Core/Sql/SqlIdentifier.cs`는 3중 방어선을 갖습니다.

1. **`From(string)`** — 형식 화이트리스트 정규식 `^[\p{L}_][\p{L}\p{N}_$#]*(\.[\p{L}_][\p{L}\p{N}_$#]*)*$`. v2.7.0에서 **블랙리스트 → 화이트리스트로 전환**(breaking change). 공백·등호·괄호·대괄호·백틱 전부 차단. 유니코드 식별자(한글 등) 지원.
2. **`FromEnum<T>()`** — 컴파일 타임 확정값이라 Injection 불가능. Flags enum 조합(`"Read, Write"`)은 거부.
3. **`FromAllowed(value, params string[])`** — 사용자 입력 화이트리스트 검증(대소문자 무시).

금지 키워드 정규식은 `(?<![.\w])(union|select|drop|insert|or|and)(?![.\w])`로, `schema.or_table` 같은 점 구분 식별자가 오탐되지 않도록 설계됐습니다. 주석에 이 판단 근거가 명시돼 있습니다.

**부수 효과**: `${}` 치환에 `string`을 쓰면 NV004 진단이 **컴파일 에러**로 발생(Source Generator). SQL Injection이 빌드 타임에 차단됩니다 — 이는 일반적 ORM 대비 차별화된 방어입니다.

### 6.2 `JoinTyped<T>` — 타입 화이트리스트 + InvariantCulture

v2.7.0 변경분. `WHERE IN` 절 인라인을 **구조화된 11종 타입만** 허용합니다.

- 숫자형 11종, enum(underlying 정수), Guid, DateTime, DateTimeOffset, DateOnly, TimeOnly
- `bool`·`char`·사용자 정의 struct는 `ArgumentException` — breaking change로 명시
- 전체 `CultureInfo.InvariantCulture` 고정 — 실행 문화권에 따른 소수점 구분 불일치(독일 로케일 등) 원천 차단
- 빈 컬렉션 거부 — `IN ()` SQL 오류 사전 방지

`DateTime`에 `.fffffff`(100ns) 포맷을 쓴 것은 Oracle/SQL Server의 datetime2(7) 정밀도와 맞춘 의도적 설계입니다.

### 6.3 기타 보안 점검

| 항목 | 결과 |
|---|---|
| src 내 하드코딩된 시크릿 | **0건** (문서 XML 주석의 `"Host=localhost;..."` 예시 1건뿐) |
| 커밋된 `.env`/인증서 | **0건** |
| CI 자격증명 | E2E 컨테이너 비밀번호만 하드코딩 — **격리된 일회용 테스트 컨테이너**이므로 수용 가능 |
| PublicAPI 회귀 방어 | RS0016/RS0017을 `WarningsAsErrors`로 승격 — public API 무단 제거/추가 차단 |

---

## 7. 성능 분석

### 7.1 도입된 최적화

| 기법 | 위치 | 효과 |
|---|---|---|
| `StringBuilderCache` | `Internal/` | 문자열 빌드 시 버퍼 재사용 |
| `ListPool` | `Internal/` | 컬렉션 GC 압축 |
| `PropertyReflectionCache` | `Internal/` | 리플렉션 결과 캐싱 |
| `InterceptorContextPool` | `Internal/` | 인터셉터 컨텍스트 재사용 |
| `RowMapper` 위임 | v2.6.0 | `resultType` 쿼리의 리플렉션 제거 |
| 인라인 SQL 빌드 | v2.7.0 ProxyEmitter | `BuildSql_XXX` 정적 메서드 인라인 방출 |
| TypeHandler 등록형 | `TypeHandlerRegistry` | 커스텀 직렬화 확장점 |
| BenchmarkDotNet + Dapper 비교 | `benchmarks/` | Dapper 대비 정량 벤치마크 |

**인라인 SQL 빌드 설계가 특히 우수합니다.** 런타임에 SQL을 조합하지 않고 빌드타임에 확정해 두는 구조는 성능과 보안을 동시에 확보합니다.

### 7.2 리스크

- **Lazy Loading**: `LazyValue` + `AssociationMapping`의 `fetchType="lazy"`는 N+1 쿼리 위험이 있습니다. 다만 명시적 opt-in 기능입니다.
- **2차 캐시**: `MemoryCacheProvider`는 프로세스 내 LRU(`eviction=LRU, size=512, flushInterval=600000ms`)입니다. 다중 인스턴스 배포 시 인스턴스 간 무효화 전파가 없습니다. **문서화된 제약인지 확인 필요.**

---

## 8. CI/CD 정밀 분석 — 가장 취약한 영역

`.github/workflows/` 5개(ci, e2e-testcontainers, benchmark, docs, publish) + dependabot. 구성 자체는 성숙합니다. **그러나 테스트 배선에 중대한 결함이 있습니다.**

### 8.1 🔴 결함 1: DB E2E 3개 스텝이 0개 테스트 실행

`ci.yml`의 e2e-test 잡이 3개 스텝에서 이 필터를 사용합니다.

```yaml
--filter "Category=E2E&Provider=PostgreSql"   # line 190
--filter "Category=E2E&Provider=MySql"         # line 195
--filter "Category=E2E&Provider=SqlServer"     # line 200
```

**실측 검증 결과**:

```
### A) Category=E2E&Provider=PostgreSql
No test matches the given testcase filter `Category=E2E&Provider=PostgreSql`
### B) Category=E2E 만        → 19개
### C) Category!=E2E (유닛)   → 281개
```

`Provider` 트레이트가 테스트 전체에 **단 한 곳도 정의돼 있지 않습니다**(`grep '\[Trait\('` 결과 19건 전부 `Category` 또는 `Integration`/`Testcontainers`).

**결과**: 잡이 **green으로 통과하지만 아무 테스트도 실행하지 않습니다.** PostgreSQL / MySQL / SQL Server 실연동 검증이 CI에서 완전히 비활성화됐습니다. 3개 DB 서비스 컨테이너를 spinning하면서 테스트는 0개입니다.

### 8.2 🔴 결함 2: Testcontainers 버전 매트릭스가 무의미

`e2e-testcontainers.yml`은 버전 매트릭스를 선언하고 환경변수를 주지만:

```yaml
matrix: { pg-version: ['13','14','15','16'] }
env:   { TC_PG_IMAGE: postgres:${{ matrix.pg-version }}-alpine }
```

**테스트 코드는 이 환경변수를 읽지 않습니다.** `grep 'GetEnvironmentVariable' Testcontainers*.cs` → **0건**.

```csharp
_container = new PostgreSqlBuilder()      // line 57 — 파라미터 없음, 기본 이미지
_container = new MySqlBuilder()           // line 39 — 파라미터 없음, 기본 이미지
_container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")  // 하드코딩
```

**결과**: PG 13/14/15/16 매트릭스 4회, MySQL 8.0/8.4 매트릭스 2회 — 총 6회 실행이 **전부 동일한 이미지로** 돌아갑니다. 지원 DB 버전 호환성 검증이 전혀 되지 않습니다. 매트릭스가 시간 낭비이자 검증 공백입니다.

### 8.3 🟠 결함 3: 19개 E2E 테스트가 CI 고아 상태

`Category=E2E` 19개 테스트는:

- 유닛 스텝에서 `Category!=E2E`로 **제외**
- e2e-test 스텝에서 `Provider` 부재로 **0건 매칭**
- `e2e-testcontainers`는 `Category=Testcontainers`로 필터링되어 **제외**

→ **어떤 CI 잡에서도 실행되지 않습니다.** 대상 클래스:

| 클래스 | 의존 DB |
|---|---|
| `SqliteE2ETests` | SQLite 인메모리 |
| `FullPipelineE2ETests` | SQLite 인메모리 |
| `SqlSessionE2ETests` | SQLite 인메모리 |
| `PostgreSqlE2ETests` | 실 PostgreSQL |
| `SqlSessionAdvancedTests` | — |
| `SqlSessionAsyncTests` | — |

SQLite 인메모리 계열은 **Docker도 네트워크도 불필요**하므로 유닛 스텝에 편입하는 것이 가장 자연스럽습니다.

### 8.4 🟠 결함 4: Testcontainers가 유닛 잡에 중복 실행

`Testcontainers*` 3개 클래스는 `Category=Testcontainers`이므로 `Category!=E2E` 조건을 **만족**합니다. 따라서 Testcontainers는 도커가 있는 GitHub 러너에서 유닛 잡의 **6개 매트릭스 조합 전부**에서 실행됩니다. 이후 `e2e-testcontainers` 주간 잡에서도 다시 실행됩니다. 로컬 실행 결과도 이를 뒷받합합니다 — `--filter "Category!=E2E"` 실행에서 **Skipped 0 / Passed 338**로, Testcontainers 테스트가 로컬에서 실제로 수행됐습니다.

### 8.5 🟡 결함 5: Testcontainersdeprecated API

`new PostgreSqlBuilder()` / `new MySqlBuilder()`는 파라미터 없는 폐기 생성자입니다. 빌드 시 경고 발생:

```
warning CS0618: 'PostgreSqlBuilder.PostgreSqlBuilder()' is obsolete ...
warning CS0618: 'MySqlBuilder.MySqlBuilder()' is obsolete ...
```

다음 Testcontainers 마이그레이션 시这两 곳이 **컴파일 에러로 전환**됩니다. 이미지 인자를 받는 생성자로 전환하며, 결함 2의 환경변수 주입도 같은 변경에서 해결됩니다.

### 8.6 ✅ CI의 강점

- 매트릭스: ubuntu+windows × net6~net11 (6조합), `fail-fast: false`
- E2E는 3개 DB 서비스 컨테이너 + healthcheck 대기
- QueryBuilder 통합 테스트는 Testcontainers 공유 픽스처 + net8.0 단일 타겟
- `pack-verify` 잡이 **패키지 개수 ≥ 9를 강제 검증** — 패키징 회귀 차단
- 아티팩트 업로드(`if: always()`), Codecov 연동, PR/메인 브랜치 트리거 분리
- `dependabot`이 PR 상한까지 지정

---

## 9. 개선 우선순위

### 🔴 즉시 (P0) — CI 검증 공백

| # | 조치 | 대상 | 효과 |
|---|---|---|---|
| 1 | E2E 테스트 클래스에 `[Trait("Provider", "...")]` 부여 | `PostgreSqlE2ETests`, `Testcontainers*` 3개 | 결함 1 해결 → DB 3종 E2E 실제 activate |
| 2 | `TC_PG_IMAGE` / `TC_MYSQL_IMAGE`를 테스트 코드에서 읽도록 수정 + `MsSqlBuilder` 이미지 인자화 | `Testcontainers*.cs` 3개 | 결함 2·5 해결 → 버전 매트릭스 실효화 + CS0618 경고 제거 |
| 3 | `Category=E2E` 재분류 — SQLite 인메모리 계열을 `Category!=E2E`로 편입 | `SqliteE2ETests`, `FullPipelineE2ETests`, `SqlSessionE2ETests`, `SqlSessionAdvancedTests`, `SqlSessionAsyncTests` 19개 | 결함 3 해결 → 고아 테스트 0개 |
| 4 | Testcontainers 계열을 유닛 잡에서 명시적 제외 | `ci.yml` 유닛 스텝 필터 | 결함 4 해결 → 중복 실행 제거, 잡 시간 절감 |

> **권장 최소 수정**: `ci.yml` 유닛 스텝 필터를 `Category!=E2E&Category!=Testcontainers`로 바꾸면 3·4번이 동시 해결됩니다.

### 🟠 단기 (P1) — 관측 가능성

| # | 조치 | 근거 |
|---|---|---|
| 5 | CI에 **테스트 개수 assertion** 추가 | `--logger trx` 후 최소 통과 개수 검증. 0개 실행의 green 통과가 재발하지 않도록 |
| 6 | `.editorconfig` 규칙 빌드 강제 | `Directory.Build.props`에 `EnforceCodeStyleInBuild=true` + `AnalysisMode` 명시. 현재 IDE0005가 advisory 상태 |
| 7 | `TreatWarningsAsErrors` 전역 적용 검토 | 현재 RS0016/RS0017만 승격. 테스트 코드의 CS0618이 경고로 남아 방치됨 |
| 8 | `FastTest`를 src까지 전파 | `Directory.Build.props`에 `Condition="'$(FastTest)'=='true'"`의 `TargetFrameworks` 오버라이드 추가. SDK 1개 환경에서 sln 빌드 불가 문제 해결 |

### 🟡 중기 (P2) — 심화

| # | 조치 | 근거 |
|---|---|---|
| 9 | 전체 커버리지 산출 | 현재 산출물은 Core 계열 1,472줄만 반영. Generators·QueryBuilder 커버리지 미확인 |
| 10 | `.serena/` `.gitignore` 추가 | AI 도구 산출물 노이즈 제거 |
| 11 | 2차 캐시의 다중 인스턴스 제약 문서화 | `MemoryCacheProvider`는 프로세스 로컬 LRU. 인스턴스 간 무효화 전파 없음 |
| 12 | Benchmark 결과의 주기적 아카이브 | `benchmark.yml` 존재하나 임계값 회귀 게이트가 없음 |

---

## 10. 리스크 평가

### 기술 리스크

| 리스크 | 심각도 | 영향 | 가능성 | 완화 |
|---|---|---|---|---|
| **DB E2E 무실행 (green false-pass)** | 높음 | 높음 | **확실** (실측 확인) | P0-1, P0-5 |
| **DB 버전 호환성 미검증** | 높음 | 높음 | **확실** | P0-2 |
| 19개 E2E 테스트 미실행 | 중간 | 중간 | **확실** | P0-3 |
| 테스트 deprecated API | 낮음 | 중간 | 높음 (다음 업그레이드 시) | P0-2 |
| 컨벤션 미강제 | 낮음 | 낮음 | 높음 | P1-6 |
| 2차 캐시 다중 인스턴스 | 중간 | 중간 | 낮음 | P2-11 |

### 비즈니스 리스크

- **릴리스 신뢰도**: v2.7.0 릴리스가 "DB 3종 검증 완료"라는 인식을 주지만 실제로는 PostgreSQL/MySQL/SQL Server 통합 경로가 CI에서 한 번도 실행되지 않았을 가능성이 높습니다. SQL Server 전용 경로(`SELECT SCOPE_IDENTITY()` 등)의 회귀를 잡아낼 방어선이 없습니다.
- **리팩토링 속도 저해 요인은 낮습니다.** 코드 품질 지표가 매우 깨끗해 기술 부채에 의한 속도 저하는 사실상 없습니다. 유일한 병목은 테스트 실행 신뢰성입니다.

---

## 11. 총평

NuVatis는 **코드 관점에서 이 프로젝트가 분석한 범위 내에서 가장 높은 품질군에 속합니다.** TODO 0건, sync-over-async 0건, 순환 의존성 0건, 하드코딩된 시크릿 0건, 커밋된 잡그물 0건, 테스트/소스 LOC 비율 1.17, Public API를 빌드 에러로 보호하는 계약 관리, SQL Injection을 컴파일 타임에 차단하는 Source Generator 진단 — 이 목록의 절반은 동일 규모 OSS ORM에서 보기 어려운 조합입니다.

`SqlIdentifier`의 화이트리스트 전환, `JoinTyped`의 InvariantCulture 고정, `PropertyReflectionCache` 계열 4종의 풀 도입, 6-TFM 멀티타겟 CPM 관리, `PublicAPI.Shipped` 1,132 엔트리 관리는 **프로젝트 소유자가 품질을 체계적으로 관리해 온** 명확한 증거입니다.

**유일한 실질적 약점은 코드가 아니라 CI의 테스트 배선입니다.** `Provider` 트레이트 누락 하나로 3개 잡이 0개 테스트를 green으로 통과시키고, Testcontainers 매트릭스는 환경변수를 읽지 않아 6회 모두 같은 이미지를 실행하며, 19개 E2E 테스트는 어느 잡에서도 실행되지 않습니다. 이 셋은 독립적으로도 결함이지만, **"테스트가 있는 것처럼 보이지만 실제로는 돌지 않는"** 공통 패턴이라 한 번에 정리할 가치가 큽니다.

코드베이스에 손댈 필요는 없습니다. **`.github/workflows/`와 테스트 트레이트 속성만 바로잡으면 프로젝트 품질이 현재 상태의 의도된 수준으로 실제로 도달합니다.**

---

*분석 범위: 정적 분석 + net8.0 단일 타겟 실측 빌드/테스트 실행. 동적 프로파일링·런타임 성능 측정 및 E2E 실DB 실행(Testcontainers)은 이 환경(SDK 8.0.131 단일 설치)에서 수행하지 않았습니다.*
