# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed

- **CI `Verify test counts` 스텝이 windows 러너에서 실패 — 서로 다른 원인 2개**: 두 번에 걸쳐 고쳤고, 둘 다 ubuntu 러너에서는 드러나지 않아 태그 push 뒤에야 발견됐다. **테스트는 매번 전부 통과한 상태였다.**
  1. **셸 미지정**: 스텝이 `declare -A` / `[[ ]]` 등 bash 문법을 쓰는데 `shell:` 이 없어 windows-latest 의 기본 셸인 pwsh 에서 실행됐다. → `shell: bash` 명시. 같은 결함이 있던 `Verify E2E test counts` 와 `Verify package count` 도 ubuntu 전용이라 잠복해 있었으므로 선점 고쳤다.
  2. **근본 원인 — 매 잡이 6개 TFM 을 전부 실행**: 매트릭스는 `dotnet`(설치할 SDK)만 바꿨고 실행할 **TFM 은 고정하지 않았다.** 멀티타겟 프로젝트에 `-f` 가 없으면 `dotnet test` 가 6개 TFM 을 전부 실행하고, 각 실행이 같은 TRX 를 덮어쓴다(로그에 `Total tests: 422` 가 4회 반복되는 것이 증거다). ubuntu 는 덮어쓰기가 성공하지만 windows 는 테스트 호스트가 파일 핸들을 놓지 않아 `Failed to write the results file ... being used by another process` 가 발생한다. 그 결과 **76개·422개가 전부 통과한 스위트가 `passed=0` 으로 오판**되어 잡이 실패했다.
     → 매트릭스에 `tfm` 을 추가해 잡마다 TFM 하나만 실행하도록 고쳤고(`--framework ${{ matrix.tfm }}`), 부수적으로 결과 디렉터리도 스위트별로 분리했다. 잡당 6회 실행이 1회로 줄면서 CI 시간도 크게 준다.
     - 결과적으로 **매트릭스 7개 잡이 모두 동일한 작업을 반복하고 있었고, SDK 버전 차이는 아무것도 검증하지 못했다.** 이제 각 잡이 해당 TFM 을 실제로 검증한다.
  - 함께 임계값을 Release 실측 기준선으로 갱신했다(기존 150 / 390 / 70 / 15 → 165 / 415 / 72 / 14).
  - 5. **검증 스텝의 `grep` 가 `set -e` 에 걸림**: GitHub Actions 의 bash 는 `-e` 로 돌아서, `PASSED=$(grep -oE 'passed="[0-9]+"' ...)` 가 미일치하면 exit 1 인 grep 이 루프 중간에 스크립트를 죽인다. E2E 검증 스텝은 `skipped="..."` 를 찾는데 TRX 루트에 그 속성이 없어(실제 이름은 `notExecuted`) **항상 죽었다.** 트리플이 조용히 죽어 어떤 키도 보고하지 않아, PostgreSQL 7 / MySQL 2 / SQL Server 5가 전부 통과했는데 잡이 실패했다. → 세 `grep` 에 `|| true` 를 붙이고 `notExecuted` 로 정정했다.
  - **windows 최소 개수 검증 복원**: 위 해결 시도 당시 windows 에서는 검증을 ubuntu 로 한정했으나, 스위트 내부에서 TFM 별 실행이 같은 `LogFileName` 의 TRX 를 덮어쓰며 경합하는 것이 원인이었다. 로거를 `LogFilePrefix` 로 바꿔 TFM·타임스탬프가 붙은 별도 파일로 쓰게 했다(Windows 11 ARM64 로컬 실측에서 `Failed to write the results file` 0건). 검증 스텝은 스위트의 TFM 별 TRX 중 최대 passed 를 임계값과 비교하며 windows 에서도 실행한다. net6.0/net7.0 TFM 은 테스트가 0건 실행(TRX total=0)되므로 최대값 비교가 필요하다.
- **`pack.sh` 검증 단계가 `NuVatis.QueryBuilder` 에서 실패**: 글롭 `${PKG}.*.nupkg` 가 `NuVatis.QueryBuilder.Tools.*` 까지 매칭해 `du` 가 인자 2개를 받고 종료했다. `${PKG}.[0-9]*.nupkg` 로 한정했다. 또한 `dotnet test ... || true` 가 테스트 실패를 삼키던 것을 제거하고 Docker 가 필요한 카테고리(`Integration`/`E2E`/`Testcontainers`)를 필터로 제외했다. macOS 기본 bash 3.2 에서 전체 실행으로 검증했다.

### Added

- **CI macOS 러너**: `build-and-unit-test` 매트릭스에 `macos-latest` 추가. 검증 스텝은 macOS 기본 bash 3.2 에서도 동작하도록 연관 배열(`declare -A`)을 제거했다.
- **`.gitattributes`**: `* text=auto eol=lf`, `*.png binary`. Windows `autocrlf` 체크아웃에서도 저장소 기준 줄바꿈을 LF 로 고정한다.
- **CRLF 매퍼 파싱 테스트**: `XmlMapperParserTests.Parse_LineEndings_ProduceSameStatementText` (LF/CRLF).
- **로컬 테스트 가이드**: `docs/troubleshooting.md` 에 Docker 없는 환경의 테스트 필터 추가.

