# NuVatis 구조 개선 플랜 — 기능 보존 우선

**작성일**: 2026-10-04
**기준**: `fix/ci-verification-gaps` 브랜치 (검증 공백 개선 이후 상태)
**원칙**: **기존 동작을 바꾸지 않는다.** 이 플랜의 모든 항목은 "기존 테스트가 그대로 통과함을 조건"으로 하며, 새 기능이나 동작 변경은 별도로 분리한다.

---

## 0. 요약

구조 분석에서 **즉시 제거 가능한 죽은 코드 2건**과 **실제 동작 버그 1건**을 발견했습니다. 반면 이 프로젝트의 가장 큰 구조적 특성인 sync/async 중복(48쌍)은 **의도된 설계로 판명되어 축소하지 않기를 권장**합니다.

| 등급 | 항목 | 규모 | 기능 훼손 위험 |
|---|---|---|---|
| **P0** | `TestExpressionEvaluator` 파싱 버그 4건 | 1파일 | **낮음** (내부 호출 0건) |
| **P0** | 미사용 코드 제거 2건 | 85줄 | **매우 낮음** |
| **P1** | SG 생성 코드 중복 구조 정리 | ~60줄 | 낮음 |
| **P2** | `SqlSession` 책임 분리 | 구조 변경 | **중간** |
| **권장 보류** | sync/async 중복 축소 | 48쌍 | **높음** — 하지 말 것 |

**이번 분석에서 코드를 변경하지 않았습니다.** 이 문서는 계획입니다.

---

## 1. P0 — `TestExpressionEvaluator`의 실제 동작 버그

### 1.1 무엇이 틀렸는가

`src/NuVatis.Core/DynamicSql/TestExpressionEvaluator.cs`는 XML 문서에서 **"MyBatis 호환 test 표현식"**이라고 명시합니다. 하지만 MyBatis의 `test` 표현식이 지원하는 핵심 동작 중 4개가 구현돼 있지 않습니다.

임시 프로브 테스트로 **실패를 재현 확인**했습니다 (5개 중 4개 실패).

| # | 시나리오 | 기대 | 실제 | 원인 |
|---|---|---|---|---|
| 1 | `Name == 'a or b'` | `true` | **false** | `" or "` 기준으로 문자열을 나눠 문자열 리터럴이 파괴됨 (L30) |
| 2 | `Name == 'rock and roll'` | `true` | **false** | 동일 (`" and "` 분할, L35) |
| 3 | `Age > 1 Or Age > 100` | `true` | **false** | `" or "` / `" OR "`만 등록되어 ` Or ` 혼합 대소문자를 못 인식 (L30) |
| 4 | `Age > 1 And Age > 100` | `false` | **true** | `" and "` / `" AND "`만 인식 (L35) |
| 5 | `(Age > 100 or Age > 1) and Age > 2` | `true` | **false** | 괄호 미지원 — 선행 `or`가 전체를 단항으로 평가 |

### 1.2 왜 지금 발견되지 않았는가

이 평가기는 **프로젝트 내부에서 한 번도 호출되지 않습니다.**

```
$ grep -rn 'TestExpressionEvaluator\.Evaluate' src   →  0건
```

Source Generator 경로는 `DynamicSqlEmitter.ConvertTestExpression`으로 `test`를 **빌드타임 C# 코드로 변환**하므로 런타임 평가기를 쓰지 않습니다. `Evaluate`는 `PublicAPI.Shipped.txt`에 등록된 **public API**로, 외부 소비자가 직접 호출할 수 있는 표면입니다.

즉 **기존 기능에는 영향이 없지만, 공개된 계약이 문서화된 기대치를 충족하지 못합니다.**

### 1.3 영향 범위

| 항목 | 평가 |
|---|---|
| 기존 라이브러리 동작 | **영향 없음** (내부 호출 0건) |
| SG 경로 | **영향 없음** (별도 코드 경로) |
| public API 소비자 | **영향 있음** — 기대대로 동작하지 않음 |
| MyBatis 마이그레이션 사용자 | **영향 큼** — 마이그레이션 문서를 안내하는 프로젝트에서 함정이 됨 |

