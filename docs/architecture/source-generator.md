# Source Generator Architecture

## 개요

NuVatis Source Generator는 Roslyn IIncrementalGenerator를 구현하여 빌드타임에 다음 코드를 자동 생성한다:

1. Mapper Interface 구현체 (Proxy)
2. SQL 빌드 메서드 (동적 SQL 포함)
3. DI Registry (mapper 등록 코드)

## 처리 파이프라인

```
XML Mapper Files (AdditionalTexts)
    |
    v
XmlMapperParser.Parse()         -- XML -> ParsedMapper 모델
    |
    v
IncludeResolver.ResolveIncludes() -- <include refid="..."> 해소
    |
    v
StringSubstitutionAnalyzer      -- ${} 사용 감지 -> NV004 경고
    |
    v
InterfaceAnalyzer.FindMapperInterfaces() -- C# 컴파일에서 mapper 인터페이스 탐지
    |
    v
ProxyEmitter.Emit()             -- 각 인터페이스의 구현체 코드 생성
    |
    v
RegistryEmitter.Emit()          -- DI 등록 코드 생성
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
    public static void RegisterAll(Type type, ISqlSession session) {
        // type -> 구현체 매핑 테이블
    }
}
```

## 진단 코드

| Code | Severity | Description |
|------|----------|-------------|
| NV001 | Error | ResultMap을 찾을 수 없음 |
| NV002 | Error | 인터페이스 메서드에 매칭되는 statement 없음 |
| NV003 | Error | 파라미터 타입에 지정된 프로퍼티가 없음 |
| NV004 | Warning | ${} 문자열 치환 사용 (SQL Injection 위험) |
| NV005 | Error | test 표현식 컴파일 실패 |
| NV006 | Info | ResultMap 컬럼이 타입 프로퍼티와 매칭되지 않음 |