### Known Issues

- net6.0/net7.0 테스트 타깃은 macOS·Windows 로컬 모두에서 테스트가 0건 실행된다. CI 의 .NET 6.0.x/7.0.x 잡은 사실상 빌드만 검증한다. 원인 미조사.

## [2.8.0] - 2026-10-04

> CI가 green인데 테스트를 실행하지 않던 구멍, `<where>` 가 깨진 SQL을 내던 결함,
> 그리고 그 과정에서 실측된 생성 코드 성능 개선을 함께 담는다.
> **public API 변경 없음** — 모든 Unshipped 항목이 비어 있는 상태에서 확정했다.

### Fixed

- **`<where>`가 2개 이상 조건에서 파싱 불가한 SQL을 생성**: `<where>` 안의 `<if>`가 2개 이상 동시에 참이면 두 번째 이후의 `AND`/`OR` 접두사가 앞 조건 끝에 공백 없이 붙어 `WHERE name = @p0AND age >= @p1` 형태의 **문법 오류 SQL**이 만들어졌다. 접두사 제거가 누적 문자열의 맨 앞에서 한 번만 일어나는 반면 조건 사이 구분자가 없기 때문이다. MyBatis의 `TextSqlNode`와 동일하게 각 동적 조각 앞에 구분 공백을 삽입하도록 고쳤다 — `ParameterEmitter.EmitWhereNode`(정적 경로)와 `EmitLambdaNode`의 `__wc_` 블록(레지스트리 람다 경로)의 2개 제품 경로가 대상이다.
  - 조건이 0개 또는 1개인 경우의 출력은 **바이트 단위로 동일**하다(앞 접두사를 제거한 뒤 같은 문자열이 남으므로). 변경되는 것은 현재 파싱 실패하는 2개 이상 조건 케이스뿐이다.
  - 수정 전후 생성 출력 전체를 diff로 대조해 **추가 10줄 / 삭제 0줄**이며 전부 `<where>` 블록 안의 `__sb_.Append(' ');` 임을 확인했다. 골든 스냅샷 기준선도 이에 맞춰 갱신했다.
  - `WhereTagPrefixTests` 6종을 추가했다. 방출된 코드를 Roslyn으로 **실제로 컴파일·실행**해 최종 SQL 문자열을 검증한다 — 기존 테스트처럼 방출 문자열에 `StartsWith("AND "` 가 있는지만 보는 방식으로는 이 버그를 잡을 수 없다. 2·3개 조건, `OR` 접두사, 접두사 없는 조건, 0·1개 조건(기존 동작 보존)을 정적 경로와 람다 경로 양쪽에서 검증한다.
  - **기존 테스트가 왜 이 버그를 놓쳤는지**: (1) E2E 프로젝트는 소스 제너레이터를 analyzer로 참조하지 않아 Core 런타임 경로만 실행하며, Core의 `ParameterBinder.Bind`는 `<where>`를 조립하지 않으므로 이 코드를 애초에 지나지 않는다. (2) 기존 단위 테스트는 방출 문자열의 **부분 문자열**만 검사했다. 두 경로 모두 실제 실행 결과로 검증하는 테스트가 없었다.
- **생성 코드 회귀 스냅샷 부재**: `GeneratorIntegrationTests`는 `Assert.Contains(부분 문자열)`로만 검증해 공백·변수명·SQL 조립 순서가 바뀌어도 통과했다. 4개 코퍼스(속성 매퍼 / if·where·set·choose / 중첩 foreach / ${} 치환)의 **전체 생성 출력을 SHA-256으로 고정**하는 `GeneratorGoldenSnapshotTests`를 추가했다. 방출 경로가 실제로 태워지는지도 별도 테스트로 보장한다 — XML이 깨져 생성기가 아무것도 내지 않아도 해시는 통과하기 때문이다.
- **`TestExpressionEvaluator` 파싱 결함 4건**: `test` 표현식 평가가 문자열 분할(`Split(" or ")`) 기반이라 다음이 깨졌다. (1) 문자열 리터럴 안에 ` or ` / ` and ` 가 있으면 리터럴이 잘려 오동작 (`Name == 'a or b'` → 항상 false), (2) `And` / `Or` 혼합 대소문자를 인식하지 못함, (3) 괄호를 지원하지 않아 `and`가 `or`보다 우선하는 MyBatis 의미와 어긋남, (4) 리터럴 안의 `!=` / `==` 가 비교 연산자로 오인됨. 괄호 → `or` → `and` 순으로 분리하는 재귀 하강 평가로 교체했다. 조회는 문자열 리터럴과 괄호 깊이를 존중하고 `and`/`or` 는 대소문자를 구분하지 않는다.
  - 이 평가기는 프로젝트 내부에서 호출되지 않고 `PublicAPI.Shipped.txt`에 등록된 public API이므로 **기존 내부 동작에는 영향이 없다.** 기존 `TestExpressionEvaluatorTests` 37개는 무수정으로 통과하며, 구/신 구현을 동일 입력에 대조하는 differential test로 정상 범위 26개 표현식의 결과가 동일함을 확인했다.
  - 구분자 단어 경계를 추가해 `Order` 같은 프로퍼티명이 ` or ` 로 잘리지 않는다. 빈 조각 처리는 종전 `Split(RemoveEmptyEntries)`와 동일하게 유지했다.