`docs/cookbook/migration-from-dapper.md`, `docs/reference/xml-mapper-reference.md`가 MyBatis 호환성을 전면에 내세우고 있어 문서-구현 괴리가 되지 않도록 해야 합니다.

### 1.4 개선안

**문자열 분할 재귀를 토큰 기반 재귀 하강 파서로 교체**합니다.

```
현재:  Evaluate → Split(" or ") → 재귀 → Split(" and ") → 재귀 → EvaluateSubExpression
개선:  Tokenizer(문자열 리터럴/연산자/괄호 구분) → 재귀 하강
       precedence: or < and < 비교
```

- 문자열 리터럴(`'...'`)을 먼저 토큰화해 `and`/`or` 연산자로 오인하지 않게 함
- `And`/`Or` 혼합 대소문자를 토큰 비교로 흡수
- 괄호를 재귀 하강의 기본으로 지원
- 기존 지원 문법(`!=`, `==`, `>=`, `<=`, `>`, `<`)은 그대로 유지

**기능 훼손 방지 조건:**

1. **기존 `TestExpressionEvaluatorTests` 37개가 수정 없이 전부 통과해야 한다.** 이 테스트가 현재 지원 문법의 계약이므로, 새 파서가 이 계약 이상을 충족해야 합니다.
2. 기존 동작이 달라지는 케이스가 발견되면 **변경하지 말고 실패로 남깁니다** — "개선"이 아니라 "호환성 축소"입니다.
3. `PropertyReflectionCache` 의존과 `IL2070`/`IL2026` suppress는 그대로 유지합니다 (AOT 영향 없음).

**예상 위험: 낮음.** 내부 호출이 없어 기존 테스트 외 회귀 표면이 없습니다. 단, 이 작업은 "새 기능 추가"(괄호·혼합 대소문자)이므로 순수 리팩터링과 구분해 별도 커밋으로 분리하는 편이 좋습니다.

---

## 2. P0 — 죽은 코드 제거

### 2.1 `ParameterEmitter.EmitBuildSqlMethod` — 43줄, 호출 지점 0개

```
$ grep -rn 'EmitBuildSqlMethod' src   →  0건 (자기 선언 제외)
```

`ProxyEmitter`는 `EmitBuildSqlStaticMethod`(30줄)만 호출합니다. 43줄짜리 구 경로가 **v2.7.0의 "인라인 SQL 빌드" 전환(2026-03-09) 이전 잔재**로 보입니다. 헤더 주석에는 "BuildSql_XXX 인라인 방출" 전환이 기록돼 있는데, 전환 후 구 경로가 남겨진 것으로 추정됩니다.

**조치**: 메서드와 그 메서드만 사용하는 private 헬퍼를 함께 제거. `ParameterEmitter.cs` 725줄 → 약 680줄.

**확인 필요**: 제거 전 `EmitBuildSqlMethod` 전용 private 헬퍼가 있는지 확인해 함께 정리합니다. 공개 API가 아니므로 `PublicAPI.txt` 변경은 없습니다.

### 2.2 `StringBuilderCache` — 42줄, 참조 0건

```
$ grep -rn 'StringBuilderCache' src   →  자기 파일 외 0건
```

`.NET Runtime` 내부 패턴을 모방해 만든 thread-local 캐시지만 **어디서도 쓰이지 않습니다.** 비교적 `ListPool`(= `DbParameterListPool`, 2건), `InterceptorContextPool`(3건), `PropertyReflectionCache`(12건)는 실제로 쓰이므로 삭제 대상이 아닙니다.

**조치**: `src/NuVatis.Core/Internal/StringBuilderCache.cs` 삭제. `internal` 클래스이므로 외부 영향 없음.

> 참고: 삭제 대신 **실제 적용**하는 선택지도 있습니다. `ParameterBinder`나 `SqlSession.BuildSql`의 `StringBuilder` 생성 지점에 넣으면 할당을 줄일 수 있으나, 이는 성능 특성 변경이므로 별도 벤치마크가 필요합니다. 이 플랜에서는 삭제를 권합니다.

