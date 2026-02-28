# Diagnostic Codes Reference

작성자: 최진호
작성일: 2026-03-01

NuVatis Source Generator가 발생시키는 진단 코드의 상세 가이드. 각 코드별 발생 원인, 재현 예제, 해결 방법을 포함한다.

---

## 코드 일람

| 코드 | 심각도 | 설명 |
|------|--------|------|
| [NV001](#nv001) | Error | XML 매퍼 파싱 실패 또는 ResultMap을 찾을 수 없음 |
| [NV002](#nv002) | Error | 인터페이스 메서드에 매칭되는 Statement가 없음 |
| [NV003](#nv003) | Error | 파라미터 타입에 지정된 프로퍼티가 없음 |
| [NV004](#nv004) | **Error** | `${}` 문자열 치환 파라미터 타입이 `string` — SQL Injection 위험 |
| [NV005](#nv005) | Error | `<if test="...">` 표현식 컴파일 실패 |
| [NV006](#nv006) | Info | ResultMap 컬럼이 타입 프로퍼티와 매칭되지 않음 |
| [NV007](#nv007) | Warning | 미사용 ResultMap |
| [NV008](#nv008) | Warning | `[NuVatisMapper]` 없이 SQL Attribute만 사용 |

모든 진단 코드는 IDE에서 클릭 한 번으로 이 가이드로 이동할 수 있는 `helpLinkUri`가 포함되어 있다.

---

## NV001

**심각도**: Error

**설명**: XML 매퍼 파싱에 실패했거나, `<select resultMap="...">` 에서 참조한 ResultMap이 같은 파일에 정의되어 있지 않다.

### 발생 조건

1. XML 문법 오류 (태그 미닫힘, 잘못된 속성 등)
2. `resultMap="UserResult"` 참조 시 동일 파일에 `<resultMap id="UserResult">` 미정의
3. XML 인코딩 문제 (BOM 없는 UTF-8 권장)

### 재현 예제

```xml
<!-- 오류: UserResult가 정의되어 있지 않음 -->
<select id="GetById" resultMap="UserResult">
  SELECT id FROM users WHERE id = #{Id}
</select>
```

```
error NV001: resultMap 'UserResult' not found in mapper 'MyApp.Mappers.IUserMapper'
```

### 해결 방법

```xml
<!-- 해결: resultMap을 정의하거나 resultType으로 교체 -->
<resultMap id="UserResult" type="User">
  <id column="id" property="Id" />
</resultMap>

<select id="GetById" resultMap="UserResult">
  SELECT id FROM users WHERE id = #{Id}
</select>
```

또는 간단한 경우 `resultType` 사용:

```xml
<select id="GetById" resultType="User">
  SELECT id FROM users WHERE id = #{Id}
</select>
```

---

## NV002

**심각도**: Error

**설명**: `[NuVatisMapper]` 인터페이스의 메서드에 대응하는 XML Statement 또는 C# Attribute SQL이 없다.

### 발생 조건

- XML 매퍼의 `namespace`가 인터페이스 FQN과 불일치
- XML의 `id`가 인터페이스 메서드명과 불일치 (대소문자 포함)
- XML 파일이 빌드에 포함되지 않음 (`AdditionalFiles`에 미추가)

### 재현 예제

```csharp
[NuVatisMapper]
public interface IUserMapper {
    User? GetById(int id);
    User? FindById(int id);  // FindById에 대한 XML Statement가 없음
}
```

```xml
<mapper namespace="MyApp.Mappers.IUserMapper">
  <select id="GetById" resultMap="UserResult">
    SELECT * FROM users WHERE id = #{Id}
  </select>
  <!-- FindById 없음 -->
</mapper>
```

```
error NV002: No statement found for method 'MyApp.Mappers.IUserMapper.FindById'
```

### 해결 방법

**방법 1**: XML에 Statement 추가

```xml
<select id="FindById" resultMap="UserResult">
  SELECT * FROM users WHERE id = #{Id}
</select>
```

**방법 2**: C# Attribute로 인라인 SQL 추가

```csharp
[Select("SELECT * FROM users WHERE id = #{Id}")]
[ResultMap("UserResult")]
User? FindById(int id);
```

**방법 3**: XML 파일을 빌드에 포함

```xml
<!-- .csproj -->
<ItemGroup>
  <AdditionalFiles Include="Mappers/**/*.xml" />
</ItemGroup>
```

**방법 4**: namespace 정확히 맞춤

```xml
<!-- 반드시 C# 인터페이스 FQN과 정확히 일치 -->
<mapper namespace="MyApp.Mappers.IUserMapper">
```

---

## NV003

**심각도**: Error

**설명**: `#{}` 파라미터에서 지정한 프로퍼티명이 C# 파라미터 타입에 존재하지 않는다.

### 발생 조건

- 오타: `#{UsreNaem}` → 실제 프로퍼티는 `UserName`
- 리팩토링 후 프로퍼티명 변경 미반영
- 중첩 접근: `#{address.Ciyt}` → 실제는 `address.City`

### 재현 예제

```csharp
public record UserSearchParam(string UserName, string Email);
```

```xml
<select id="Search" resultMap="UserResult">
  SELECT * FROM users
  WHERE user_name = #{UsreNaem}   <!-- 오타: UserName이어야 함 -->
</select>
```

```
error NV003: Property 'UsreNaem' not found on type 'UserSearchParam'. Did you mean 'UserName'?
```

### 해결 방법

프로퍼티명의 오타를 수정한다.

```xml
WHERE user_name = #{UserName}
```

---

## NV004

**심각도**: Error (v2.0.0부터 Warning → Error 승격)

**설명**: XML 매퍼에서 `${}` 문자열 치환을 사용하고 있으며, 해당 파라미터의 C# 타입이 `string`이다. 사용자 입력이 SQL에 직접 삽입될 경우 SQL Injection 취약점이 발생한다.

### 발생 조건

```csharp
public record SortParam(string Column);  // string 타입
```

```xml
<select id="GetSorted" resultMap="UserResult">
  SELECT * FROM users ORDER BY ${Column}  <!-- string 타입 Column이 문자열 치환됨 -->
</select>
```

```
error NV004: ${Column} in 'MyApp.Mappers.IUserMapper.GetSorted' uses string substitution
which is vulnerable to SQL injection; use #{Column} for parameter binding,
or use SqlIdentifier type for safe string substitution
```

### 해결 방법

세 가지 경로 중 상황에 맞는 것을 선택한다.

**경로 1 (권장): `#{}` 파라미터 바인딩으로 교체**

`${}` 가 실제로는 파라미터 바인딩으로 충분한 경우 (WHERE 조건의 값 등).

```xml
<!-- 변경 전 -->
WHERE name = ${name}

<!-- 변경 후 -->
WHERE name = #{name}
```

**경로 2: `SqlIdentifier` 타입 사용 (동적 식별자)**

컬럼명, 테이블명, ORDER BY 방향 등 `${}` 가 불가피한 경우.

```csharp
// enum 기반 (가장 안전)
public enum SortColumn { CreatedAt, UserName, Id }
public record SortParam(SqlIdentifier Column);

mapper.GetSorted(new SortParam(SqlIdentifier.FromEnum(SortColumn.CreatedAt)));
```

```csharp
// 화이트리스트 기반 (사용자 입력)
var col = SqlIdentifier.FromAllowed(userInput, "id", "user_name", "created_at");
mapper.GetSorted(new SortParam(col));
```

**경로 3: `[SqlConstant]` 어트리뷰트 (리터럴 상수 전용)**

값이 코드에 하드코딩된 리터럴 상수인 경우에만 사용. **런타임 검증 없음**.

```csharp
public static class Tables {
    [SqlConstant] public const string Users = "users";
}
```

NV004가 억제되지만, 나중에 이 값이 사용자 입력으로 바뀌면 컴파일러가 침묵한다는 점에 주의.

### v2.0.0 마이그레이션

v1.x에서 NV004를 경고로 무시하던 코드는 v2.0.0부터 빌드가 실패한다. 프로젝트 전체에서 `${}` 사용을 검색하여 일괄 수정한다.

```bash
# 프로젝트의 모든 ${} 사용 검색
grep -r '\${' --include="*.xml" .
```

---

## NV005

**심각도**: Error

**설명**: `<if test="...">`, `<when test="...">`, `<bind value="...">` 의 표현식이 Source Generator가 파악한 파라미터 타입으로 컴파일되지 않는다.

### 발생 조건

- 존재하지 않는 프로퍼티 접근: `test="NonExistent != null"`
- 잘못된 타입 비교: `test="IntValue == 'string'"`
- 미지원 표현식 구문

### 재현 예제

```csharp
public record SearchParam(string? UserName);
```

```xml
<if test="Email != null">   <!-- Email 프로퍼티가 SearchParam에 없음 -->
  AND email = #{Email}
</if>
```

```
error NV005: Expression 'Email != null' failed to compile for type 'SearchParam'.
Property 'Email' does not exist.
```

### 해결 방법

1. 프로퍼티명 오타를 수정한다.
2. 파라미터 타입에 해당 프로퍼티를 추가한다.
3. 지원되지 않는 표현식은 단순화한다.

---

## NV006

**심각도**: Info

**설명**: `<resultMap>`의 `<result column="...">` 에서 지정한 컬럼명이 C# 타입의 프로퍼티와 매칭되지 않는다. 빌드는 성공하지만 해당 컬럼은 매핑되지 않는다.

### 발생 조건

- ResultMap의 `property` 오타
- 리팩토링 후 C# 프로퍼티명 변경 미반영

### 재현 예제

```csharp
public class User {
    public int    Id       { get; set; }
    public string UserName { get; set; }  // 프로퍼티명: UserName
}
```

```xml
<resultMap id="UserResult" type="User">
  <id     column="id"        property="Id" />
  <result column="user_name" property="Username" />  <!-- 대소문자 오타: UserName이어야 함 -->
</resultMap>
```

```
info NV006: Column 'user_name' (property 'Username') not matched to any property of 'User'.
Available properties: Id, UserName
```

### 해결 방법

프로퍼티명을 정확하게 수정한다.

```xml
<result column="user_name" property="UserName" />
```

---

## NV007

**심각도**: Warning

**설명**: `<resultMap>` 이 정의되어 있지만 어떤 `<select>`, `<association>`, `<collection>` 에서도 참조되지 않는다.

### 발생 조건

- Statement를 삭제했지만 ResultMap은 그대로 남아 있음
- `resultMap` 속성의 오타로 참조가 끊어짐

### 재현 예제

```xml
<resultMap id="OldUserResult" type="User">  <!-- 어디에서도 참조되지 않음 -->
  <id column="id" property="Id" />
</resultMap>

<select id="GetById" resultMap="UserResult">  <!-- UserResult를 참조, OldUserResult 아님 -->
  SELECT id FROM users WHERE id = #{Id}
</select>
```

```
warning NV007: ResultMap 'OldUserResult' is defined but never referenced.
```

### 해결 방법

1. 미사용 ResultMap을 삭제한다.
2. Statement에서 올바른 ResultMap을 참조하고 있는지 확인한다.

---

## NV008

**심각도**: Warning

**설명**: 인터페이스에 `[Select]`, `[Insert]` 등 SQL Attribute가 있지만 `[NuVatisMapper]` 가 없다. Source Generator는 해당 인터페이스를 처리하지만, 명시적 opt-in을 권장한다.

### 재현 예제

```csharp
// [NuVatisMapper] 누락
public interface IUserMapper {
    [Select("SELECT * FROM users WHERE id = #{Id}")]
    User? GetById(int id);
}
```

```
warning NV008: Interface 'IUserMapper' has SQL attributes but is missing [NuVatisMapper].
Consider adding [NuVatisMapper] for explicit opt-in.
```

### 해결 방법

`[NuVatisMapper]` 어트리뷰트를 추가한다.

```csharp
[NuVatisMapper]
public interface IUserMapper {
    [Select("SELECT * FROM users WHERE id = #{Id}")]
    User? GetById(int id);
}
```

---

## 자주 묻는 진단 관련 질문

**Q: NV004를 프로젝트 전체에서 일시적으로 경고로 낮출 수 있나?**

A: `.editorconfig` 또는 `<NoWarn>`으로 임시 억제 가능하다. 단, 프로덕션 배포 전에 반드시 해결해야 한다.

```xml
<!-- .csproj (임시 억제, 프로덕션에서는 제거) -->
<NoWarn>$(NoWarn);NV004</NoWarn>
```

**Q: NV006이 계속 나오는데 프로퍼티명이 맞는 것 같다.**

A: C#은 대소문자를 구분한다. `username`과 `UserName`은 다르다. Source Generator의 매칭은 대소문자를 구분하지 않지만, 정확한 프로퍼티명을 사용하는 것이 권장된다.

**Q: XML 파일을 추가했는데 NV002가 계속 발생한다.**

A: `.csproj`에 `AdditionalFiles`가 추가되어 있는지 확인한다.

```xml
<ItemGroup>
  <AdditionalFiles Include="Mappers/**/*.xml" />
</ItemGroup>
```

그래도 해결되지 않으면 `dotnet build --no-incremental`로 증분 빌드 캐시를 비우고 재빌드한다.