- **`StringBuilderCache` 제거**: 제품 코드 참조 0건인 thread-local 캐시(42줄)였다. `internal`이며 PublicAPI에 등록돼 있지 않아 계약 변경 없이 삭제했다. 이 클래스를 검증하던 `InternalPoolTests.StringBuilderCache_AcquireAndRelease` 테스트도 함께 제거했다 — 제품에서 쓰이지 않는 코드를 스스로 테스트하는 구조는 미사용 상태를 정상으로 보이게 만든다.

### Added

- **제품 방출 경로의 `${}` 가드 동작 테스트 7종**(`ParameterEmitterLambdaPathTests`): SQL Injection 방어 로직이 실제로 실행되는 경로에는 집중 검증이 없었다 — 기존 13개 테스트가 전부 폐기 예정 경로만 검증했다. 정적 메서드 경로와 람다 경로의 가드 계약, `dbFactory` 비의존, `__sb_`/`__params_`/`__idx_` 로컬 변수 사용을 고정한다.
- **`TestExpressionEvaluator` 경계 사례 테스트 18종**: 리터럴 내 논리 연산자·비교 연산자, 혼합 대소문자, 괄호(중첩 포함), 식별자 경계, 빈 조각 처리를 검증한다.
- **직접 접근 방출 회귀 테스트 10종**(`TypedPropertyAccessTests`): 방출된 코드를 Roslyn으로 **실제로 컴파일·실행**해 SQL과 바인딩된 파라미터 값을 확인한다. 정상 경로(직접 접근으로 올바른 값 바인딩), `(object?)` 캐스트 방출, 대소문자 무시 매칭의 선언 대소문자 방출, 그리고 폴백 계약 4종(미해석 프로퍼티 → 리플렉션으로 `DBNull`, 타입 미해결/`object`/null 타입명 → 해석 없음, 파라미터 null → null 의미 유지, typed 미제공 시 기존 리플렉션 경로 동작)을 고정한다. 문자열 검사만으로는 "직접 접근이 나간 코드가 컴파일되는지"를 확인할 수 없다 — 실제로 `int?` 캐스트가 빠져 컴파일이 깨지는 결함을 이 하네스로 잡을 수 있었다.
- **골든 스냅샷의 경로 공존 보장 테스트 2종**: 코퍼스가 한쪽으로 쏠리면 어느 경로의 회귀를 놓친다. 직접 접근(`__typed_`)과 리플렉션(`__getprop_`)이 **둘 다** 등장하는지, 그리고 직접 접근이 발동한 statement에는 같은 프로퍼티에 대한 잔여 리플렉션 호출이 없는지 검사한다. `object` 파라미터 코퍼스가 폴백을 계속 태우는지도 별도로 고정한다.

### Changed

- **생성 코드의 `__getprop_` 리플렉션을 정적 타입 직접 접근으로 대체**(`TypedPropertyAccess`): 매핑 파라미터의 정적 타입이 Roslyn 심볼로 해석되면, 해당 타입에 실제로 존재하고 읽을 수 있는 프로퍼티에 대해 `GetType().GetProperty(...)` 대신 컴파일된 직접 접근을 방출한다. 심볼 해석·선언 접근자·상속 탐색을 Roslyn으로 수행하고, 테스트 구문을 파라미터 타입에 대조해 **집합에 없는 프로퍼티는 기존 리플렉션 경로로 되돌린다.** 따라서 오타 프로퍼티가 컴파일 에러로 바뀌는 등 기존 동작 변화가 없다.
  - 방출 형태: `__getprop_(__param_, "Name")` → `__typed_?.Name`, 접두로 `var __typed_ = __param_ is global::X ? (global::X)__param_ : (global::X?)null;` 한 줄. 타입이 맞지 않거나 null이면 `__typed_` 가 null 이라 종전의 "없으면 null" 의미가 그대로 보존된다.
  - **폴백이 그대로 남는 경우**: 중첩 경로(`#{user.Address.City}` — 세그먼트 타입 잇기 미구현), foreach 아이템 프로퍼티(`#{it.Name}` — 엘먼트 타입 해석 미구현), 파라미터 타입이 `object`/`dynamic`/미해결, 읽기 전용이 아닌 비공개 프로퍼티, 쓰기 전용 프로퍼티.
  - 값 대입 시 `(object?)` 캐스트를 넣었다. `__typed_?.MinAge` 는 `int?` 라 `?? DBNull.Value` 가 성립하지 않고 컴파일이 깨진다.
  - 프로퍼티 조회는 `OrdinalIgnoreCase` 다. `__getprop_` 의 `BindingFlags.IgnoreCase` 와 의미를 맞추기 위함이며, 방출에는 **선언된 정확한 대소문자**를 쓴다.
  - 두 제품 경로(프록시 `BuildSql_XXX` 정적 메서드, 레지스트리 `DynamicSqlBuilder` 람다) 모두 적용된다. 방출 접합점은 `BuildLambdaNestedAccess` 하나라 두 경로의 동작이 갈라지지 않는다.
  - public API 표면은 넓히지 않았다. `TypedPropertyAccess` 는 `internal` 이라 `EmitBuildSqlStaticMethod` / `EmitDynamicBuilderLambda` / `RegistryEmitter.Emit` 에 **internal 오버로드**를 추가해 기존 public 시그니처를 그대로 남겼다. 테스트에서 직접 검증하기 위해 `InternalsVisibleTo("NuVatis.Generators.Tests")` 를 추가했다.
  - **성능에 대한 정직한 범위**: SQL 조립 단독으로는 1,536.3ns → 706.8ns(2.17배)이지만, 실제 쿼리까지 포함하면 28,152.3ns → 26,753.1ns 로 **약 5%** 이다. 조립 절감폭(829ns)이 전체 쿼리의 약 3% 에 불과하기 때문이다. 네트워크 DB에서는 상대 이득이 1% 미만으로 내려간다. 상세 실측은 아래 Known Limitations 참고.
