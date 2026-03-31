# Source Generator Architecture

## 개요

NuVatis Source Generator는 Roslyn IIncrementalGenerator를 구현하여 빌드타임에 다음 코드를 자동 생성한다:

1. Mapper Interface 구현체 (Proxy)
2. DynamicSqlEmitter - 동적 SQL 빌드 메서드 (런타임 리플렉션 제거)
3. MappingEmitter - ResultMap 기반 타입-세이프 매핑 코드
4. DI Registry (mapper 등록 코드)

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
ProxyEmitter.Emit()               -- 각 인터페이스의 구현체 코드 생성
    |
    v
DynamicSqlEmitter.Emit()          -- 동적 SQL 빌드 메서드 생성 (리플렉션 제거)
    |
    v
MappingEmitter.Emit()             -- ResultMap 기반 매핑 코드 생성
    |
    v
UnusedResultMapAnalyzer           -- 미사용 ResultMap 탐지 -> NV007 경고
    |
    v
ResultMapColumnAnalyzer           -- ResultMap 컬럼-프로퍼티 불일치 -> NV006 정보
    |
    v
RegistryEmitter.Emit(interfaces, xmlMappers)
    ├── RegisterAttributeStatements() -- [Select]/[Insert] 등 어트리뷰트 statement 등록
    └── RegisterXmlStatements()       -- XML statement 등록
          ├── 정적 statement → SqlSource 텍스트 직접 등록
          └── 동적 statement → ParameterEmitter.EmitDynamicBuilderLambda() 람다 등록
                (foreach, if, where, set, choose 포함 시 동적으로 판별)
```

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
