# NuVatis 개선 플랜 — 실측 기반

**작성일**: 2026-10-04
**기준 커밋**: `main` @ `d01fde4` (v2.7.0)
**측정 환경**: .NET SDK 8.0.131 단일 설치, Docker 사용 가능, Linux

---

## 0. 먼저 — 이전 보고서의 정정

이전 분석은 `dotnet test --list-tests` 출력에 기반했습니다. 이 어댑터 조합에서 **`--list-tests`는 신뢰할 수 없다는 것을 이번 실측에서 발견**했고, 실행 결과(`Passed! - Total:`)로 전부 재측정했습니다. **수치가 바뀐 항목이 있습니다.**

| 항목 | 이전 보고서 | 실측 정정 | 근거 |
|---|---|---|---|
| `Category=E2E` 테스트 수 | 19개 | **49개** | `dotnet test --filter "Category=E2E"` → Total 49 |
| 고아 테스트 | 19개 | **49개** (기준선 유지) | 위와 동일 |
| Testcontainers 중복 실행 | 유닛 잡에서 6회 중복 | **정정** — 미실행 | 상세 로그 실행 흔적 **0건** |
| 미발견 클래스 | 없음 | **신규 발견 4개 클래스** | 아래 1.3 |

`--list-tests`가 300개만 열거하고 387개 중 일부를 누락했으며, 실행 중 어댑터가 `Result reported for unknown test case`를 다수 출력했습니다. **이 프로젝트에서는 `Passed!/Total:` 줄만 신뢰해야 합니다.**

---

## 1. 실측 결과

### 1.1 실행 기준 수량 (신뢰 구간)

| 필터 | Total | 비고 |
|---|---|---|
| 필터 없음 (NuVatis.Tests) | **387** | 전부 통과, 실패 0, 스킵 0 |
| `Category!=E2E` — **CI 유닛 스텝이 실제로 쓰는 필터** | **338** | |
| `Category=E2E` | **49** | |
| `Category!=E2E&Category!=Testcontainers` | 338 | Testcontainers가 유닛 잡에 포함되지 않음 |
| `Category=Testcontainers` | **0** | ❌ 트레이트가 매칭되지 않음 |
| `Category=E2E&Provider=PostgreSql` — **CI E2E 1단계** | **0** | ❌ |
| `Category=E2E&Provider=MySql` — **CI E2E 2단계** | **0** | ❌ |
| `Category=E2E&Provider=SqlServer` — **CI E2E 3단계** | **0** | ❌ |

### 1.2 🔴 CI E2E 잡 3개 단계 = 0개 테스트 (확정)

`ci.yml`의 e2e-test 잡이 3개 DB 서비스 컨테이너를 띄우면서 **테스트를 하나도 실행하지 않고 green으로 통과**합니다. `Provider` 트레이트가 소스 전체에 **0건**이기 때문입니다.

```
$ dotnet test ... --filter "Category=E2E&Provider=PostgreSql"
No test matches the given testcase filter
```

이건 이전 보고서와 동일하게 **확정된 결함**입니다. 3개 단계 모두 동일.

### 1.3 🔴 신규 발견 — 16개 테스트가 VSTest에 등록되지 않음

`IAsyncLifetime`을 구현한 4개 클래스가 정확히 문제를 일으킵니다.

| 클래스 | 소스 상 테스트 수 | VSTest 카운트 | 상태 |
|---|---|---|---|
| `E2E.PostgreSqlE2ETests` | 7 | **0** | 실행되나 "unknown test case"로 보고됨 |
| `E2E.TestcontainersPostgreSqlE2ETests` | 2 | **0** | **실행 흔적 0건** |
| `E2E.TestcontainersMySqlE2ETests` | 2 | **0** | **실행 흔적 0건** |
| `E2E.TestcontainersSqlServerE2ETests` | 5 | **0** | **실행 흔적 0건** |
| `E2E.SqliteE2ETests` (`IDisposable`) | 6 | 6 | ✅ 정상 |
| `E2E.SqlSessionE2ETests` (`IDisposable`) | 19→12 | 12 | ✅ 정상 |
| `E2E.FullPipelineE2ETests` | 12 | 12 | ✅ 정상 |
| `SqlSessionAdvancedTests` / `SqlSessionAsyncTests` | 12 / 7 | 12 / 7 | ✅ 정상 |