- `.editorconfig` 규칙 빌드 강제: `EnforceCodeStyleInBuild`가 설정되지 않아 IDE 규칙이 CI에서 advisory 상태였다. `src`에 활성화하고 불필요한 `using` 12건을 제거했다. 패키지에 XML 문서가 포함되어 소비자 IntelliSense가 제공된다. `NuVatis.Generators`는 netstandard2.0 소스 제너레이터로 제외했다.
- `e2e-testcontainers.yml` 잡을 `Provider` 트레이트로 분리했다.
- `NuVatis.QueryBuilder.Tools`의 `IsPackable` 정책이 `publish.yml` 배포 목록과 어긋나던 점에 주석으로 명시했다.
- `coverlet.collector` 6.0.0 → 10.1.0 (CI의 `XPlat Code Coverage` 수집 경로 동작 확인).
- `benchmark.yml`의 "성능 회귀 감지"는 실제로 현재 결과만 출력하는 요약 스텝이었다. `Performance Summary`로 이름을 정정하고 기준선 비교 미구현 사실을 주석에 명시했다.
- `PostgreSqlE2ETests` 기본 연결 문자열을 Docker PostgreSQL 표준값으로 변경했다.
- 지원 TF 정책: .NET 6.0 / 7.0은 EOL이므로 해당 타겟이 EOL 패키지를 참조하며 보안 패치가 제공되지 않는 사실을 README에 명시했다.
- `System.CommandLine`이 RC 전 상태라는 사실과 배포 대상이 아니라는 근거를 주석으로 남겼다.
- **CI 최소 테스트 개수 검증**을 추가했다. 유닛 4개 스위트와 E2E 3개 DB 단계가 최소 통과 개수를 만족하지 않으면 실패한다 — `Provider` 트레이트 누락으로 0건이 매칭되어도 green으로 통과하던 결함을 구조적으로 차단한다. 임계값은 **Release 빌드** 실측 기준선(generators 173 / core 422 / querybuilder 76 / tools 15)에서 잡았다.
- **xunit 러너 상향**: `xunit 2.4.2 → 2.9.3`, `xunit.runner.visualstudio 2.4.5 → 3.1.5`. 구 버전에서는 `IAsyncLifetime` 구현 클래스가 VSTest에 등록되지 않아 Testcontainers 계열 9개가 미실행되고 PostgreSQL E2E 7개가 집계되지 않았다. 전체 테스트 387 → 410 (구조 개선 후 423).
- **테스트 트레이트 재분류**: SQLite 인메모리 E2E 49개를 `Category=E2E` → `Category=SqliteE2E`로 옮겨 CI 유닛 잡에서 실행되도록 했다(338 → 422).
- **Testcontainers 이미지 환경변수화**: `TC_PG_IMAGE` / `TC_MYSQL_IMAGE` / `TC_MSSQL_IMAGE`을 코드에서 읽도록 해, 주간 잡의 버전 매트릭스가 실제로 해당 DB 버전을 검증한다. 종전에는 6회 실행이 모두 동일 이미지를 사용했다.
- **deprecated Testcontainers 생성자 제거**: `PostgreSqlBuilder()` / `MySqlBuilder()` / `MsSqlBuilder()` 파라미터 없는 생성자(CS0618)를 이미지 문자열을 받는 생성자로 교체했다(5곳).
- **잘못된 XML 주석 3곳 수정**: `<AppDbContext>`, `<TContext>`, `<bind>`가 닫히지 않아 CS1570이 발생했다.
- **`FastTest` 프로필 전파**: src 멀티타겟까지 net8.0으로 축소해, .NET 8 SDK 단일 설치 환경에서 sln 빌드가 NETSDK1045로 실패하던 문제를 해결했다.
- `SecondLevelCacheTests`의 `Task.WaitAll` 교착 가능성(xUnit1031)을 `Task.WhenAll`로 교체했다.
- `QueryBuilderTests`의 `Assert.Equal(1, count)`를 `Assert.Single`로 교체했다.

### Known Limitations (미해결)

