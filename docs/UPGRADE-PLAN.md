# NuVatis 고도화 작업 플랜

작성자: 최진호
작성일: 2026-02-26
버전: v0.1.0-alpha.1 -> v1.0.0-GA

---

## 목차

1. [현황 분석](#1-현황-분석)
2. [Phase 1: v0.2.0-beta — 안정화 및 인프라](#2-phase-1-v020-beta)
3. [Phase 2: v0.5.0-rc — 핵심 기능 고도화](#3-phase-2-v050-rc)
4. [Phase 3: v0.8.0-rc — 성능 및 생태계](#4-phase-3-v080-rc)
5. [Phase 4: v1.0.0-GA — 안정화 및 릴리스](#5-phase-4-v100-ga)
6. [일정 요약](#6-일정-요약)
7. [위험 요소 및 완화 전략](#7-위험-요소-및-완화-전략)

---

## 1. 현황 분석

### 1.1 프로젝트 메트릭

| 항목 | 수치 |
|------|------|
| 소스 코드 | ~5,335줄 / 47파일 |
| 테스트 코드 | ~5,137줄 / 26파일 / 214개 테스트 |
| NuGet 패키지 | 9개 |
| 지원 .NET | 7.0 / 8.0 멀티 타겟 |
| 지원 DB | PostgreSQL, MySQL, SQL Server |
| CI 매트릭스 | 2 OS x 2 .NET x 3 DB |

### 1.2 아키텍처 현황

```
ISqlSession (인터페이스)
  └── SqlSession (구현)
        ├── IExecutor
        │     ├── SimpleExecutor (사용 중)
        │     └── BatchExecutor (미연결)
        ├── InterceptorPipeline
        │     ├── LoggingInterceptor
        │     ├── MetricsInterceptor
        │     └── OpenTelemetryInterceptor
        ├── ParameterBinder (#{} -> @p0)
        ├── ColumnMapper (런타임 리플렉션 매핑)
        └── ResultMapper (런타임 리플렉션 매핑)

SqlSessionFactory (Singleton)
  ├── OpenSession() -> SimpleExecutor
  ├── OpenReadOnlySession()
  └── FromExistingConnection()

NuVatisIncrementalGenerator (SG)
  ├── XmlMapperParser -> ParsedMapper
  ├── InterfaceAnalyzer -> MapperInterfaceInfo
  ├── IncludeResolver
  ├── StringSubstitutionAnalyzer (NV004)
  ├── ProxyEmitter -> *Impl.g.cs
  ├── MappingEmitter -> Map_*() 메서드
  ├── RegistryEmitter -> NuVatisMapperRegistry.g.cs
  └── ParameterEmitter
```

### 1.3 식별된 기술 부채

| ID | 항목 | 심각도 | 위치 |
|----|------|--------|------|
| TD-01 | BatchExecutor가 SqlSessionFactory에서 미연결 | 중 | SqlSessionFactory.cs |
| TD-02 | ColumnMapper.MapComplex가 런타임 리플렉션 사용 (AOT 제한) | 상 | ColumnMapper.cs:79-107 |
| TD-03 | ResultMapper 전체가 런타임 리플렉션 사용 (AOT 제한) | 상 | ResultMapper.cs |
| TD-04 | TestExpressionEvaluator가 런타임 리플렉션 사용 (AOT 제한) | 중 | TestExpressionEvaluator.cs |
| TD-05 | MappingEmitter가 GetFieldValue<object>로 타입 불일치 가능 | 중 | MappingEmitter.cs:27 |
| TD-06 | BatchExecutor.Flush가 순차 ExecuteNonQuery (배칭 미구현) | 중 | BatchExecutor.cs:29-55 |
| TD-07 | CI 커버리지 리포트 미설정 | 하 | ci.yml |
| TD-08 | DocFX 빌드/호스팅 미완 | 하 | docs/ |
| TD-09 | Dependabot/Renovate 미설정 | 하 | .github/ |

---

## 2. Phase 1: v0.2.0-beta

목표: 인프라 안정화, BatchExecutor 연결, DX 개선
기간: 4주
우선순위: 상

### Task 1.1: BatchExecutor를 SqlSessionFactory에 연결

의존성: 없음
예상: 3일
대상 파일:
- `src/NuVatis.Core/Session/ISqlSessionFactory.cs`
- `src/NuVatis.Core/Session/SqlSessionFactory.cs`
- `src/NuVatis.Core/Session/ISqlSession.cs`
- `src/NuVatis.Core/Session/SqlSession.cs`

작업 내용:
1. ISqlSessionFactory에 `OpenBatchSession()` 메서드 추가
2. SqlSessionFactory에서 BatchExecutor를 생성하여 세션에 주입
3. ISqlSession에 `FlushStatements()` / `FlushStatementsAsync()` 추가
4. SqlSession에서 BatchExecutor 모드일 때 Write 쿼리를 배치에 누적

설계:
```
ISqlSessionFactory
  ├── OpenSession(autoCommit)         // 기존
  ├── OpenReadOnlySession()           // 기존
  ├── OpenBatchSession()              // 신규: BatchExecutor 사용
  └── FromExistingConnection(...)     // 기존

ISqlSession
  ├── Insert/Update/Delete            // BatchMode면 Add만
  ├── FlushStatements()               // 신규: 배치 일괄 실행
  └── FlushStatementsAsync()          // 신규
```

수락 기준:
- [ ] OpenBatchSession()으로 생성된 세션에서 Insert 3건 -> FlushStatements() -> DB에 3건 반영
- [ ] Flush 전 Dispose 시 자동 Rollback
- [ ] 기존 OpenSession() 동작에 영향 없음
- [ ] 단위 테스트 5개 이상

### Task 1.2: BatchExecutor에 DbBatch(Npgsql) 최적화 경로 추가

의존성: Task 1.1
예상: 3일
대상 파일:
- `src/NuVatis.Core/Executor/BatchExecutor.cs`
- `src/NuVatis.Core/Provider/IDbProvider.cs`

작업 내용:
1. IDbProvider에 `bool SupportsBatching { get; }` 속성 추가
2. BatchExecutor.FlushAsync에서 DbBatch 지원 여부에 따라 분기
3. Npgsql DbBatch 경로: 단일 라운드트립으로 다중 명령 전송
4. 비지원 Provider: 기존 순차 실행 유지 (폴백)
5. Flush 크기 제어: `maxBatchSize` 파라미터 (기본 500)

현재 BatchExecutor.FlushAsync (변경 전):
```csharp
// 순차 실행 - 네트워크 라운드트립 N회
foreach (var item in _batch) {
    await using var command = CreateCommand(connection, dbTransaction, item);
    totalAffected += await command.ExecuteNonQueryAsync(ct);
}
```

변경 후 설계:
```csharp
if (connection.CanCreateBatch) {
    // DbBatch 경로 - 라운드트립 1회
    await using var batch = connection.CreateBatch();
    batch.Transaction = dbTransaction;
    foreach (var item in _batch) {
        var cmd = batch.CreateBatchCommand();
        cmd.CommandText = item.Sql;
        CopyParameters(item.Parameters, cmd.Parameters);
    }
    totalAffected = await batch.ExecuteNonQueryAsync(ct);
} else {
    // 폴백: 기존 순차 실행
}
```

수락 기준:
- [ ] PostgreSQL E2E 테스트에서 DbBatch 경로 동작 확인
- [ ] MySQL/SqlServer에서 폴백 경로 동작 확인
- [ ] 1000건 Insert 벤치마크에서 순차 대비 성능 향상 측정

### Task 1.3: SG 진단 코드 추가 (NV005, NV006)

의존성: 없음
예상: 2일
대상 파일:
- `src/NuVatis.Generators/Diagnostics/DiagnosticDescriptors.cs`
- `src/NuVatis.Generators/NuVatisIncrementalGenerator.cs`
- `src/NuVatis.Generators/Analysis/InterfaceAnalyzer.cs`

작업 내용:
1. NV005: XML Statement ID와 인터페이스 메서드명 불일치 경고
   - XmlMapper에 `<select id="GetById">` 존재, 인터페이스에 `GetById` 메서드 없음
2. NV006: ResultMap 컬럼과 대상 타입 프로퍼티 불일치 Info
   - ResultMap에 `column="user_name" property="UserName"` 정의, 대상 타입에 `UserName` 프로퍼티 없음

수락 기준:
- [ ] 불일치 시 빌드 경고/정보 출력
- [ ] 정상 매핑 시 경고 없음
- [ ] Generator 단위 테스트 추가

### Task 1.4: CI 커버리지 리포트 (Codecov)

의존성: 없음
예상: 1일
대상 파일:
- `.github/workflows/ci.yml`

작업 내용:
1. coverlet으로 커버리지 수집 (`--collect:"XPlat Code Coverage"`)
2. Codecov Action으로 리포트 업로드
3. README.md에 커버리지 뱃지 추가
4. 커버리지 목표: 80% (Phase 3에서 90%로 상향)

수락 기준:
- [ ] CI 실행 시 Codecov에 리포트 업로드
- [ ] README에 커버리지 뱃지 표시

### Task 1.5: Dependabot 구성

의존성: 없음
예상: 0.5일
대상 파일:
- `.github/dependabot.yml` (신규)

작업 내용:
1. NuGet 패키지 주간 업데이트 체크
2. GitHub Actions 월간 업데이트 체크
3. 보안 취약점 자동 PR 생성

수락 기준:
- [ ] dependabot.yml 배포 후 PR 자동 생성 확인

### Task 1.6: DocFX 빌드 및 GitHub Pages 배포

의존성: 없음
예상: 2일
대상 파일:
- `.github/workflows/docs.yml` (신규)
- `docs/docfx.json`
- `docs/toc.yml`

작업 내용:
1. DocFX 빌드 워크플로우 (main push 트리거)
2. GitHub Pages 배포 (gh-pages 브랜치)
3. API 자동 문서 생성 (소스 XML 주석 기반)
4. Getting Started, Cookbook, Architecture 문서 검수

수락 기준:
- [ ] main push 시 DocFX 사이트 자동 배포
- [ ] API 참조 문서 자동 생성
- [ ] 기존 마크다운 문서 정상 렌더링

### Task 1.7: MappingEmitter 타입 안전성 개선

의존성: 없음
예상: 2일
대상 파일:
- `src/NuVatis.Generators/Emitters/MappingEmitter.cs`
- `src/NuVatis.Generators/Analysis/TypeResolver.cs`

작업 내용:
현재 MappingEmitter가 `GetFieldValue<object>`로 모든 값을 가져오는 문제 해결:
```csharp
// 현재 (타입 불일치 가능)
obj.UserName = reader.GetFieldValue<object>(ordinal_user_name);

// 개선 후 (타입 명시)
obj.UserName = reader.GetString(ordinal_user_name);
obj.Age = reader.GetInt32(ordinal_age);
```

1. TypeResolver에서 프로퍼티 타입 -> DbDataReader 접근 메서드 매핑
2. MappingEmitter가 프로퍼티 타입에 따라 정확한 reader 메서드 생성
3. Nullable 타입 처리 (`reader.IsDBNull` 체크 후 할당)

수락 기준:
- [ ] 생성 코드에 타입별 reader 메서드 사용
- [ ] int, string, DateTime, Guid, bool, decimal, Nullable<T>, Enum 커버
- [ ] Generator 테스트 추가

---

## 3. Phase 2: v0.5.0-rc

목표: 리플렉션 제거, 보안 강화, TypeHandler
기간: 8주
우선순위: 상

### Task 2.1: TestExpressionEvaluator SG 전환

의존성: 없음
예상: 10일
대상 파일:
- `src/NuVatis.Generators/Emitters/ProxyEmitter.cs`
- `src/NuVatis.Generators/Parsing/XmlMapperParser.cs`
- `src/NuVatis.Generators/Emitters/DynamicSqlEmitter.cs` (신규)
- `src/NuVatis.Core/DynamicSql/TestExpressionEvaluator.cs` (폴백 유지)

작업 내용:

현재 런타임 동작:
```
XML: <if test="name != null and age > 18">
     -> TestExpressionEvaluator.Evaluate("name != null and age > 18", parameter)
     -> 리플렉션으로 parameter.Name, parameter.Age 접근
```

SG 전환 후:
```
빌드타임: XmlMapperParser가 test 표현식 파싱
     -> DynamicSqlEmitter가 C# 코드 생성
     -> static Func<TParam, bool> 필드로 방출

런타임: 생성된 Func 직접 호출 (리플렉션 제로)
```

DynamicSqlEmitter 설계:
1. MyBatis 표현식 -> C# 코드 변환 규칙:
   - `name != null` -> `p.Name != null`
   - `age > 18` -> `p.Age > 18`
   - `and` -> `&&`, `or` -> `||`
   - `list.size > 0` -> `p.List?.Count > 0`
   - `type == 'admin'` -> `p.Type == "admin"`
2. netstandard2.0 제약: 정규식 기반 경량 변환기 (Expression 트리 불가)
3. 파라미터 타입 결정: InterfaceAnalyzer에서 메서드 파라미터 타입 추출
4. 런타임 폴백: SG 미적용 사용자는 기존 TestExpressionEvaluator 유지

생성 코드 예시:
```csharp
internal sealed class UserMapperImpl : IUserMapper {
    private static readonly Func<UserSearchParam, bool> _test_Search_if_0 =
        p => p.UserName != null;
    private static readonly Func<UserSearchParam, bool> _test_Search_if_1 =
        p => p.Ids?.Count > 0;

    public IList<User> Search(UserSearchParam param) {
        // SG가 동적 SQL을 인라인 빌드
        var sqlBuilder = new StringBuilder("SELECT id, user_name, email FROM users WHERE 1=1");
        if (_test_Search_if_0(param)) {
            sqlBuilder.Append(" AND user_name LIKE @p0");
        }
        // ...
        return _session.SelectList<User>("IUserMapper.Search", param);
    }
}
```

수락 기준:
- [ ] SG 생성 코드에서 TestExpressionEvaluator 호출 제거
- [ ] if, choose/when/otherwise, where, set, foreach 모든 태그 커버
- [ ] 리플렉션 미사용 확인 (ILLink 경고 제로)
- [ ] 기존 테스트 214개 전부 통과
- [ ] SG 미적용 시 런타임 폴백 동작 확인

### Task 2.2: ColumnMapper/ResultMapper SG 매핑 코드 생성

의존성: Task 1.7 (MappingEmitter 개선)
예상: 7일
대상 파일:
- `src/NuVatis.Generators/Emitters/MappingEmitter.cs`
- `src/NuVatis.Generators/Emitters/ProxyEmitter.cs`
- `src/NuVatis.Core/Mapping/ColumnMapper.cs` (폴백 유지)
- `src/NuVatis.Core/Mapping/ResultMapper.cs` (폴백 유지)

작업 내용:

현재 런타임 매핑 (ColumnMapper.MapComplex):
```csharp
var obj = Activator.CreateInstance<T>();          // 리플렉션
var props = type.GetProperties(...);              // 리플렉션
prop.SetValue(obj, Convert.ChangeType(value));    // 리플렉션
```

SG 생성 매핑:
```csharp
// MappingEmitter가 생성하는 정적 매핑 메서드
static User Map_UserResult(DbDataReader reader) {
    var obj = new User();
    var ordinal_id        = reader.GetOrdinal("id");
    var ordinal_user_name = reader.GetOrdinal("user_name");
    var ordinal_email     = reader.GetOrdinal("email");

    if (!reader.IsDBNull(ordinal_id))
        obj.Id = reader.GetInt32(ordinal_id);
    if (!reader.IsDBNull(ordinal_user_name))
        obj.UserName = reader.GetString(ordinal_user_name);
    if (!reader.IsDBNull(ordinal_email))
        obj.Email = reader.GetString(ordinal_email);

    return obj;
}
```

1. ProxyEmitter가 ResultMap 정의가 있으면 MappingEmitter 생성 메서드 사용
2. ResultMap 정의가 없으면 ColumnMapper (런타임 자동매핑) 폴백
3. Association/Collection 매핑도 SG로 생성

수락 기준:
- [ ] ResultMap이 정의된 쿼리에서 리플렉션 제로
- [ ] 생성된 매핑 코드에 타입별 reader 메서드 사용
- [ ] Association(1:1), Collection(1:N) 매핑 SG 생성
- [ ] ILLink 트리밍 경고 제로 (.NET 8)
- [ ] 기존 ResultMapper 테스트 전부 통과

### Task 2.3: [SqlConstant] 어트리뷰트 및 NV004 고도화

의존성: 없음
예상: 4일
대상 파일:
- `src/NuVatis.Core/Attributes/SqlConstantAttribute.cs` (신규)
- `src/NuVatis.Generators/Diagnostics/StringSubstitutionAnalyzer.cs`
- `src/NuVatis.Generators/Diagnostics/DiagnosticDescriptors.cs`

작업 내용:

1. [SqlConstant] 어트리뷰트 정의:
```csharp
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class SqlConstantAttribute : Attribute { }
```

2. StringSubstitutionAnalyzer 확장:
   - ${var}의 var가 [SqlConstant] 상수/리터럴이면 -> 경고 없음
   - ${var}의 var가 동적 문자열이면 -> NV004 Warning 유지
   - 향후 NV007로 Error 격상 옵션 (EditorConfig 설정)

3. 사용 예시:
```csharp
public static class SqlRef {
    [SqlConstant] public const string OrderByName = "user_name";
    [SqlConstant] public const string OrderByDate = "created_at";
}
```

수락 기준:
- [ ] [SqlConstant] 필드가 ${} 에 사용될 때 NV004 억제
- [ ] 미마킹 변수가 ${} 에 사용될 때 NV004 경고 발생
- [ ] SG 테스트 추가

### Task 2.4: TypeHandler 시스템 구현

의존성: 없음
예상: 5일
대상 파일:
- `src/NuVatis.Core/Mapping/ITypeHandler.cs` (기존 인터페이스 활용)
- `src/NuVatis.Core/Mapping/TypeHandlerRegistry.cs` (기존 레지스트리 활용)
- `src/NuVatis.Core/Mapping/TypeHandlers/` (신규 디렉토리)

작업 내용:

1. 내장 TypeHandler 구현:
   - JsonTypeHandler<T>: JSON 열 <-> C# 객체 (System.Text.Json)
   - DateOnlyTypeHandler: DateOnly <-> DB date (.NET 8)
   - TimeOnlyTypeHandler: TimeOnly <-> DB time (.NET 8)
   - EnumStringTypeHandler<T>: Enum <-> DB string

2. XML에서 TypeHandler 지정:
```xml
<result column="metadata" property="Metadata" typeHandler="JsonTypeHandler" />
```

3. 전역 등록:
```csharp
options.TypeHandlers.Register<JsonDocument>(new JsonTypeHandler<JsonDocument>());
```

4. SG 생성 코드에서 TypeHandler 호출 통합

수락 기준:
- [ ] JSON 열 직렬화/역직렬화 동작
- [ ] DateOnly/TimeOnly 매핑 동작 (.NET 8)
- [ ] 커스텀 TypeHandler 등록 및 사용 가능
- [ ] 단위 테스트 추가

### Task 2.5: <bind> 태그 지원

의존성: Task 2.1 (DynamicSqlEmitter)
예상: 2일
대상 파일:
- `src/NuVatis.Generators/Parsing/XmlMapperParser.cs`
- `src/NuVatis.Generators/Models/ParsedSqlNode.cs`

작업 내용:
```xml
<select id="Search">
  <bind name="pattern" value="'%' + name + '%'" />
  SELECT * FROM users WHERE name LIKE #{pattern}
</select>
```

1. XmlMapperParser에 `<bind>` 태그 파싱 추가
2. BindNode 모델 추가
3. DynamicSqlEmitter에서 바인드 변수 생성 코드 방출

수락 기준:
- [ ] <bind> 태그를 통한 변수 바인딩 동작
- [ ] SG 생성 코드에서 바인드 변수 인라인

---

## 4. Phase 3: v0.8.0-rc

목표: 성능 최적화, 생태계 확장, 품질 강화
기간: 4주
우선순위: 중

### Task 3.1: NuVatis.Sqlite 패키지 추가

의존성: 없음
예상: 2일
대상 파일:
- `src/NuVatis.Sqlite/` (신규 프로젝트)

작업 내용:
1. SqliteProvider 구현 (Microsoft.Data.Sqlite)
2. Edge 컴퓨팅 및 테스트 환경 지원
3. CI E2E 테스트에 SQLite 추가

수락 기준:
- [x] SQLite 기반 CRUD 동작
- [x] 기존 테스트 SQLite 백엔드로 실행 가능
- [x] NuGet 패키지 생성 (10개로 증가)

### Task 3.2: Object Pooling 적용

의존성: 없음
예상: 3일
대상 파일:
- `src/NuVatis.Core/Binding/ParameterBinder.cs`
- `src/NuVatis.Core/Executor/SimpleExecutor.cs`

작업 내용:
1. ParameterBinder에서 DbParameter 리스트 ArrayPool 사용
2. StringBuilder 풀링 (StringBuilderCache 패턴)
3. InterceptorContext 오브젝트 풀링

수락 기준:
- [x] 벤치마크에서 GC Gen0 컬렉션 감소 측정
- [x] 기존 테스트 전부 통과
- [x] 메모리 할당 프로파일 비교 데이터

### Task 3.3: 벤치마크 CI 연동

의존성: 없음
예상: 2일
대상 파일:
- `benchmarks/NuVatis.Benchmarks/`
- `.github/workflows/benchmark.yml` (신규)

작업 내용:
1. BenchmarkDotNet 결과를 GitHub Actions artifact로 저장
2. 이전 결과와 비교하여 성능 회귀 감지
3. Dapper, EF Core 대비 벤치마크 포함
4. main 브랜치 push 시 자동 실행

수락 기준:
- [x] 벤치마크 결과 artifact 업로드
- [x] 성능 회귀 시 CI 경고
- [x] SelectOne, SelectList, Insert, BatchInsert 시나리오 커버

### Task 3.4: Testcontainers 기반 다중 버전 E2E

의존성: 없음
예상: 3일
대상 파일:
- `tests/NuVatis.Tests/E2E/`

작업 내용:
1. Testcontainers.NET 패키지 도입
2. PostgreSQL 12/13/14/15/16 버전별 테스트
3. MySQL 5.7/8.0/8.4 버전별 테스트
4. CI에서 자동 실행 (주간 스케줄)

수락 기준:
- [x] 다중 DB 버전에서 E2E 통과
- [x] 주간 CI 스케줄 동작

### Task 3.5: NuGet 패키지 서명 및 SourceLink

의존성: 없음
예상: 1일
대상 파일:
- `Directory.Build.props`
- `.github/workflows/publish.yml`

작업 내용:
1. SourceLink 활성화 (`<EmbedUntrackedSources>`, `<DebugType>embedded`)
2. deterministic 빌드 (`<ContinuousIntegrationBuild>`)
3. snupkg 심볼 패키지 NuGet.org 배포

수락 기준:
- [x] NuGet 패키지에서 소스 코드 디버깅 가능
- [x] Symbol server에 심볼 업로드

### Task 3.6: .NET Aspire 통합 컴포넌트

의존성: Task 1.1, Task 1.2
예상: 5일
대상 파일:
- `src/NuVatis.Extensions.Aspire/` (신규 프로젝트)

작업 내용:
1. Aspire Component 표준 구조:
   - `AddNuVatisPostgreSql(connectionName)` 확장 메서드
   - 자동 Health Check 등록
   - 자동 OpenTelemetry Tracing/Metrics 등록
2. NuVatis.Extensions.DI를 코어로 활용
3. Aspire 컨피규레이션 바인딩 레이어
4. 샘플 AppHost 프로젝트

수락 기준:
- [x] Aspire AppHost에서 NuVatis 리소스 등록
- [x] Aspire 대시보드에서 SQL 트레이스 표시
- [x] Health Check 대시보드 연동

### Task 3.7: 커버리지 목표 90% 달성

의존성: Task 1.4
예상: 5일

작업 내용:
1. 커버리지 리포트 분석 후 미커버 영역 식별
2. 엣지 케이스 테스트 추가:
   - Dynamic SQL 태그 조합 (if + foreach, choose + where 중첩)
   - Nullable 타입 매핑
   - 외부 커넥션 모드 (FromExistingConnection)
   - 동시 접근 감지 (EnsureNotBusy)
   - Dispose 후 사용 시도
3. 에러 경로 테스트:
   - 존재하지 않는 Statement ID
   - 잘못된 XML 매퍼
   - DB 커넥션 실패

수락 기준:
- [x] 전체 라인 커버리지 90% 이상 (91.2% 달성)
- [x] 브랜치 커버리지 80% 이상 (81.4% 달성)

---

## 5. Phase 4: v1.0.0-GA

목표: API 안정화, 최종 검증, 정식 릴리스
기간: 2주
우선순위: 상

### Task 4.1: Public API 검수 및 동결

예상: 2일

작업 내용:
1. 모든 public 인터페이스/클래스 검수
2. PublicApiAnalyzers 도입 (API 변경 감지)
3. API 호환성 정책 문서화
4. Obsolete 대상 없는지 확인

수락 기준:
- [x] Public API 목록 문서화 (docs/api/public-api-reference.md)
- [x] 의도하지 않은 public 노출 수정 (Internal 클래스 검증 완료)

### Task 4.2: 마이그레이션 가이드 작성

예상: 3일
대상 파일:
- `docs/cookbook/migration-from-dapper.md` (신규)
- `docs/cookbook/migration-from-efcore.md` (신규)
- `docs/cookbook/hybrid-efcore-nuvatis.md` (신규)

작업 내용:
1. Dapper -> NuVatis 단계별 전환 가이드
2. EF Core + NuVatis 하이브리드 패턴 (복잡 쿼리만 NuVatis)
3. 트랜잭션 공유 패턴 코드 예시
4. 기존 SQL -> XML Mapper 이관 팁

### Task 4.3: 릴리스 노트 및 최종 테스트

예상: 2일

작업 내용:
1. CHANGELOG.md 0.2.0, 0.5.0, 0.8.0, 1.0.0 버전별 정리
2. 전체 테스트 스위트 최종 실행
3. 3종 DB x 2 .NET 버전 E2E 최종 검증
4. NuGet 패키지 10~11개 생성 및 검증
5. v1.0.0 태그 push -> Trusted Publishing 배포

수락 기준:
- [x] 모든 테스트 통과 (311 + 68 = 379개, 실패 0)
- [x] NuGet.org 배포 워크플로우 구성 (publish.yml, 11개 패키지)
- [x] GitHub Release 자동 생성 구성 (softprops/action-gh-release)
- [x] 문서 사이트 구조 완성 (DocFX, cookbook, api, security)

---

## 6. 일정 요약

```
2026-02     2026-03     2026-04     2026-05     2026-06
  |           |           |           |           |
  v0.1.0-a1  |           |           |           |
  [현재]     |           |           |           |
              |           |           |           |
              |-Phase 1---|           |           |
              | v0.2.0-b  |           |           |
              | (4주)     |           |           |
              |           |           |           |
              |           |---Phase 2------------|
              |           |  v0.5.0-rc           |
              |           |  (8주)               |
              |           |           |           |
              |           |           |--Phase 3--|
              |           |           | v0.8.0-rc |
              |           |           | (4주)     |
              |           |           |           |
              |           |           |     Phase4|
              |           |           |     v1.0.0|
              |           |           |     (2주) |
```

| Phase | 버전 | 기간 | Task 수 | 핵심 산출물 |
|-------|------|------|---------|-------------|
| Phase 1 | v0.2.0-beta | 4주 | 7개 | BatchExecutor, CI 인프라, DX |
| Phase 2 | v0.5.0-rc | 8주 | 5개 | 리플렉션 제거, TypeHandler, 보안 |
| Phase 3 | v0.8.0-rc | 4주 | 7개 | 성능, SQLite, Aspire, 커버리지 |
| Phase 4 | v1.0.0-GA | 2주 | 3개 | API 동결, 문서, 릴리스 |
| 합계 | | 18주 | 22개 | |

---

## 7. 위험 요소 및 완화 전략

### R-01: TestExpressionEvaluator SG 전환 복잡도

위험: MyBatis 표현식의 다양한 패턴(중첩 and/or, 메서드 호출, 프로퍼티 체인)을 C# 코드로 완전 변환하기 어려움.
영향: Phase 2 지연.
완화: 지원 표현식 범위를 명확히 정의하고, 미지원 표현식은 런타임 폴백으로 처리. 점진적으로 지원 범위 확대.

### R-02: DbBatch API 호환성

위험: DbBatch는 .NET 6+에서 추상 클래스가 존재하지만, 실제 구현은 Provider별로 다름. SQL Server의 Microsoft.Data.SqlClient는 DbBatch 미지원(2024 기준).
영향: Task 1.2 범위 축소.
완화: SupportsBatching 플래그로 분기하고, 미지원 Provider는 순차 실행 폴백 유지. Provider별 지원 현황 문서화.

### R-03: AOT 트리밍 경고

위험: 리플렉션 제거 과정에서 기존 코드 경로의 AOT 경고가 전파될 수 있음.
영향: Phase 2 추가 작업.
완화: ILLink 경고를 CI에서 상시 감시 (`<TreatWarningsAsErrors>true`). 리플렉션 경로에 `[RequiresUnreferencedCode]` 명시.

### R-04: .NET Aspire 생태계 변동

위험: .NET Aspire가 아직 활발히 진화 중이라 API 변경 가능성.
영향: Phase 3 Task 3.6 재작업.
완화: Aspire Component를 별도 패키지로 분리하여 코어에 영향 없도록 격리. GA 시점의 Aspire 버전 확정 후 최종 구현.

### R-05: 하위 호환성 유지

위험: Phase 2의 SG 전환 과정에서 기존 사용자의 생성 코드 포맷이 변경될 수 있음.
영향: 기존 사용자 빌드 깨짐.
완화: Phase 1에서 PublicApiAnalyzers 도입. 생성 코드 포맷 변경 시 Major 버전업 검토. pre-release 버전에서 충분한 테스트.

---

## 부록: Task 의존성 그래프

```
Phase 1:
  1.1 BatchExecutor 연결 ──┐
  1.2 DbBatch 최적화 ──────┤ (1.1에 의존)
  1.3 NV005/NV006 진단 ────┤
  1.4 Codecov ─────────────┤
  1.5 Dependabot ──────────┤
  1.6 DocFX ───────────────┤
  1.7 MappingEmitter ──────┘

Phase 2:
  2.1 TestExpr SG 전환 ────┐
  2.2 ColumnMapper SG ─────┤ (1.7에 의존)
  2.3 [SqlConstant] ───────┤
  2.4 TypeHandler ─────────┤
  2.5 <bind> 태그 ─────────┘ (2.1에 의존)

Phase 3:
  3.1 SQLite ──────────────┐
  3.2 Object Pooling ──────┤
  3.3 벤치마크 CI ─────────┤
  3.4 Testcontainers ──────┤
  3.5 SourceLink ──────────┤
  3.6 Aspire ──────────────┤ (1.1, 1.2에 의존)
  3.7 커버리지 90% ────────┘ (1.4에 의존)

Phase 4:
  4.1 API 동결 ────────────┐ (Phase 1~3 전체에 의존)
  4.2 마이그레이션 가이드 ─┤
  4.3 릴리스 ──────────────┘
```
