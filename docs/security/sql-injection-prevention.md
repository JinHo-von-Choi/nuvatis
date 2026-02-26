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

### ${param} - 문자열 치환 (위험)

```xml
<select id="GetByTable">
  SELECT * FROM ${tableName} WHERE id = #{Id}
</select>
```

생성되는 SQL: `SELECT * FROM users WHERE id = @p0`

`${tableName}`의 값이 SQL 문자열에 직접 삽입된다. 사용자 입력이 그대로 SQL에 포함되므로 SQL Injection 공격에 취약하다.

## NV004 컴파일 경고

NuVatis Source Generator는 XML 매퍼에서 `${}` 사용을 감지하면 컴파일 시 NV004 경고를 발생시킨다.

```
warning NV004: ${tableName} in 'MyApp.Mappers.IUserMapper.GetByTable' uses string substitution
which is vulnerable to SQL injection; use #{tableName} instead
```

## ${} 사용이 불가피한 경우

동적 테이블명, 컬럼명, ORDER BY 방향 등은 파라미터 바인딩이 불가능하다. 이 경우 반드시 화이트리스트 검증을 수행한다.

```csharp
private static readonly HashSet<string> AllowedSortColumns = new() {
    "user_name", "email", "created_at", "id"
};

public IList<User> SearchSorted(string sortColumn) {
    if (!AllowedSortColumns.Contains(sortColumn)) {
        throw new ArgumentException($"Invalid sort column: {sortColumn}");
    }
    return _mapper.SearchSorted(new { SortColumn = sortColumn });
}
```

```xml
<select id="SearchSorted" resultMap="UserResult">
  SELECT * FROM users ORDER BY ${SortColumn}
</select>
```

화이트리스트로 검증된 값만 `${}` 에 전달한다. 사용자 입력을 절대 직접 전달하지 않는다.

## [SqlConstant] 어트리뷰트

컴파일타임 상수로 사용되는 필드/프로퍼티에 `[SqlConstant]`를 적용하면 NV004 경고가 억제된다. 이 값은 Source Generator가 안전한 상수로 간주한다.

```csharp
public class TableConstants {
    [SqlConstant]
    public const string UsersTable = "users";

    [SqlConstant]
    public const string OrdersTable = "orders";
}
```

```xml
<select id="GetAllUsers">
  SELECT * FROM ${UsersTable}
</select>
```

`[SqlConstant]`가 적용된 `UsersTable`은 NV004 경고를 발생시키지 않는다.

## NV004 경고 억제

화이트리스트 검증이 완료된 정당한 사용이라면 `#pragma warning disable`로 억제할 수 있다. 단, 팀 코드 리뷰에서 반드시 검증해야 한다.

## Best Practices

1. 항상 `#{}` 사용을 기본으로 한다
2. `${}` 사용 시 반드시 화이트리스트 검증을 동반한다
3. 사용자 입력을 `${}` 에 직접 전달하지 않는다
4. NV004 경고를 무시하지 않는다
5. 코드 리뷰에서 `${}` 사용을 반드시 점검한다