- `NuVatis.Generators`의 `ParameterEmitter.EmitBuildSqlMethod`와 그 하위 `EmitNode` 계열(합계 약 384줄)은 **제품 코드에서 호출 지점이 0건**이나 `PublicAPI.Shipped.txt`에 public API로 등록돼 있어 제거할 수 없다. `[Obsolete]`로 표시해 v3.0에서 제거할 수 있게 했다(SqlSessionFactoryBuilder.AddXmlConfiguration과 동일한 패턴). 제거 시 해당 경로를 검증하는 테스트 2개 파일도 함께 삭제해야 한다.
- **`${}` 컴파일 타임 최적화는 발동 조건이 거의 없어 배선하지 않았다**: `paramTypeMap`을 통해 SqlIdentifier 타입을 빌드타임에 판별해 런타임 가드를 생략하는 로직은 **폐기 예정 경로에만** 남아 있고, 제품 경로(`EmitBuildSqlStaticMethod` / `EmitDynamicBuilderLambda`)는 `paramTypeMap`을 받지 않아 항상 런타임 타입 가드를 방출한다. 배선을 검토했으나 다음 두 이유로 하지 않았다. (1) `paramTypeMap`의 키는 **메서드 파라미터 이름**인 반면 조회 대상은 `${}`의 **프로퍼티 이름**이라, 일반적인 `Search(SearchParam param)` + `${SortColumn}` 패턴에서는 키가 맞지 않아 최적화가 전혀 발동하지 않는다(실측). (2) 실측 비용에서 가드는 회당 1.08ns로, 생성 코드가 매번 수행하는 `GetType().GetProperty(BindingFlags)` 리플렉션(회당 약 368ns)의 0.3%에 불과하다. 가드를 없애도 성능 개선이 0.3% 미만이며, 실질 개선하려면 생성 코드의 프로퍼티 접근 캐시화가 필요하다.
- **`DynamicSqlEmitter.EmitSqlBuilder`는 파이프라인에 연결돼 있지 않으며, 자체 결함도 갖고 있다**: `RegistryEmitter`는 같은 클래스의 `HasDynamicNodes`만 호출하고 `EmitSqlBuilder`는 `src` 어디에서도 호출되지 않는다(`PublicAPI.Shipped.txt`에는 public API로 등록). 게다가 이 메서드의 `<where>` 블록은 `__whereBuilder`를 생성만 하고 `__sql`에 연결하지 않아 조건 내용을 전혀 수집하지 못하고, 자식 노드가 `__sql`에 직접 append되므로 `<where>`가 항상 빈 문자열을 내보낸다. `<where>` 수정 대상에서 **의도적으로 제외**했다 — 죽은 경로의 결함을 고치는 것은 배선/제거 판단 없이는 의미가 없기 때문이다. `docs/architecture/source-generator.md`가 이 클래스를 파이프라인 상에 그려둔 것은 낡은 문서다.
- **생성 코드의 `__getprop_` 리플렉션 제거 — 실측 근거와 미구현 범위**: `DynamicSqlBuildBenchmark`로 실제 생성 출력(`BuildSql_Search`)을 그대로 옮겨 측정한 결과다.
  - **SQL 조립 단독**: 1,536.3 ns → 706.8 ns (2.17배, 절감 829 ns), 할당 1.99KB → 1.75KB.
  - **실제 쿼리까지 포함(SQLite 인메모리)**: 28,152.3 ns → 26,753.1 ns, **약 5%** 수준이다. 조립 비용 절감폭(829 ns)이 전체 쿼리 비용의 약 3%에 불과하다.
  - 따라서 **"2배 개선"이라는 조립 단독 수치를 그대로 신뢰하면 안 된다.** 실제 DB 왕복(네트워크 DB는 통상 0.5~5 ms)이 지배적인 환경에서는 상대 이득이 1% 미만으로 내려간다. `SqlSession.BuildSql`은 SQL 문자열을 캐시하지 않아(`SqlSession.cs:737-743`) 매 실행마다 이 비용을 내지만, 결과 캐시 히트 시에는 건너뛴다.
  - **직접 접근 최적화는 구현 완료** — 위 Changed 항목 참조. 다만 중첩 경로와 foreach 아이템 프로퍼티는 여전히 리플렉션에 남아 있다. 세그먼트별 타입을 잇는 심볼 해석이 추가로 필요하며, 이를 넣으면 폴백 면적이 줄어 실제 절감폭이 늘어난다.
  - `DynamicSqlBuildBenchmark` 는 생성 코드에서 옮긴 미러라, 생성 출력이 바뀌면(where 수정, 직접 접근 도입 등) 함께 갱신해야 한다. 기준선 해시를 파일 주석에 적어 두었다.
- `TypeResolver.IsNullableType`도 public API로 등록돼 있어 유지한다.
- .NET 6.0 / 7.0 타겟은 EOL이며 유지 여부는 제품 결정 사항이다.
- XML 주석 누락(CS1591)은 다수 존재해 `NoWarn`으로 두고 별도 과제로 남긴다.


