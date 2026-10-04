# Source Generator Architecture

## 개요

NuVatis Source Generator는 Roslyn IIncrementalGenerator를 구현하여 빌드타임에 다음 코드를 자동 생성한다:

1. Mapper Interface 구현체 (ProxyEmitter)
2. 동적 SQL 빌드 코드 (ParameterEmitter) — 정적 메서드와 람다 두 경로
3. 타입 매퍼 코드 (TypeMappersEmitter → MappingEmitter)
4. DI Registry (RegistryEmitter)

> **주의**: `DynamicSqlEmitter.EmitSqlBuilder()` 는 public API 로 등록돼 있으나 **실제 파이프라인에서 호출되지 않는다.**
> 동적 SQL 본문은 `ParameterEmitter` 가 담당하며, `DynamicSqlEmitter` 는 `HasDynamicNodes()`
> 로 동적 statement 인지를 판별하는 데만 쓰인다. `EmitSqlBuilder` 내부의 `<where>` 블록은
> `__whereBuilder` 를 `__sql` 에 연결하지 않아 조건을 수집하지 못하는 결함도 있어, v2.8.0
> 시점 기준 배선 여부를 결정하지 않은 상태다. `docs/architecture/source-generator.md` 이전
> 판본이 이 클래스를 파이프라인 상에 그려둔 것은 낡은 기술서였다.

## 처리 파이프라인

```
XML Mapper Files (AdditionalTexts)
    |
    v
XmlMapperParser.Parse()           -- XML -> ParsedMapper 모델
    |
    v
IncludeResolver.ResolveIncludes() -- <include refid="..."> 해소
    |
    v
StringSubstitutionAnalyzer        -- ${} 사용 감지 -> NV004 빌드 오류
    |
    v
InterfaceAnalyzer.FindMapperInterfaces() -- C# 컴파일에서 mapper 인터페이스 탐지
    |
    v
ProxyEmitter.Emit()               -- 각 인터페이스의 구현체 + BuildSql_XXX 정적 메서드 생성
    |
    v
ParameterEmitter.EmitBuildSqlStaticMethod()  -- 프록시 경로 동적 SQL 본문
    |
    v
TypeMappersEmitter.Emit()         -- NuVatisTypeMappers 타입 매퍼 코드 생성
    |                                  (MappingEmitter.EmitMapMethodFromType 위임)
    |
    v
UnusedResultMapAnalyzer           -- 미사용 ResultMap 탐지 -> NV007 경고
    |
    v
ResultMapColumnAnalyzer           -- ResultMap 컬럼-프로퍼티 불일치 -> NV006 정보
    |
    v
RegistryEmitter.Emit(interfaces, xmlMappers, typeToMethod, compilation)
    ├── RegisterAll()                -- 인터페이스 → 구현체 등록
    ├── RegisterStatements()         -- resultType 기반 RowMapper 연결
    └── RegisterXmlStatements()      -- XML statement 등록
          ├── 정적 statement → SqlSource 텍스트 직접 등록
          └── 동적 statement → ParameterEmitter.EmitDynamicBuilderLambda() 람다 등록
                (DynamicSqlEmitter.HasDynamicNodes() 로 판별)
```

> `compilation` 인자는 `TypedPropertyAccessResolver` 의 심볼 조회에만 쓰인다
> (아래 "파라미터 직접 접근" 참조).

## 인터페이스 탐지 전략

InterfaceAnalyzer는 다음 두 조건 중 하나를 만족하는 인터페이스만 스캔한다:

1. `[NuVatisMapper]` 어트리뷰트가 적용된 인터페이스
2. 메서드에 NuVatis SQL 어트리뷰트(`[Select]`, `[Insert]`, `[Update]`, `[Delete]`)가 있는 인터페이스

이전에는 "Mapper" 접미사 관례로 전역 스캔했으나, AutoMapper 등 외부 라이브러리와 충돌이 발생하여 명시적 opt-in 방식으로 전환했다.

## 생성되는 코드 구조

### Mapper Proxy

```csharp
// IUserMapperImpl.g.cs (자동 생성)
internal sealed class IUserMapperImpl : IUserMapper {
    private readonly ISqlSession _session;

    public IUserMapperImpl(ISqlSession session) {
        _session = session;
    }

    public User? GetById(int id) {
        return _session.SelectOne<User>("MyApp.Mappers.IUserMapper.GetById", new { id });
    }

    // ... 각 메서드 구현
}
```

### Registry

```csharp
// NuVatisMapperRegistry.g.cs (자동 생성)
public static class NuVatisMapperRegistry {

    // Mapper 인터페이스 -> 구현체 등록
    public static void RegisterAll(Type type, ISqlSession session) { ... }

    // [Select]/[Insert] 등 Attribute 기반 statement 등록
    public static void RegisterAttributeStatements(
        Dictionary<string, MappedStatement> statements) { ... }

    // XML Mapper statement 등록 (v2.3.0+)
    // 정적 statement: SqlSource 텍스트 직접 설정
    // 동적 statement: DynamicSqlBuilder 람다 설정
    public static void RegisterXmlStatements(
        Dictionary<string, MappedStatement> statements) {
        // 정적 예시:
        statements["MyApp.IUserMapper.GetById"] = new MappedStatement {
            FullId        = "MyApp.IUserMapper.GetById",
            StatementType = StatementType.Select,
            SqlSource     = "SELECT id, user_name FROM users WHERE id = #{Id}",
        };
        // 동적 예시 (<foreach> 포함):
        statements["MyApp.IUserMapper.InsertBatch"] = new MappedStatement {
            FullId             = "MyApp.IUserMapper.InsertBatch",
            StatementType      = StatementType.Insert,
            SqlSource          = "",
            DynamicSqlBuilder  = static (__param_) => {
                // SG가 생성하는 foreach/if/where 처리 람다
                ...
            },
        };
    }
}
```

