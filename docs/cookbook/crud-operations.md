# CRUD Operations Cookbook

## 기본 CRUD

### Insert

```xml
<insert id="Insert">
  INSERT INTO users (user_name, email, created_at)
  VALUES (#{UserName}, #{Email}, #{CreatedAt})
</insert>
```

```csharp
var affected = mapper.Insert(new User {
    UserName  = "jinho",
    Email     = "jinho@example.com",
    CreatedAt = DateTime.UtcNow
});
```

### Select One

```xml
<select id="GetById" resultMap="UserResult">
  SELECT id, user_name, email FROM users WHERE id = #{Id}
</select>
```

```csharp
var user = mapper.GetById(42);
```

### Select List

```xml
<select id="GetAll" resultMap="UserResult">
  SELECT id, user_name, email FROM users ORDER BY id
</select>
```

```csharp
IList<User> users = mapper.GetAll();
```

### Update

```xml
<update id="Update">
  UPDATE users SET user_name = #{UserName}, email = #{Email}
  WHERE id = #{Id}
</update>
```

```csharp
var affected = mapper.Update(new User { Id = 42, UserName = "updated" });
```

### Delete

```xml
<delete id="Delete">
  DELETE FROM users WHERE id = #{Id}
</delete>
```

```csharp
var affected = mapper.Delete(42);
```

## Batch Insert (foreach)

```xml
<insert id="InsertBatch">
  INSERT INTO users (user_name, email) VALUES
  <foreach collection="Users" item="u" separator=",">
    (#{u.UserName}, #{u.Email})
  </foreach>
</insert>
```

```csharp
mapper.InsertBatch(new BatchParam {
    Users = new List<User> {
        new() { UserName = "user1", Email = "u1@ex.com" },
        new() { UserName = "user2", Email = "u2@ex.com" }
    }
});
```

## Transaction

```csharp
using var session = factory.OpenSession();
var mapper = session.GetMapper<IUserMapper>();

mapper.Insert(user1);
mapper.Insert(user2);
session.Commit();
```

Commit 없이 Dispose되면 자동 Rollback된다.

### Async Transaction Helper

```csharp
await session.ExecuteInTransactionAsync(async () => {
    await mapper.InsertAsync(user1);
    await mapper.InsertAsync(user2);
});
```

성공 시 자동 Commit, 예외 발생 시 자동 Rollback.

## 페이징

```xml
<select id="GetPaged" resultMap="UserResult">
  SELECT id, user_name, email FROM users
  ORDER BY id
  LIMIT #{PageSize} OFFSET #{Offset}
</select>
```

```csharp
var page = mapper.GetPaged(new PageParam { PageSize = 20, Offset = 40 });
```

## ResultMap (컬럼-프로퍼티 매핑)

DB 컬럼명과 C# 프로퍼티명이 다를 때 명시적으로 매핑한다.

```xml
<resultMap id="UserResult" type="User">
  <id column="user_id" property="Id" />
  <result column="user_name" property="UserName" />
  <result column="email_address" property="Email" />
  <result column="is_active" property="IsActive" />
</resultMap>
```

ResultMap 없이 사용하면 ColumnMapper가 컬럼명-프로퍼티명을 자동 매칭한다 (case-insensitive, underscore 무시).