상관관계가 100%입니다. **`IAsyncLifetime`을 쓰면 안 되고, `IDisposable`/없으면 정상 등록됩니다.**

**근본 원인 (높은 확신)**: `Directory.Packages.props`가 `xunit 2.4.2` + `xunit.runner.visualstudio 2.4.5`로 고정돼 있습니다. 2.4.5는 2020년 릴리스로, `IAsyncLifetime` 클래스에서 발견/실행 결과 불일치가 발생하는 알려진 어댑터 결함군에 속합니다. 현재 xunit v2는 2.9.x, runner는 3.x입니다.

**배제된 가설**: `[Collection("Testcontainers")]`에 대응하는 `[CollectionDefinition]`이 없다는 추정. 임시 파일로 정의를 추가해 재빌드·재실행했으나 `Category=Testcontainers` 여전히 0건 → **가설 기각**. (임시 파일은 제거 완료)

> **이 항목은 CI에서도 재현될 가능성이 높으나 확정하지 못했습니다.** CI는 `dotnet-version: 8.0.x`라 vstest 콘솔 버전이 동일해 재현되리라 추정합니다. 플랜 1단계에서 브랜치로 직접 검증하도록 배치했습니다.

### 1.4 🔴 Testcontainers 버전 매트릭스 무의미 (확정)

`e2e-testcontainers.yml`이 `TC_PG_IMAGE` / `TC_MYSQL_IMAGE` 환경변수를 주지만 테스트 코드가 **읽지 않습니다.**

```
$ grep -rn 'GetEnvironmentVariable' tests/NuVatis.Tests/E2E/Testcontainers*.cs
(결과 없음 — 0건)

TestcontainersPostgreSqlE2ETests.cs:58   .WithImage("postgres:16-alpine")   // 하드코딩
TestcontainersMySqlE2ETests.cs:40        .WithImage("mysql:8.0")            // 추정 하드코딩
```

PG 13/14/15/16 매트릭스 4회 + MySQL 8.0/8.4 매트릭스 2회 = **총 6회 실행이 전부 동일 이미지**입니다.

### 1.5 🟠 deprecated API (확정)

```
TestcontainersPostgreSqlE2ETests.cs(57,22): warning CS0618: 'PostgreSqlBuilder.PostgreSqlBuilder()' is obsolete
TestcontainersMySqlE2ETests.cs(39,22):      warning CS0618: 'MySqlBuilder.MySqlBuilder()' is obsolete
```

`MsSqlBuilder`만 이미지 인자를 받고 있어(올바른 패턴), PG/MySQL 두 곳만 뒤처져 있습니다. 다음 Testcontainers 마이그레이션 시 **컴파일 에러로 전환**됩니다.

### 1.6 🟠 49개 E2E 테스트가 CI 어디에서도 실행되지 않음 (확정)

- 유닛 스텝: `Category!=E2E` → 49개 **제외**
- e2e-test 스텝: `Provider` 부재 → 0건 매칭
- `e2e-testcontainers`: `Category=Testcontainers` 필터 → 0건

이 중 `SqliteE2ETests`(6), `FullPipelineE2ETests`(12), `SqlSessionE2ETests`(12), `SqlSessionAdvancedTests`(12), `SqlSessionAsyncTests`(7) = **49개 전부 SQLite 인메모리 기반**입니다. Docker도 네트워크도 필요 없으며, **지금 로컬에서 100% 통과 중**입니다(387/387). 유닛 스텝으로 옮기면 즉시 커버리지가 생깁니다.

### 1.7 🟡 EditorConfig 미강제 (확정)

`.editorconfig`에 `dotnet_diagnostic.IDE0005.severity = warning`가 선언돼 있으나 `EnforceCodeStyleInBuild`가 없어 CI에서 advisory입니다. 강제 시 드러나는 위반:

```
NuVatis.Core (net8.0, EnforceCodeStyleInBuild=true) → 6 Warning(s), 전부 IDE0005
  DynamicSql/TestExpressionEvaluator.cs(4,1)
  Internal/PropertyReflectionCache.cs(1,1) (3,1) (5,1)
  Mapping/ColumnMapper.cs(3,1)
  Statement/MappedStatement.cs(1,1)
```

**불필요한 `using` 6줄뿐**입니다. 제거 비용 대비 규칙 게이트 획득 가치가 매우 큽니다.

### 1.8 🟡 FastTest가 src에 전파되지 않음 (확정)

README는 "net8 단일 타겟 — 속도 대폭 단축(권장)"이라 안내하지만, `FastTest`는 **테스트 csproj에만** 조건문이 있습니다. src는 계속 멀티타겟이라 SDK 8 단일 설치 환경에서:

```
$ dotnet build NuVatis.sln -c Release -p:FastTest=true
error NETSDK1045: The current .NET SDK does not support targeting .NET 9.0.  (×12 프로젝트)
```

### 1.9 🟡 패키징 게이트 취약 (확정)

| 워크플로 | 검증 방식 | 실제 |
|---|---|---|
| `ci.yml` pack-verify | `COUNT -lt 9` 이면 실패 | **14개** 생성 |
| `publish.yml` | 13개 패키지명 개별 확인 | 13개 (강함) |

`ci.yml`의 게이트는 14개 중 5개가 무시되어도 통과합니다. 그리고 `publish.yml`의 강한 검증은 **`v*` 태그에서만** 돌기 때문에, 메인 CI에서는 패키지 누락을 잡지 못합니다.

부수 발견: `NuVatis.QueryBuilder.Tools`는 `IsPackable=true`라 `NuVatis.QueryBuilder.Tools.0.1.0-alpha.nupkg`가 생성되지만 `publish.yml` EXPECTED 목록에 **없어** 배포되지 않습니다. 의도적이면 `IsPackable=false`로 명시하는 편이 안전합니다.

### 1.10 🟡 문서와 워크플로 실제 동작 불일치 (확정)

| README 서술 | 실제 정의 |
|---|---|
| `e2e-testcontainers.yml` → "push (main), PR" | **주간 cron(일 03:00 UTC) + 수동만** |
| `benchmark.yml` → "push (main), PR" | push main(경로 제한) + 수동. **PR 없음** |
| `docs.yml` → "push (main, docs/**)" | `src/**/*.cs`, `src/**/*.csproj` 트리거도 있음 (누락) |
| `dotnet test --filter "Category!=Integration"` = "단위 테스트만 (Docker 불필요)" | 이 필터는 `NuVatis.Tests`의 Testcontainers/E2E를 **제외하지 못함** |

마지막 항목이 특히 위험합니다. 개발자가 "Docker 불필요한 단위 테스트"로 생각한 명령이 실제로는 컨테이너 테스트를 포함합니다.

---

## 2. 영향도 분석

### 2.1 변경 범위별 분류

| 등급 | 내용 | 프로덕션 코드 영향 | 리스크 |
|---|---|---|---|
| **A. 테스트 코드만** | 트레이트 부여, 이미지 환경변수화, 컬렉션 정리 | **없음** | 낮음 |
| **B. 빌드 설정만** | CPM 버전 상향, FastTest 전파, EditorConfig 강제 | **없음** (패키징 산출물 불변) | 중간 |
| **C. 워크플로만** | ci.yml 필터·게이트 수정, README 정정 | **없음** | 낮음 |

**전 항목이 프로덕션 코드(`src/`)를 건드리지 않습니다.** 즉 리팩토링·리스크 영역이 아닙니다.

### 2.2 기능 영향 — 무엇이 "살아나기" 하는가