### 2.3 참고 — 정리 대상이 아닌 것

프로브 스크립트가 함께 뽑아냈지만 **제거하면 안 되는** 항목입니다. 판단 근거를 함께 기록해 오해를 막습니다.

| 항목 | 이유 |
|---|---|
| `NuVatisIncrementalGenerator.Initialize()` | `ISourceGenerator` 인터페이스 진입점. 호출자가 Roslyn |
| `QueryCapture.Of/HasQuery/QueryCount/LastQuery` | 테스트 헬퍼의 public API. 외부 사용 |
| `DbProviderRegistry.IsRegistered()` | public API |
| `NuVatisEfCoreExtensions.OpenNuVatisSessionAsync()` | public API |
| `FieldNode.CountL()/Avg()` | DSL public API (레코드 포지셔널 멤버) |
| `TypeResolver.IsNullableType()` | **정리 후보.** `public`이나 내부에서 미사용. PublicAPI에 없으면 제거 가능 — 별도 확인 필요 |

---

## 3. P1 — Source Generator 내부 구조

### 3.1 병렬 AST 워커 2벌

`ParameterEmitter.cs`에는 **같은 SQL AST를 순회하는 워커가 2벌** 있습니다.

| 경로 | 메서드 | 라인 | 사용자 |
|---|---|---|---|
| 정적 메서드 방출 | `EmitNode` + `EmitForEachChildNode` | 111~420 | `ProxyEmitter` |
| 람다 방출 | `EmitLambdaNode` + `EmitLambdaForEachChildNode` | 452~690 | `RegistryEmitter` |

두 워커는 태그별 분기(`if`/`where`/`set`/`foreach`/`choose`)를 각각 독립적으로 구현합니다. **새 태그를 추가할 때 두 곳을 함께 고쳐야 하며, 한쪽만 고치면 출력 불일치가 조용히 발생**합니다.

**개선안**: 두 워커의 공통 부분(태그 판별, 자식 컬렉션 순회 규칙)을 하나의 **순회 스텝 인터페이스**로 추상화합니다.

```
ITreeWalkStep {
    void VisitText(TextNode, ctx);
    void VisitParameter(ParameterNode, ctx);
    void VisitIf(IfNode, ctx);
    void VisitWhere(WhereNode, ctx);
    void VisitForEach(ForEachNode, ctx);
    ...
}
```

그러면 정적 방출과 람다 방출은 **각각의 스텝 구현**이 되어, 태그 추가 시 스텝 구현체만 늘리면 됩니다.

**기능 훼손 위험: 낮음~중간.** 생성되는 SQL 문자열이 한 글자라도 달라지면 동작이 바뀝니다. 완화 조건:

1. `Generators.Tests` 145개가 수정 없이 통과해야 한다
2. `GeneratorIntegrationTests`의 **생성 코드 문자열 스냅샷**이 1글자도 변하지 않아야 한다
3. 리팩터링 PR에서 `git diff`가 `ParameterEmitter.cs` 외에는 없어야 한다

**판단**: 이 작업은 226줄 규모를 다루고 회귀 위험이 출력 문자열에 직결됩니다. **기존 테스트 스냅샷이 충분한 보호망인지 먼저 확인**하고, 없다면 스냅샷을 먼저 강화한 뒤 진행하세요. 순서상 2단계(죽은 코드 제거)를 먼저 끝내는 편이 낫습니다.

---

## 4. P2 — `SqlSession` 책임 분리

### 4.1 현재 상태 (측정)

```
파일 873줄 | 메서드 57개 | 최대 메서드 41줄 | 20줄 이상 15개 | 30줄 이상 3개
sync/async 쌍 13개
```

**방법론적 평가**: 메서드 크기 분포는 양호합니다. God Object의 전형(수백 줄짜리 단일 메서드)은 없습니다. 문제는 크기가 아니라 **책임의 개수**입니다.