- **CI E2E 3단계가 테스트를 0개 실행**: `ci.yml` e2e-test 잡이 `Category=E2E&Provider=PostgreSql` 필터를 사용했으나 `Provider` 트레이트가 소스에 전혀 정의돼 있지 않아 3개 단계(PDB 서비스 컨테이너를 기동하면서)가 모두 0건 매칭으로 green 통과했다. PostgreSQL/MySQL/SQL Server 통합 경로에 대한 CI 검증이 사실상 부재했던 상태다. 관련 클래스에 `Provider` 트레이트를 부여해 각 단계를 실제 테스트와 연결했다.
- **Testcontainers 9개 + PostgreSQL E2E 7개 미실행**: `xunit.runner.visualstudio 2.4.5`에서 `IAsyncLifetime` 구현 테스트 클래스가 VSTest에 등록되지 않았다. Testcontainers 계열 3개 클래스는 아예 발견되지 않았고, `PostgreSqlE2ETests` 7개는 실행되나 "Result reported for unknown test case"로 보고되어 집계에 반영되지 않았다. `xunit 2.4.2 → 2.9.3`, `xunit.runner.visualstudio 2.4.5 → 3.1.5`로 상향해 해결했다. 전체 테스트 수 387 → 410.
- **Testcontainers 다중 버전 매트릭스 무효화**: `e2e-testcontainers.yml`이 `TC_PG_IMAGE`/`TC_MYSQL_IMAGE`/`TC_MSSQL_IMAGE` 환경변수를 주입했으나 테스트 코드가 이를 읽지 않고 `WithImage("postgres:16-alpine")` 등으로 하드코딩하고 있었다. PG 13/14/15/16 매트릭스 4회와 MySQL 8.0/8.4 매트릭스 2회가 모두 동일 이미지를 실행해 DB 버전 호환성이 검증되지 않았다. 이미지 문자열을 생성자에 전달하는 패턴으로 전환해 환경변수를 존중하도록 변경했다.
- **SQLite E2E 49개가 CI에서 미실행**: `Category=E2E`인 SQLite 인메모리 테스트 49개가 유닛 잡의 `Category!=E2E` 필터로 제외되고 e2e 잡에서는 0건 매칭이 되어 어느 곳에서도 실행되지 않았다. `Category=SqliteE2E`로 재분류해 유닛 잡에서 실행되도록 변경했다(유닛 실행 수 338 → 394).
- **패키지 검증 게이트 취약**: `ci.yml` pack-verify가 `COUNT -lt 9` 개수 기준이라 14개 중 5개가 누락돼도 통과했다. `publish.yml`과 동일한 13개 패키지명 개별 검증으로 변경했다.
- **`FastTest` 프로필 미작동**: `FastTest=true`가 테스트 프로젝트에만 적용되고 `src` 멀티타겟에는 전파되지 않아, .NET 8 SDK 단일 설치 환경에서 `dotnet build NuVatis.sln /p:FastTest=true`가 `NETSDK1045`로 실패했다. README가 "속도 대폭 단축(권장)"이라 안내하고 있었으나 실제로는 동작하지 않았다. `Directory.Build.targets`에 FastTest TFM 오버라이드를 추가했다(`NuVatis.Generators`는 netstandard2.0 고정이라 제외).
- **잘못된 형식의 XML 주석 3곳**: `NuVatisEfCoreExtensions.cs`(`<AppDbContext>`), `NuVatisEfCoreOptions.cs`(`<TContext>`), `ParsedSqlNode.cs`(`<bind>`)의 XML 문서에 닫는 태그가 없어 CS1570이 발생했다. 이스케이프 처리했다.
- **Testcontainers deprecated 생성자**: `PostgreSqlBuilder()`/`MySqlBuilder()`/`MsSqlBuilder()` 파라미터 없는 생성자가 폐기 예정(`CS0618`)으로 경고와 함께 다음 Testcontainers 메이저 업그레이드 시 컴파일 오류로 전환될 상태였다. 이미지 문자열을 생성자에 전달하도록 변경했다(`NuVatis.Tests` 2곳, `NuVatis.QueryBuilder.Tests` 3곳).
- **차단형 태스크 대기**: `SecondLevelCacheTests.MemoryCacheProvider_ConcurrentAccess_ThreadSafe`가 `Task.WaitAll`을 사용해 교착 가능성이 있었다(`xUnit1031`). `Task.WhenAll` + `async Task`로 변경했다.

### Added

- **CI 최소 테스트 개수 검증**: 유닛 4개 스위트와 E2E 3개 DB 단계를 최소 통과 개수로 검증해, 0건 매칭의 green 통과가 재발하지 않도록 했다. `Provider` 트레이트 누락이 다시 발생하면 즉시 잡이 실패한다.
- **테스트 트레이트 규약 문서화**: README에 Category/Provider 트레이트와 실행 환경 대응표를 추가했다.
- **Testing Cookbook에 CI 실행 구성 명시**: 단계별 필터와 최소 통과 개수, 외부 DB 환경변수(`NUVATIS_TEST_PG_CONNECTION`, `TC_PG_IMAGE`, `TC_MYSQL_IMAGE`, `TC_MSSQL_IMAGE`)를 문서화했다. `xunit.runner.visualstudio`가 3.x 미만이면 `IAsyncLifetime` 클래스가 조용히 미실행된다는 점을 경고로 남겼다.

### Changed