| 조치 | 활성화되는 검증 | 현재 상태 |
|---|---|---|
| Provider 트레이트 부여 | PG/MySQL/SQL Server 실연동 E2E | 3단계 모두 0건 |
| xunit 러너 상향 | Testcontainers 9개 + PG E2E 7개 | 미실행 / 미집계 |
| Testcontainers 이미지 환경변수화 | DB **버전 호환성** (PG 13~16, MySQL 8.0/8.4) | 6회 모두 동일 이미지 |
| E2E 49개를 유닛 잡으로 | SQLite 파이프라인 49개 | 미실행, **로컬에선 100% 통과 확인** |
| 최소 실행 개수 assertion | 0건 green 통과 재발 방지 | 현재 재발 가능 |

### 2.3 프로덕션 코드에 미치는 영향 (직접)

없습니다. 다만 **테스트가 실제로 돌기 시작하면 프로덕션 결함을 발견할 위험이 생깁니다.** 이는 비용이 아니라 이 플랜의 목적이지만, 다음을 사전에 기대해야 합니다.

- PostgreSQL E2E가 로컬 `localhost:35432`, 사용자 `bee`를 하드코딩하고 있습니다(`PostgreSqlE2ETests.cs:25-26`). CI 서비스 컨테이너는 `localhost:5432`, `nuvatis_test`입니다. **연결 문자열 불일치로 E2E가 즉시 실패할 가능성이 높습니다.** 실행 전에 환경변수화로 정리해야 합니다.
- SQL Server 전용 경로(`SELECT SCOPE_IDENTITY()` 등)는 현재 실행된 적이 없어 **미발견 회귀가 존재할 수 있습니다.**

### 2.4 예상 비용

| 작업 | 규모 |
|---|---|
| A. 테스트 코드 수정 | 파일 7개, 약 80줄 |
| B. 빌드 설정 | `Directory.Packages.props` 2줄, `Directory.Build.props` 6줄 |
| C. 워크플로/문서 | `ci.yml` ~15줄, `README.md` ~10줄 |
| 1.1의 6줄 `using` 제거 | 6줄 |

총 **약 120줄 변경**으로 프로젝트 최대 취약점(검증 공백)을 해소할 수 있습니다.

---

## 3. 개선 플랜

### Phase 1 — 검증 되살리기 (최우선, 1~2일)

> 전부 테스트/워크플로만 변경. 프로덕션 코드 무관.

#### 1-1. xunit 러너 상향 — 무엇보다 먼저

`Directory.Packages.props`에서:

```xml
<PackageVersion Include="xunit"                      Version="2.9.3" />
<PackageVersion Include="xunit.runner.visualstudio" Version="3.1.5" />
<PackageVersion Include="Xunit.SkippableFact"        Version="1.5.61" />
```

**순서상 최우선인 이유**: 1-4(트레이트)와 1-5(실행 개수 assertion)가 제대로 동작하려면 어댑터가 Testcontainers 9개를 정상 등록해야 합니다. 러너가 고쳐지지 않으면 assertion이 즉시 실패해 원인을 혼동하게 됩니다.

- [ ] 브랜치 생성 후 상향
- [ ] 기준선 확보: 현재 387/387 통과 확인
- [ ] 상향 후 재실행 → **387 + 16 = 403개** 기대 (SkippableFact는 스킵 가능)
- [ ] `Category=Testcontainers` 필터가 **9건**을 반환하는지 확인 (현재 0)
- [ ] 회귀 시 롤백 후 Phase 1 나머지를 진행

#### 1-2. CI E2E 트레이트 부여

`Provider` 트레이트를 추가해 3개 단계를 살립니다.

| 파일 | 추가할 트레이트 | 제공 DB |
|---|---|---|
| `E2E/PostgreSqlE2ETests.cs` | `[Trait("Provider", "PostgreSql")]` | PostgreSQL |
| `E2E/TestcontainersPostgreSqlE2ETests.cs` | `[Trait("Provider", "PostgreSql")]` | PostgreSQL |
| `E2E/TestcontainersMySqlE2ETests.cs` | `[Trait("Provider", "MySql")]` | MySQL |
| `E2E/TestcontainersSqlServerE2ETests.cs` | `[Trait("Provider", "SqlServer")]` | SQL Server |
| `E2E/SqliteE2ETests.cs`, `SqlSessionE2ETests`, `FullPipelineE2ETests`, `SqlSessionAdvancedTests`, `SqlSessionAsyncTests` | 불필요 | SQLite |