현재 `SqlSession`이 동시에 처리하는 책임:

| 책임 | 근거 |
|---|---|
| 쿼리 실행 (12개 public Select/Execute) | L58~530 |
| 쓰기 + SelectKey 오케스트레이션 | `ExecuteWrite`/`Async`, `ApplySelectKey`/`Async` |
| 배치 모드 분기 | `_batchExecutor` null 검사 (ExecuteWrite 3곳) |
| 2차 캐시 조회/무효화 | `TryGetCached`, `PutCache`, `FlushNamespaceCache` |
| 인터셉터 컨텍스트 생성/반환 | `CreateInterceptorContext`, `ReturnContext` |
| SQL 빌드 + 파라미터 풀 반환 | `BuildSql`, `ReturnParameters` |
| 스테이트먼트 해석 | `ResolveStatement` |
| 재진입/수명주기 가드 | `EnsureNotDisposed`, `EnsureNotBusy`, `ReleaseBusy` |
| 트랜잭션 | `Commit`/`Rollback`/`ExecuteInTransactionAsync` |

### 4.2 개선안 — 내부 collaborators로 추출

`ISqlSession` **인터페이스는 그대로 두고**, 구현 내부에서만 책임을 분리합니다.

```
SqlSession (public façade — ISqlSession 구현, 상태·수명주기만 보유)
  ├── StatementResolver      ← ResolveStatement, MakeSqlStmt
  ├── SqlBuildContextFactory ← BuildSql, CreateInterceptorContext, ReturnContext/Parameters
  ├── SecondLevelCacheAccess ← TryGetCached, PutCache, FlushNamespaceCache
  ├── WriteExecutor          ← ExecuteWrite/Async + SelectKey 오케스트레이션
  └── BusyGuard              ← EnsureNotBusy / ReleaseBusy
```

각 collaborator는 `SqlSession`의 private 멤버에 접근할 수 있도록 필요한 것만 넘겨받습니다.

### 4.3 왜 보수적으로 접근해야 하는가

`SqlSession`은 이 ORM의 **가장 뜨거운 경로**입니다. 실행마다 호출되며, 캐시·인터셉터·트랜잭션·배치가 모두 엮여 있습니다. 동시에 다음 제약이 있습니다:

- `_isBusy` 재진입 가드가 `try/finally`로 모든 공개 메서드에散布돼 있음 — 이동 시 빠뜨리면 동시 호출 시 재진입 버그
- `InterceptorContext`는 풀에서 가져와 반환하는 **try/finally 수명 규약**이 있음
- 성능 임팩트 없음이 보장되어야 함 (객체 할당 증가 금지)

**기능 훼손 위험: 중간.** 이 플랜에서 가장 위험한 항목입니다.

**권장 접근**:

1. **1개씩** 추출하고 각각 별도 PR로 진행 (동시 추출 금지)
2. 각 단계마다 `NuVatis.Tests` 405개 + `SqliteE2E` 56개 통과 확인
3. `BenchmarkDotNet` 결과로 성능 회귀 없음 확인 (CI 벤치마크는 회귀 게이트가 없으므로 로컬 비교 필요 — 4.3 참조)
4. `PublicAPI.Shipped.txt` 변경이 없어야 함 (모두 internal/private 추출이므로 성립해야 함)
5. 첫 추출은 부가가장 낮은 `BusyGuard` 또는 `StatementResolver`부터

**완료 판단 기준**: `SqlSession.cs`가 500줄 이하가 되더라도 **그 자체가 목표가 아닙니다.** 목표는 테스트가 그대로 통과하고 동작이 동일한 상태입니다. 줄 수 감축은 부수적 결과입니다.

---

## 5. 축소하지 않기를 권장하는 것

### 5.1 sync/async 중복 48쌍

```
SqlSession 13 | InMemorySqlSession 9 | BatchExecutor 6 | SimpleExecutor 5
AdoTransaction 4 | Interceptor/OTel/ResultSetGroup 각 2 | EFCore 1
```

