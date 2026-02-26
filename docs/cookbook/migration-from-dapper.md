# Dapper에서 NuVatis로 마이그레이션

작성자: 최진호
작성일: 2026-02-26

---

## 개요

Dapper에서 NuVatis로 전환하면 다음을 얻는다:

- XML 기반 SQL 관리 (SQL과 C# 코드 분리)
- 컴파일 타임 타입 안전성 (Source Generator)
- 동적 SQL (if, choose, foreach 태그)
- 2차 캐시, 인터셉터, OpenTelemetry 통합
- AOT/트리밍 호환

Dapper의 간결함을 선호한다면 Attribute 기반 인라인 SQL도 지원한다.

---

## 단계별 전환

### 1단계: 패키지 설치

```bash
dotnet add package NuVatis.Core
dotnet add package NuVatis.Generators
dotnet add package NuVatis.PostgreSql   # 사용 중인 DB에 맞게 선택
dotnet add package NuVatis.Extensions.DependencyInjection
```

### 2단계: DI 등록

Dapper (before):
```csharp
builder.Services.AddScoped<IDbConnection>(_ =>
    new NpgsqlConnection(connectionString));
```

NuVatis (after):
```csharp
builder.Services.AddNuVatis(options => {
    options.ConnectionString = connectionString;
    options.Provider         = new PostgreSqlProvider();
});
```

### 3단계: 쿼리 마이그레이션

#### 단순 SELECT

Dapper:
```csharp
var user = connection.QueryFirstOrDefault<User>(
    "SELECT id, name, email FROM users WHERE id = @Id",
    new { Id = userId });
```

NuVatis (Attribute 방식 - Dapper와 유사한 인라인):
```csharp
[NuVatisMapper]
public interface IUserMapper {
    [Select("SELECT id, name, email FROM users WHERE id = @id")]
    User? GetById(int id);
}

// 사용
var user = userMapper.GetById(userId);
```

NuVatis (XML 방식 - 권장):
```xml
<!-- UserMapper.xml -->
<mapper namespace="UserMapper">
  <select id="GetById" resultType="User">
    SELECT id, name, email FROM users WHERE id = @id
  </select>
</mapper>
```

```csharp
var user = session.SelectOne<User>("UserMapper.GetById", new { id = userId });
```

#### 동적 WHERE 절

Dapper:
```csharp
var sql = "SELECT * FROM users WHERE 1=1";
if (!string.IsNullOrEmpty(name))
    sql += " AND name LIKE @Name";
if (age > 0)
    sql += " AND age > @Age";

var users = connection.Query<User>(sql, new { Name = $"%{name}%", Age = age });
```

NuVatis:
```xml
<select id="Search" resultType="User">
  SELECT * FROM users
  <where>
    <if test="Name != null and Name != ''">
      AND name LIKE '%' || @Name || '%'
    </if>
    <if test="Age > 0">
      AND age > @Age
    </if>
  </where>
</select>
```

#### INSERT + 자동 생성 키

Dapper:
```csharp
var id = connection.ExecuteScalar<int>(
    "INSERT INTO users (name, email) VALUES (@Name, @Email) RETURNING id",
    user);
```

NuVatis:
```xml
<insert id="Insert">
  <selectKey keyProperty="Id" order="AFTER">
    SELECT lastval()
  </selectKey>
  INSERT INTO users (name, email) VALUES (@Name, @Email)
</insert>
```

```csharp
session.Insert("UserMapper.Insert", user);
// user.Id가 자동으로 채워진다
```

#### 트랜잭션

Dapper:
```csharp
using var transaction = connection.BeginTransaction();
try {
    connection.Execute("INSERT INTO ...", param, transaction);
    connection.Execute("UPDATE ...", param, transaction);
    transaction.Commit();
} catch {
    transaction.Rollback();
    throw;
}
```

NuVatis:
```csharp
await session.ExecuteInTransactionAsync(async s => {
    await s.InsertAsync("OrderMapper.Insert", order);
    await s.UpdateAsync("InventoryMapper.Decrease", item);
});
```

#### Batch Insert

Dapper:
```csharp
connection.Execute(
    "INSERT INTO logs (message) VALUES (@Message)",
    logs); // IEnumerable<Log>
```

NuVatis:
```xml
<insert id="BatchInsert">
  INSERT INTO logs (message) VALUES
  <foreach collection="list" item="log" separator=",">
    (@log.Message)
  </foreach>
</insert>
```

### 4단계: 테스트 전환

Dapper 테스트에서 `IDbConnection` 모킹 대신 `InMemorySqlSession` 사용:

```csharp
var session = new InMemorySqlSession();
session.SetupResult<User>("UserMapper.GetById", expectedUser);

var result = session.SelectOne<User>("UserMapper.GetById", new { id = 1 });
Assert.Equal(expectedUser, result);
```

### 5단계: 점진적 전환 전략

한 번에 전환하지 않아도 된다. 같은 프로젝트에서 Dapper와 NuVatis를 공존시킬 수 있다:

1. 새 기능은 NuVatis로 작성
2. 기존 Dapper 코드는 그대로 유지
3. 리팩토링 시점에 순차적으로 NuVatis로 전환
4. 동일 DbConnection을 공유하면 트랜잭션도 공유 가능

---

## Dapper vs NuVatis 비교

| 기능 | Dapper | NuVatis |
|------|--------|---------|
| SQL 위치 | C# 코드 내 문자열 | XML 또는 Attribute |
| 동적 SQL | 문자열 연결 | XML 태그 (if, choose, foreach) |
| 타입 안전성 | 런타임 | 컴파일 타임 (SG) |
| 매핑 | 컨벤션 기반 | ResultMap + SG 생성 |
| 캐싱 | 없음 | 2차 캐시 내장 |
| 인터셉터 | 없음 | BeforeExecute/AfterExecute |
| AOT 호환 | 부분적 | 완전 지원 (SG) |
| 학습 곡선 | 낮음 | 중간 (MyBatis 경험 시 낮음) |