> ⚠️ **선행 작업**: `PostgreSqlE2ETests`의 하드코딩 연결 문자열(`localhost:35432`, user `bee`)을 `NUVATIS_TEST_PG_CONNECTION` 환경변수 기반으로 변경해야 합니다. 그대로 두면 CI에서 즉시 실패합니다. 이 파일은 1-3의 환경변수화와 함께 처리합니다.

#### 1-3. Testcontainers 이미지 환경변수화 + deprecated 생성자 제거

두 결함을 한 번에 해결합니다.

```csharp
// 현재 (버전 무시 + deprecated)
_container = new PostgreSqlBuilder().WithImage("postgres:16-alpine")

// 변경 (버전 제어 가능 + deprecated 해소)
var image = Environment.GetEnvironmentVariable("TC_PG_IMAGE") ?? "postgres:16-alpine";
_container = new PostgreSqlBuilder(image)
    .WithDatabase("nuvatis_test")
    .WithUsername("test")
    .WithPassword("test")
    .Build();
```

- [ ] PG / MySQL / SQL Server 3개 파일 동일 패턴 적용
- [ ] `MsSqlBuilder`도 `TC_MSSQL_IMAGE`로 통일
- [ ] 빌드 후 `warning CS0618` **0건** 확인

#### 1-4. 실행 개수 assertion — 재발 방지

`ci.yml`의 각 테스트 스텝에 최소 통과 개수를 강제합니다.

```bash
# Unit Tests (Core) 스텝에 추가
--logger "trx;LogFileName=core.trx" \
--results-directory ./TestResults
# 이후 스텝:
- name: Verify test count
  run: |
    PASSED=$(grep -o 'passed="[0-9]*"' ./TestResults/core.trx | head -1 | grep -o '[0-9]*')
    if [[ ${PASSED} -lt 300 ]]; then
      echo "::error::Unit test count ${PASSED} < 300 — 테스트가 누락됐을 수 있음"
      exit 1
    fi
```

| 스텝 | 최소 임계값 | 근거 |
|---|---|---|
| Core unit | 338 | 현재 실측값 |
| Generators | 145 | 현재 실측값 |
| QueryBuilder unit | 76 | 현재 실측값 |
| QueryBuilder.Tools | 15 | 현재 실측값 |
| E2E PostgreSQL | 7 | `PostgreSqlE2ETests` |
| E2E MySQL | 2 | `TestcontainersMySqlE2ETests` |
| E2E SqlServer | 5 | `TestcontainersSqlServerE2ETests` |

> 임계값은 1-1 완료 후 재측정한 값으로 확정합니다. 지금 숫자를 그대로 쓰면 1-1에서 어긋납니다.

#### 1-5. e2e-testcontainers 중복 실행 제거

현재 PG 매트릭스(4회)와 MySQL 매트릭스(2회)가 **둘 다 `Category=Testcontainers` 필터**를 쓰므로 매번 3개 클래스 전부를 실행합니다(총 18회 중 6회만 필요).

```yaml
# 1-2의 Provider 트레이트를 활용한 분리
-e2e-postgres:  --filter "Category=Testcontainers&Provider=PostgreSql"
-e2e-mysql:     --filter "Category=Testcontainers&Provider=MySql"
```

SQL Server는 별도 잡 신설 또는 주간 잡에 편입.

### Phase 2 — 49개 고아 테스트 복귀 (1일)

`Category=E2E` 49개는 전부 SQLite 인메모리 기반이며 로컬에서 통과 중입니다. 유닛 잡으로 옮기면 커버리지가 즉시 338 → 387이 됩니다.

**방안 선택:**

| 방안 | 내용 | 장점 | 단점 |
|---|---|---|---|
| **2-A (권장)** | 트레이트를 `Category=Unit`으로 재분류, 유닛 스텝에서 `Category!=E2E` 필터 유지 | 최소 변경 | 의미 변경이므로 명명 주의 |
| 2-B | `ci.yml` 유닛 필터를 `Category!=E2E&Category!=Testcontainers`로 변경 | 워크플로 1줄 | 49개가 여전히 e2e-test 잡에서 실행되려면 Provider 필요 |