`ExecuteWrite`(36줄)와 `ExecuteWriteAsync`(41줄)을 비교하면 **30줄이 그대로 복제**돼 있고, 차이는 5개뿐입니다.

```
await × 4, RunBefore→RunBeforeAsync, ApplySelectKey→ApplySelectKeyAsync,
ExecuteTimed→ExecuteTimedAsync, .ConfigureAwait(false)
```

전형적인 해결책은 **async만 구현하고 sync는 `GetAwaiter().GetResult()` 래퍼로 두는 것**입니다. 하지만 이 프로젝트는 현재 다음을 **의도적으로 유지**하고 있습니다.

```
$ grep -rnE '\.Result\b|\.Wait\(\)|GetAwaiter\(\)\.GetResult\(\)' src   →  0건
```

sync-over-async 0건은 실수로가 아니라 설계 목표입니다. AOT/고성능 라이브러리에서 동기 블로킹 호출은 교착 위험과 스레드 풀 고갈을 만듭니다.

**따라서 축소를 권장하지 않습니다.** 중복은 의도된 비용이며, 이를 없애는 대가는 명확한 리스크입니다.

대신 저비용으로 누출을 줄일 수 있습니다:

- 두 메서드 위에 **"`X`와 `XAsync`는 구조적으로 동일하며, 한쪽 수정 시 다른 쪽도 반드시 함께 수정"** 이라는 주석을 명시
- 또는 `ExecuteWrite`/`Async`를 **순수한 "단계 나열" 구조**로 만들어 두 버전의 본문 차이가 `await`뿐이도록 시각적으로 드러나게 함

둘 다 동작 변경이 없습니다.

### 5.2 `TestExpressionEvaluator`의 분할 위치 재검토

P0-1.4의 파서 교체가 **이 절의 본질**입니다. 분할 위치만 바꾸면 버그가 고쳐지지 않습니다. 요구사항은 "괄호·리터럴·대소문자"이며, 이는 MyBatis 호환성 문제입니다.

---

## 6. 실행 순서

```
[1] 죽은 코드 제거 ──────────────────────── 위험: 매우 낮음
     · ParameterEmitter.EmitBuildSqlMethod (43줄)
     · StringBuilderCache.cs (42줄)
     · TypeResolver.IsNullableType() (PublicAPI 미등록 확인 후)
     검증: 전체 666개 통과, Generators 생성 스냅샷 불변
                    ↓
[2] TestExpressionEvaluator 파서 교체 ──── 위험: 낮음 (기능 추가)
     · 토큰화 + 재귀 하강
     · 기존 37개 테스트 무수정 통과가 계약
     검증: 기존 37개 + 신규 경계 사례 5개
                    ↓
[3] SG 순회 스텝 추상화 ─────────────────── 위험: 낮음~중간
     · 선행: 생성 코드 스냅샷 테스트 강화 여부 확인
                    ↓
[4] SqlSession collaborator 추출 ───────── 위험: 중간 (1 PR = 1 책임)
     · Benchable 기준선 확보 후 시작
     · BusyGuard → StatementResolver → CacheAccess → WriteExecutor 순
```

**1~2번은 오늘 하루 안에 끝나고 기능 개선 가치가 명확합니다.** 3~4번은 규모와 리스크가 올라가므로 각각 별도 리뷰 주기를 두는 편이 좋습니다.

---

## 7. 전체 검증 절차 (매 단계 공통)