- **`.editorconfig` 규칙 빌드 강제**: `EnforceCodeStyleInBuild`가 설정되지 않아 `.editorconfig`의 IDE 규칙이 CI에서 advisory 상태였다. `src`에 `EnforceCodeStyleInBuild`와 `GenerateDocumentationFile`을 활성화했다(IDE0005 검사는 문서 파일 생성이 전제 조건 — roslyn#41640). 불필요한 `using` 12건을 제거했다. 패키지에 XML 문서가 포함되어 소비자 IntelliSense가 제공된다. `NuVatis.Generators`는 netstandard2.0 소스 제너레이터로 이식성 유지를 위해 제외했다.
- **`e2e-testcontainers.yml` 잡 분리**: PG/MySQL 잡이 모두 `Category=Testcontainers` 단독 필터를 사용해 3개 DB 클래스를 전부 실행했다. `Provider`로 분리해 매트릭스가 해당 DB 버전만 검증하도록 변경했다.
- `NuVatis.QueryBuilder.Tools`의 `IsPackable` 정책이 `publish.yml` 배포 목록과 어긋나던 점에 주석으로 명시했다.
- **`coverlet.collector` 6.0.0 → 10.1.0**: CI의 `XPlat Code Coverage` 수집 경로가 그대로 동작함을 Cobertura 리포트 생성으로 확인했다.
- **`benchmark.yml` 정정**: 헤더와 스텝명이 "성능 회귀 감지"를 암시했으나 실제로는 현재 결과만 출력하는 요약 스텝이었다. `Performance Summary`로 이름을 바꾸고 기준선 비교 미구현 사실을 주석에 명시했다.
- **`PostgreSqlE2ETests` 기본 연결 문자열 표준화**: 개인 개발 설정으로 보이는 `localhost:35432` / `bee` 폴백을 Docker PostgreSQL 표준 기본값(`localhost:5432` / `postgres`)으로 변경했다. CI는 `NUVATIS_TEST_PG_CONNECTION`으로 덮어쓴다.
- **지원 TF 정책 문서화**: .NET 6.0 / 7.0은 EOL이므로 해당 타겟이 `Microsoft.Extensions.* 6.*/7.*` 등 EOL 패키지를 참조하며 보안 패치가 제공되지 않는 사실을 README에 명시했다. 타겟 제거는 별도 결정 사항이다.
- `System.CommandLine`이 2.0 RC 전 상태라는 사실과, `NuVatis.QueryBuilder.Tools`가 배포 대상이 아니므로 영향 범위가 제한적이라는 근거를 주석으로 남겼다.

### Added

- **`JsonTypeHandler<T>` 단위 테스트 8종**: `RegisterTypeHandler<T>`로 등록되는 public API였으나 커버리지가 0%였다. 직렬화/역직렬화, `DBNull`/빈 문자열 처리, 커스텀 `JsonSerializerOptions`(네이밍 정책) 반영, 컬렉션 왕복(DB 저장 후 재조회)을 검증한다.
- **`TimeOnlyTypeHandler.GetValue` 타입 분기 테스트 3종**: `TimeSpan` / `DateTime` 변환과 미지원 타입 예외 경로가 미검증 상태였다. 컬럼 타입을 직접 제어할 수 있는 `DataTableReader`로 검증한다(SQLite TEXT 컬럼은 `GetValue`가 `string`을 반환해 분기를 재현할 수 없다).
- **CI 최소 테스트 개수 검증**: 유닛 4개 스위트와 E2E 3개 DB 단계를 최소 통과 개수로 검증해, 0건 매칭의 green 통과가 재발하지 않도록 했다. `Provider` 트레이트 누락이 다시 발생하면 즉시 잡이 실패한다.

## [2.7.0] - 2026-07-07

### Changed

- **`SqlIdentifier.JoinTyped<T>`**: 리터럴 렌더링을 `InvariantCulture` 고정 포맷으로 전환 (숫자형 소수점, 날짜·시간 포맷이 실행 문화권과 무관하게 일정). `DateTime` 리터럴 출력 포맷이 `'yyyy-MM-dd HH:mm:ss.fffffff'`로 변경된다 (종전: 문화권 의존 기본 `ToString()`). 지원 타입을 숫자형 11종·enum·Guid·DateTime·DateTimeOffset·DateOnly·TimeOnly로 한정하고, 종전에 수용되던 `bool`·`char`·사용자 정의 struct는 `ArgumentException`을 발생시킨다 (breaking behavior change). enum은 underlying 정수값으로 인라인된다.
- **`SqlIdentifier.From`**: 검증 방식을 식별자 형식 화이트리스트(문자(유니코드)/밑줄 시작, 문자·숫자·밑줄·`$`·`#`, 점 구분 다단계)로 전환. 공백·등호·괄호·대괄호·백틱 등 식별자 형식을 벗어나는 입력은 `ArgumentException`을 발생시킨다 (breaking behavior change — 종전 블랙리스트에서는 이 중 일부가 통과했다). SQL 키워드 거부는 종전과 동일하게 유지된다.

### Deprecated

- **`SqlSessionFactoryBuilder.AddXmlConfiguration(string)` / `Build(string)`**: `[Obsolete]` 처리. 호출 시 `NotSupportedException`을 발생시킨다. XML 매퍼는 빌드타임 Source Generator가 처리하므로 런타임 로드 경로가 없으며, 설정 경로를 저장만 하고 사용하지 않던 종전 동작을 명시적 예외로 대체했다. v3.0에서 제거 예정.

## [2.6.0] - 2026-03-31

### Added

- **`MappedStatement.RowMapper`**: `resultType` 쿼리용 SG 생성 행 매퍼 델리게이트 프로퍼티 추가. `SqlSession.SelectOne/SelectList` 및 Async/Stream 변형 5개에서 null이 아니면 리플렉션 없이 SG 매퍼를 직접 호출한다.
- **`NuVatisTypeMappers` 공유 정적 클래스 SG 생성**: Source Generator가 `resultType`-only 스테이트먼트의 행 매핑 메서드를 프록시 내부가 아닌 `NuVatisTypeMappers.g.cs` 공유 클래스에 생성한다. 프록시와 레지스트리 양쪽에서 참조 가능하다.
- **.editorconfig**: .NET 표준 코딩 스타일 규칙 추가 (indent, var 사용, naming, 패턴 매칭 등)

### Changed

- **`ProxyEmitter`**: `resultType`-only 스테이트먼트의 매핑 메서드를 인라인 생성에서 `global::NuVatis.NuVatisTypeMappers.Map_T_XXX` 공유 클래스 참조로 전환.
- **`RegistryEmitter`**: `resultType` 스테이트먼트 등록 시 `RowMapper = reader => global::NuVatis.NuVatisTypeMappers.Map_T_XXX(reader)` 람다를 emit하도록 확장.
- **Central Package Management(CPM)**: `Directory.Packages.props` 도입으로 18개 프로젝트 패키지 버전 중앙 관리 전환. TF-조건부 패키지는 `VersionOverride`로 per-TF 버전 유지
- **Microsoft.NET.Test.Sdk**: 17.6.0 → 17.13.0 통일 (4개 테스트 프로젝트 전체)
- **Microsoft.CodeAnalysis.CSharp**: Generators.Tests 4.8.0 → 5.3.0 (Generators와 버전 일치)
- **ResultMapper**: bare `catch {}` → `catch (IndexOutOfRangeException)` 한정 및 의도 주석 추가
- **TestExpressionEvaluator**: 타입 변환 `catch` 블록 의도 주석 추가
- **`NuVatis.QueryBuilder` PublicAPI**: `PublicAPI.Unshipped.txt` 228개 엔트리를 `PublicAPI.Shipped.txt`로 이관. v2.4.0에서 추가된 QB API가 처음으로 공식 Shipped API로 등록됨.
- **Microsoft.Data.SqlClient**: 6.0.1 → 7.0.0 (Azure 의존성 분리, 패키지 경량화)
- **Testcontainers**: PostgreSql/MySql 4.2.0 → 4.10.0 (MsSql 4.10.0과 버전 통일)

### Fixed

- **`PropertyReflectionCache.Build()` IL2070**: `[RequiresUnreferencedCode]` 어노테이션 추가로 AOT 어노테이션 체인 완성.
- **`ResultMapper.ProcessCollection()` IL3050**: `#pragma warning disable` 목록에 IL3050 추가 (`MakeGenericType` 호출 정적 분석 경고 해소).
- **Generators.Tests 빌드 오류**: NuVatis.Generators(CodeAnalysis 5.3.0)와 Generators.Tests(CodeAnalysis 4.8.0) 버전 불일치로 인한 CS1705 오류 수정
- **Microsoft.SourceLink.GitHub**: 버전 `10.0.0` → `10.0.102` (NU1603 × 19 해소)
- **`LazyValue<T>` CS1587**: XML 문서 주석을 `[UnconditionalSuppressMessage]` 속성 앞으로 이동
- **`ResultMapper` CS8604**: `ProcessCollection` 호출 시 non-null 보장 변수에 null-forgiving 연산자 추가
- **CI Testcontainers 버전 불일치**: PostgreSql/MySql(4.2.0)과 MsSql(4.10.0) 간 `ContainerConfiguration` 생성자 시그니처 불일치로 `MissingMethodException` 발생하던 문제 해결
- **CI SqlServerProvider 테스트 실패**: Microsoft.Data.SqlClient 6.0.1의 `System.Data.SqlClient` 레거시 어셈블리 참조로 `FileNotFoundException` 발생하던 문제 해결

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
| NuVatis.Core | 2.6.0 |
| NuVatis.Generators | 2.6.0 |
| NuVatis.PostgreSql | 2.6.0 |
| NuVatis.MySql | 2.6.0 |
| NuVatis.SqlServer | 2.6.0 |
| NuVatis.Sqlite | 2.6.0 |
| NuVatis.Oracle | 2.6.0 |
| NuVatis.QueryBuilder | 2.6.0 |
| NuVatis.Extensions.DependencyInjection | 2.6.0 |
| NuVatis.Extensions.OpenTelemetry | 2.6.0 |
| NuVatis.Extensions.EntityFrameworkCore | 2.6.0 |
| NuVatis.Extensions.Aspire | 2.6.0 |
| NuVatis.Testing | 2.6.0 |