2-A는 1-2에서 SQLite 계열에 `Provider` 트레이트를 부여하지 않음과 함께 적용합니다. 결과적으로:

- 유닛 잡: 338 + 49 = **387개** (SQLite만, Docker 불필요)
- e2e 잡 3단계: PG / MySQL / SQL Server 각 7 / 2 / 5개
- Testcontainers 잡: 버전 매트릭스 정상화

> 2-B는 `Category!=Integration`이 README에 쓰이는 점과 혼동될 수 있어 2-A를 권장합니다.

### Phase 3 — 규칙 게이트와 빌드 속도 (2~3일)

- [ ] `NuVatis.Core`의 IDE0005 6건 제거 (불필요한 `using` 6줄)
- [ ] `Directory.Build.props`에 `EnforceCodeStyleInBuild` 조건부 추가
  - **주의**: 전체 적용은 테스트 프로젝트의 기존 위반까지 CI를 막을 수 있으므로, `src/` 한정 적용 후 위반 0건 확인 후 전체 확대를 권장
- [ ] `FastTest`를 `Directory.Build.props`로 전파

```xml
<PropertyGroup Condition="'$(FastTest)' == 'true' and !$(MSBuildProjectName.Contains('Generators'))">
  <TargetFrameworks>net8.0</TargetFrameworks>
</PropertyGroup>
```

> `NuVatis.Generators`는 `netstandard2.0` 고정이라 제외해야 합니다. 이 프로젝트에서 실제 확인된 함정입니다(오버라이드 시 NETSDK1005 발생).

- [ ] `ci.yml` pack-verify를 `publish.yml`과 동일한 13개 개별 확인으로 강화 (현재 임계값 9 → 14개 중 5개 무시 가능)
- [ ] `NuVatis.QueryBuilder.Tools`의 `IsPackable` 정책 확정 — 미출시 의도면 `false`로 명시

### Phase 4 — 문서 정합성 (반나절)

| 위치 | 현재 | 정정 |
|---|---|---|
| README 485행 | `dotnet test --filter "Category!=Integration"` | `NuVatis.Tests` 기준 `Category!=E2E&Category!=Testcontainers`로 명확히 분리 |
| README 525행 | e2e-testcontainers = "push (main), PR)" | "주간(일 03:00 UTC) + 수동" |
| README 524행 | benchmark = "push (main), PR)" | "push main (src/**, benchmarks/**) + 수동" |
| README 526행 | docs = "push (main, docs/**)" | `src/**/*.cs`, `src/**/*.csproj` 포함 |
| README 495행 | `FastTest=true` "속도 대폭 단축" | Phase 3 완료 후 실제로 성립하므로 유지 |
| `docs/cookbook/testing.md` | Category 규약 부재 | 트레이트 규약 문서화 |

---

## 4. 실행 순서와 위험

```
Phase 1 (1-1 xunit 상향) ←── 다른 모든 조정의 선행조건
    │
    ├─ 1-2 Provider 트레이트 ──┐
    ├─ 1-3 이미지 환경변수화 ──┼─→ Phase 2 (49개 복귀)
    ├─ 1-4 개수 assertion ─────┘
    └─ 1-5 잡 분리
              ↓
        Phase 3 (규칙 게이트, 빌드 속도, 패키징)
              ↓
        Phase 4 (문서)
```

| 위험 | 확률 | 완화 |
|---|---|---|
| xunit 상향 후 기존 테스트 실패 | 중간 | 브랜치 격리, 387/387 기준선 대비 단계적 확인 |
| xunit 상향으로 `SkippableFact` 16개가 스킵 처리 | 높음 | 임계값을 "통과"가 아닌 "발견" 기준(≥1)로 설정 |
| PG E2E가 하드코딩 연결 문자열로 실패 | **확실** | 1-2에서 환경변수화 선행 필수 |
| E2E가 이전엔 안 돌던 결함을 드러냄 | 중간 | **기대되는 결과.** SQL Server 경로 우선 점검 |
| EditorConfig 전체 강제로 CI 차단 | 낮음 | src 한정 적용 → 0건 확인 → 확대 |