```bash
# 기준선 (현재 상태 — 2026-10-04 실측)
#   Core 유닛 405 / Core SqliteE2E 56 / Testcontainers 9 / E2E-PG 7
#   Generators 145 / QB unit 76 / QB integration 16 / QB.Tools 15
#   합계 666개, 실패 0

# 1) 빌드 무결성 — 오류 0건, 새 경고 0건
dotnet build NuVatis.sln -c Release -p:FastTest=true --no-incremental

# 2) 전체 테스트
dotnet test tests/NuVatis.Tests/ -c Release -p:FastTest=true \
  --filter "Category!=E2E&Category!=Testcontainers"
dotnet test tests/NuVatis.Generators.Tests/ -c Release -p:FastTest=true
dotnet test tests/NuVatis.QueryBuilder.Tests/ -c Release -p:FastTest=true
dotnet test tests/NuVatis.QueryBuilder.Tools.Tests/ -c Release -p:FastTest=true

# 3) public API 계약 불변 (구조 작업이므로 반드시 불변이어야 함)
git diff --exit-code -- 'src/**/PublicAPI.*.txt'

# 4) 단계별 회귀 확인
#   · Generators: 생성 코드 스냅샷 diff 0
#   · SqlSession: SqliteE2E 56개 통과
```

`PublicAPI.txt`가 변경되면 그 순간 **구조 리팩터링이 아니라 public API 변경**입니다. `PublicApiAnalyzers`가 `WarningsAsErrors`로 승격돼 빌드가 실패하므로, 통과 여부로 판정할 수 있습니다.

---

## 8. 하지 않는 것

- **net6.0 / net7.0 타겟 제거** — 제품 결정이며 EOL 사실은 이미 README에 기록했습니다. 기술 판단으로 하지 않습니다
- **sync/async 중복 축소** — 5.1 참조
- **`SqlSession`을 인터페이스 분리** — `ISqlSession`은 이미 분리돼 있습니다. public API 변경이 필요하므로 하지 않습니다
- **DI 컨테이너 도입** — 소형 라이브러리에서 Core에 의존성을 추가하는 것은 설계 철학(ADO.NET 최소 추상화)과 충돌합니다
- **테스트 커버리지 100% 추구** — 5단계 측정이 남았지만(structure 단계와 별개), 커버리지 수치 자체가 목표가 아닙니다

---

## 9. 총평

이 프로젝트의 구조는 **전반적으로 건강합니다.** 계층 분리가 명확하고, 3계층 의존 그래프에 순환이 없으며, 파일 크기가 균형 잡혀 있습니다. 특이하게도 죽은 코드가 거의 없는데(2건 발견), 이는 보통의 신생 프로젝트에서 드문 편입니다.

발견된 3건의 성격이 서로 다릅니다.

**`TestExpressionEvaluator` 버그는 가장 주의가 필요합니다.** MyBatis 호환을 전면에 내세우는 프로젝트의 public 계약이 문서화된 기대치를 충족하지 못합니다. 내부에서 쓰이지 않기 때문에 지금 깨지지 않지만, **소비자가 처음 호출하는 순간 발견되는 종류의 버그**입니다. 마이그레이션 문서를 읽고 넘어온 사용자가 정확히 그렇게 됩니다.

**죽은 코드 2건은 부수적이나 즉시 이득입니다.** 특히 `EmitBuildSqlMethod` 43줄은 전환 작업의 잔재로, 제거하면 이후 작업자가 "이 경로가 어디 쓰이는 걸까" 헤매지 않게 합니다.

**`SqlSession` 분리는 이득이 가장 불확실한 작업입니다.** 측정 결과 메서드 크기는 양호해서 God Object 문제는 아닙니다. 9개 책임을 추출하는 작업은 2주일을 소모하면서 동작 위험을 수반하고, 얻는 것은 파일 크기 감소입니다. **익숙해진 코드가 방해하지 않는다면 하지 않는 편이 나을 수 있습니다.** 1~2단계가 실측 가치가 명확하니, 그걸로 충분하다고 봅니다.

---

*본 플랜의 모든 수치는 실측 기준입니다. `TestExpressionEvaluator`의 4개 버그는 임시 프로브 테스트로 재현 확인 후 즉시 삭제했으며, 코드 변경은 이 플랜 작성 시점까지 0건입니다.*


---

## 부록: 구현 결과 (2026-10-04)

### 수행 완료