## 파라미터 직접 접근 (v2.8.0~)

`#{...}` 바인딩은 기본적으로 `__getprop_` 지역 함수를 통해
`GetType().GetProperty(name, Public|Instance|IgnoreCase)` 리플렉션을 수행한다.
`TypedPropertyAccessResolver` 는 매핑 파라미터의 정적 타입을 Roslyn 심볼로 해석해,
**그 타입에 실제로 존재하고 읽을 수 있는 프로퍼티에 대해서만** 컴파일된 직접 접근을
방출한다.

```csharp
private static (string Sql, List<DbParameter> Parameters) BuildSql_Search(object? __param_)
{
    // ... __getprop_ 지역 함수 ...

    // 심볼 해석이 성공했을 때만 방출되는 한 줄
    var __typed_ = __param_ is global::MyApp.OrderQuery
        ? (global::MyApp.OrderQuery)__param_
        : (global::MyApp.OrderQuery?)null;

    // __getprop_(__param_, "Name")  →  __typed_?.Name
    // 값 대입 시 (object?) 캐스트가 붙는다 — int? ?? DBNull 는 컴파일되지 않으므로
    __params_.Add(ParameterBinder.CreateParameter(
        __pn_, (object?)__typed_?.Name ?? System.DBNull.Value));
}
```

### 안전 계약

| 조건 | 동작 |
|------|------|
| 심볼 해석 성공 + 읽을 수 있는 공개 인스턴스 프로퍼티 | `__typed_?.Prop` 직접 접근 |
| 타입에 없는 프로퍼티 (오타) | `__getprop_` 리플렉션으로 폴백 — 컴파일 에러가 나지 않는다 |
| 중첩 경로 `#{user.Address.City}` | 폴백 (세그먼트 타입 잇기 미구현) |
| foreach 아이템 `#{it.Name}` | 폴백 (엘먼트 타입 해석 미구현) |
| 파라미터 타입이 `object` / `dynamic` / 미해결 | 폴백 |
| 파라미터가 `null` 이거나 타입 불일치 | `__typed_` 가 null → 종전과 동일하게 null |

프로퍼티 조회는 `OrdinalIgnoreCase` 로 하되(`__getprop_` 의
`BindingFlags.IgnoreCase` 와 동일) 방출에는 **선언된 정확한 대소문자**를 쓴다.

### 성능 — 정직한 범위

`DynamicSqlBuildBenchmark` 실측(SQLite 인메모리, 인자 5개 statement):

| 측정 범위 | 리플렉션 | 직접 접근 | 개선 |
|-----------|---------|----------|------|
| SQL 조립 단독 | 1,536.3 ns | 706.8 ns | 2.17배 |
| 실제 쿼리까지 포함 | 28,152.3 ns | 26,753.1 ns | **약 5%** |

조립 절감폭 829 ns 이 전체 쿼리의 약 3% 에 불과하다. 네트워크 DB(왕복 0.5~5 ms)
에서는 상대 이득이 1% 미만으로 내려간다. `SqlSession.BuildSql` 은 SQL 문자열을
캐시하지 않아 매 실행마다 이 비용을 내지만, 결과 캐시 히트 시에는 건너뛴다.

## MappingEmitter — 지원 타입

`MappingEmitter`는 `<resultMap>`에 선언된 각 프로퍼티의 CLR 타입을 분석하여 타입별 최적 reader 호출 코드를 생성한다.

| CLR 타입 | 생성 코드 |
|----------|----------|
| `int` / `int?` | `reader.GetInt32(ordinal)` |
| `long` / `long?` | `reader.GetInt64(ordinal)` |
| `string` | `reader.GetString(ordinal)` |
| `bool` / `bool?` | `reader.GetBoolean(ordinal)` |
| `DateTime` / `DateTime?` | `reader.GetDateTime(ordinal)` |
| `decimal` / `decimal?` | `reader.GetDecimal(ordinal)` |
| `double` / `double?` | `reader.GetDouble(ordinal)` |
| `Enum` 파생 타입 | `(EnumType)reader.GetInt32(ordinal)` |

### Enum 프로퍼티 처리

`Enum` 타입 프로퍼티는 `(EnumType)reader.GetInt32(ordinal)` 형식의 명시적 캐스트 코드를 생성한다.
`Convert.ToObject`나 리플렉션을 사용하지 않아 AOT 환경에서도 안전하다.

```csharp
// 생성된 코드 예시
result.Status = (OrderStatus)reader.GetInt32(2);
```

---

## 진단 코드

| Code | Severity | Description |
|------|----------|-------------|
| NV001 | Error | ResultMap을 찾을 수 없음 |
| NV002 | Error | 인터페이스 메서드에 매칭되는 statement 없음 |
| NV003 | Error | 파라미터 타입에 지정된 프로퍼티가 없음 |
| NV004 | Error | ${} 문자열 치환 사용 (SQL Injection 위험, [SqlConstant] 적용 시 억제) |
| NV005 | Error | test 표현식 컴파일 실패 |
| NV006 | Info | ResultMap 컬럼이 타입 프로퍼티와 매칭되지 않음 |
| NV007 | Warning | 미사용 ResultMap (어떤 statement에서도 참조되지 않음) |
| NV008 | Warning | ResultMap 프로퍼티와 매핑 대상 타입 불일치 |