---

## 5. 검증 방법

각 Phase 완료 시 아래를 실행합니다. `--list-tests`는 이 프로젝트에서 부정확하므로 **`Total:` 줄만** 사용합니다.

```bash
# 1. 기준선 확인
dotnet test tests/NuVatis.Tests/NuVatis.Tests.csproj -c Release -f net8.0 \
  -p:TargetFrameworks=net8.0 --filter "Category!=E2E"        # ≥ 387 기대
dotnet test tests/NuVatis.Tests/ -c Release -f net8.0 -p:TargetFrameworks=net8.0 \
  --filter "Category=Testcontainers"                          # ≥ 9 기대 (현재 0)
dotnet test tests/NuVatis.Tests/ -c Release -f net8.0 -p:TargetFrameworks=net8.0 \
  --filter "Category=E2E&Provider=PostgreSql"                 # ≥ 7 기대 (현재 0)
dotnet test tests/NuVatis.Tests/ -c Release -f net8.0 -p:TargetFrameworks=net8.0 \
  --filter "Category=E2E&Provider=MySql"                      # ≥ 2 기대 (현재 0)
dotnet test tests/NuVatis.Tests/ -c Release -f net8.0 -p:TargetFrameworks=net8.0 \
  --filter "Category=E2E&Provider=SqlServer"                  # ≥ 5 기대 (현재 0)

# 2. 경고 0건 확인
dotnet build src/NuVatis.Core -c Release -p:TargetFrameworks=net8.0 --no-incremental
# 3. deprecated 경고 제거 확인
dotnet build tests/NuVatis.Tests/ -c Release -f net8.0 -p:TargetFrameworks=net8.0 2>&1 | grep -c CS0618   # 0 기대
```

**완료 판정 기준**: 위 5개 필터가 모두 0을 반환하지 않고, 전체 통과 수가 실측 기준선 이상이며, CS0618이 0건.

---

## 6. 총평

프로젝트 코드 자체는 손댈 필요가 없습니다. `src/` 10,164줄은 TODO 0건, sync-over-async 0건, 순환 의존성 0건, 경고 0건의 상태이고, 테스트/소스 LOC 비율 1.17입니다.

문제는 전부 **검증 배선의 단절**입니다. 그리고 이번 실측으로 그 범위가 이전 파악보다 넓고严重함이 분명해졌습니다.

가장 중요한 발견은 1.3입니다. `Provider` 트레이트 누락만 고쳤다면 3개 E2E 단계가 "켜지지만" Testcontainers 9개는 여전히 어댑터에 등록되지 않아 계속 조용히 실패합니다. **xunit 러너 상향을 먼저 해야 나머지 수정이 의미가 있습니다.** 순서를 뒤집으면 assertion이 바로 빨간불이 되어 원인을 놓치기 쉽습니다.

전체 변경량은 약 120줄이고, 그 중 프로덕션 코드는 0줄입니다. 6줄짜리 `using` 제거로 규칙 게이트를 켤 수 있고, 트레이트 속성 몇 줄로 죽어 있던 16개 DB 테스트가 살아납니다.

가장 중요한 한 가지 예상을 미리 말씀드립니다. **E2E가 실제로 돌기 시작하면 지금까지 한 번도 실행되지 않은 SQL Server 경로에서 회귀가 나올 수 있습니다.** 그건 버그가 아니라 그 동안의 검증 공백이 이제야 표면화되는 것입니다. 계획된 결과로 보고 대응하는 것이 좋습니다.

---

*본 문서의 모든 수치는 실행 결과(`Passed! - Total:`)로 실측했습니다. `dotnet test --list-tests`는 이 어댑터 조합에서 불완전하므로 사용하지 않았습니다. xunit 러너 상향이 CI에서 동일하게 재현되는지는 브랜치 검증이 필요하며, 현재는 SDK 8.0.131 환경 기준 확신도 80%입니다.*
