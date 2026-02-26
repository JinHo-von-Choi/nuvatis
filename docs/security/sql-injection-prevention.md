# SQL Injection Prevention

## #{} vs ${} - 반드시 이해해야 할 차이

### #{param} - 파라미터 바인딩 (안전)

```xml
<select id="GetById">
  SELECT * FROM users WHERE id = #{Id}
</select>
```

생성되는 SQL: `SELECT * FROM users WHERE id = @p0`

값이 DB 드라이버의 파라미터로 전달된다. SQL Injection이 불가능하다.

### ${param} - 문자열 치환 (위험, 컴파일 오류)

```xml
<select id="GetByTable">
  SELECT * FROM ${tableName} WHERE id = #{Id}
</select>
```

생성되는 SQL: `SELECT * FROM users WHERE id = @p0`

`${tableName}`의 값이 SQL 문자열에 직접 삽입된다. 사용자 입력이 그대로 SQL에 포함되므로
SQL Injection 공격에 취약하다.

**v2.0.0부터 `${}` 파라미터 타입이 `string`이면 NV004 빌드 오류가 발생한다.**

## NV004 진단

NuVatis Source Generator는 XML 매퍼에서 `${}` 사용을 감지하면 컴파일 시 NV004를 발생시킨다.

```
error NV004: ${tableName} in 'MyApp.Mappers.IUserMapper.GetByTable' uses string substitution
which is vulnerable to SQL injection; use #{tableName} instead
```

v2.0.0부터 Error로 승격되어 빌드가 실패한다.

## ${} 사용이 불가피한 경우: 3가지 안전한 경로

동적 테이블명, 컬럼명, ORDER BY 방향 등은 파라미터 바인딩이 불가능하다.
이 경우 아래 세 가지 경로 중 하나를 선택한다.

### 경로 1. SqlIdentifier.FromEnum — enum 기반 (가장 안전)

값의 집합이 컴파일 타임에 확정된다면 enum을 정의하고 `SqlIdentifier.FromEnum`을 사용한다.

```csharp
using NuVatis.Core.Sql;

public enum SortColumn { CreatedAt, UserName, Id }

public record SortParam(SqlIdentifier Column);

// 사용
var result = mapper.GetSorted(
    new SortParam(SqlIdentifier.FromEnum(SortColumn.CreatedAt)));
```

```xml
<select id="GetSorted" resultMap="UserResult">
  SELECT * FROM users ORDER BY ${Column}
</select>
```

enum 이름이 직접 SQL에 삽입된다. enum 값은 컴파일 타임에 확정되므로 Injection이 불가능하다.

### 경로 2. SqlIdentifier.FromAllowed — 화이트리스트 기반

런타임에 결정되지만 허용 값 목록이 정해진 경우 화이트리스트 검증을 수행한다.

```csharp
using NuVatis.Core.Sql;

public record SortParam(SqlIdentifier Column);

public IList<User> GetSorted(string userInput) {
    var column = SqlIdentifier.FromAllowed(
        userInput,
        "id", "user_name", "created_at", "email");  // 허용 목록

    return _mapper.GetSorted(new SortParam(column));
}
```

허용 목록에 없는 값은 `ArgumentException`이 발생한다.

### 경로 3. [SqlConstant] 어트리뷰트 — 컴파일타임 상수 전용

값이 리터럴 상수이고 런타임에 변하지 않을 때 사용한다. NV004가 억제되지만 **런타임 검증은 없다**.

```csharp
public static class TableRef {
    [SqlConstant] public const string Users  = "users";
    [SqlConstant] public const string Orders = "orders";
}
```

```xml
<select id="GetAllUsers">
  SELECT * FROM ${UsersTable}
</select>
```

주의: 나중에 `UsersTable`의 소스가 사용자 입력으로 변경되면 컴파일러가 침묵한다.
반드시 진짜 상수에만 적용하라.

## SqlIdentifier 타입

`SqlIdentifier`는 `NuVatis.Core.Sql` 네임스페이스에 있다.

```csharp
using NuVatis.Core.Sql;
```

생성자는 private이며 세 가지 팩토리 메서드로만 생성된다.

| 메서드 | 설명 | 안전 수준 |
|--------|------|-----------|
| `SqlIdentifier.FromEnum<T>(T value)` | enum 값 → 이름 문자열 | 최고 |
| `SqlIdentifier.FromAllowed(string, params string[])` | 화이트리스트 검증 | 높음 |
| `SqlIdentifier.From(string)` | 패턴 검사만 수행 | 보통 (리터럴 전용) |

`SqlIdentifier.From`이 차단하는 패턴:
- 금지 문자: `;` `'` `"` `\n` `\r` `\0`
- 금지 시퀀스: `--` `/*` `*/`
- SQL 키워드 (단어 경계): `union`, `select`, `drop`, `insert`, `or`, `and`

## Best Practices

1. 항상 `#{}` 사용을 기본으로 한다
2. `${}` 가 필요하면 `SqlIdentifier.FromEnum` 또는 `SqlIdentifier.FromAllowed`를 사용한다
3. `[SqlConstant]`는 리터럴 상수에만 사용하고, 런타임 값에는 사용하지 않는다
4. `SqlIdentifier.From`은 코드에 하드코딩된 리터럴에만 사용한다
5. 사용자 입력을 어떤 경로로도 `${}` 에 직접 전달하지 않는다