| 단계 | 결과 |
|---|---|
| 1단계 죽은 코드 | **부분 수행.** `StringBuilderCache` 42줄 삭제. `EmitBuildSqlMethod` 계열 384줄은 public API 게이트에 막혀 `[Obsolete]` 표시로 전환 |
| 2단계 파서 교체 | **완료.** 버그 4건 수정, 기존 37개 무수정 통과 + differential test로 호환성 증명 |
| 3단계 스텝 추상화 | **판단 유보.** 아래 근거 |
| 4단계 SqlSession 분리 | **판단 유보.** 아래 근거 |

### 3단계를 하지 않은 이유

두 살아있는 워커의 공유 범위를 계량했다.

```
DynamicSqlEmitter.EmitNode      112줄, 케이스 10개
ParameterEmitter.EmitLambdaNode 143줄, 케이스 9개
공통 케이스 8개, ParameterEmitter만 0개, DynamicSql만 2개
```

겉보기엔 병렬 구조지만 공유되는 것은 `switch` 골격뿐이다. 방출 본문은 완전히 다르다.

- `DynamicSqlEmitter` → `test` 표현식을 C# 조건문으로 변환 (`__sql.Append`, `if (cond)`)
- `ParameterEmitter.EmitLambdaNode` → SQL 문자열 + DbParameter 바인딩 (`__sb_`, `__params_`, `__idx_`)

재귀 문맥도 다르다(`paramType` vs `prefix`). 스텝 인터페이스를 도입하면 공유되는 `switch`만 절약하는 대신 인터페이스 1개, 메서드 시그니처 8~10개 × 구현 2벌, 그리고 두 문맥의 합집합을 담는 컨텍스트 객체가 추가된다. 유지 비용이 줄어들지 않고 ** indirection만 늘어날 가능성이 높다.**

또한 `paramTypeMap`(SqlIdentifier 런타임 가드 주입)이 ParameterEmitter에만 존재해 **두 워커의 안전성 의미가 다르다.** 통합 과정에서 그 가드가 약화될 위험이 있다.

### 4단계를 하지 않은 이유

`SqlSession`(873줄)에서 저위험하게 추출 가능한 부분의 크기를 측정했다.

| 후보 | 라인 | 파일 대비 |
|---|---|---|
| `ResolveStatement` | 8줄 | 0.9% |
| `BuildSql` + `ReturnParameters` (static) | 10줄 | 1.1% |
| **합계** | **18줄** | **2.1%** |

무게가 있는 부분(`ExecuteWrite` 36+41줄, 캐시, 인터셉터 컨텍스트)은 `try/finally` 기반 재진입 가드와 풀 반환 규칙이 엮여 있어 위험도가 가장 높다. 즉 **가장 안전한 작업은 이득이 2%이고, 이득이 큰 작업은 위험하다.** 추적 비용 대비 값이 나오지 않는다.

### 구현 중 발견한 추가 문제

1. **생성 코드 회귀 스냅샷 부재** → 골든 스냅샷 테스트로 해결. 이후 `[Obsolete]` 적용 전후로 해시가 동일함을 확인하며 "384줄 경로가 진짜 미사용"임을 실측 증명했다.
2. **`${}` 컴파일 타임 최적화가 제품 경로에서 사라짐** → `paramTypeMap` 기반 가드 생략 로직이 폐기 예정 경로에만 남아 있다. 제품 경로는 항상 런타임 가드를 방출한다. 안전성은 더 엄격해졌지만 ${} 치환마다 런타임 타입 검사 1회가 추가된다. 배선 여부는 **설계 변경**이라 별도 판단이 필요하다.
3. **SQL Injection 방어의 집중 검증이 죽은 코드에만 존재** → `ParameterEmitterLambdaPathTests` 7개로 제품 경로 실제 동작을 고정했다.

### 최종 실측

```
Core: CI 유닛        422 통과
Core: Testcontainers   9 통과
Generators            154 통과  (145 → 154)
QB unit               76 통과
QB integration        16 통과
QB.Tools              15 통과
─────────────────────────────
합계 692개, 실패 0

빌드 오류 0건 · 경고 4건(환경 한정 CS9057) · PublicAPI 변경 0건 · YAML 5개 유효
```
