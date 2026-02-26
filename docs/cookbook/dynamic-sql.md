# Dynamic SQL Cookbook

## if - 조건부 SQL

파라미터가 null이 아닌 경우에만 조건을 포함한다.

```xml
<select id="Search" resultMap="UserResult">
  SELECT * FROM users
  <where>
    <if test="UserName != null">
      AND user_name LIKE #{UserName}
    </if>
    <if test="Email != null">
      AND email = #{Email}
    </if>
    <if test="MinAge > 0">
      AND age >= #{MinAge}
    </if>
  </where>
</select>
```

`<where>` 태그는 첫 번째 조건의 선행 AND/OR을 자동 제거한다.

## choose/when/otherwise - Switch-case

```xml
<select id="SearchOrdered" resultMap="UserResult">
  SELECT * FROM users
  ORDER BY
  <choose>
    <when test="SortBy == 'name'">user_name</when>
    <when test="SortBy == 'email'">email</when>
    <when test="SortBy == 'date'">created_at DESC</when>
    <otherwise>id</otherwise>
  </choose>
</select>
```

## set - 동적 UPDATE

null이 아닌 필드만 업데이트한다. 후행 쉼표를 자동 제거한다.

```xml
<update id="UpdatePartial">
  UPDATE users
  <set>
    <if test="UserName != null">user_name = #{UserName},</if>
    <if test="Email != null">email = #{Email},</if>
    <if test="Age > 0">age = #{Age},</if>
  </set>
  WHERE id = #{Id}
</update>
```

```csharp
mapper.UpdatePartial(new User { Id = 42, Email = "new@example.com" });
// 생성 SQL: UPDATE users SET email = @p0 WHERE id = @p1
```

## foreach - 컬렉션 반복

### IN 절

```xml
<select id="GetByIds" resultMap="UserResult">
  SELECT * FROM users WHERE id IN
  <foreach collection="Ids" item="id" open="(" separator="," close=")">
    #{id}
  </foreach>
</select>
```

```csharp
var users = mapper.GetByIds(new { Ids = new[] { 1, 2, 3, 4, 5 } });
```

### Batch VALUES

```xml
<insert id="InsertBatch">
  INSERT INTO logs (message, level, created_at) VALUES
  <foreach collection="Logs" item="log" separator=",">
    (#{log.Message}, #{log.Level}, #{log.CreatedAt})
  </foreach>
</insert>
```

## bind - 변수 바인딩

표현식 결과를 변수로 바인딩하여 SQL에서 재사용한다. LIKE 패턴 생성에 유용하다.

```xml
<select id="SearchByName" resultMap="UserResult">
  <bind name="namePattern" value="'%' + UserName + '%'" />
  SELECT * FROM users
  WHERE user_name LIKE #{namePattern}
</select>
```

`[SqlConstant]` 어트리뷰트가 적용된 필드/프로퍼티는 SQL Injection 검사(NV004)에서 안전한 상수로 간주된다.

```csharp
public class TableConstants {
    [SqlConstant]
    public const string UsersTable = "users";
}
```

## sql/include - SQL Fragment 재사용

```xml
<sql id="userColumns">
  id, user_name, email, created_at
</sql>

<select id="GetById">
  SELECT <include refid="userColumns" />
  FROM users WHERE id = #{Id}
</select>

<select id="GetAll">
  SELECT <include refid="userColumns" />
  FROM users ORDER BY id
</select>
```

## 복합 조건 조합

```xml
<select id="AdvancedSearch" resultMap="UserResult">
  SELECT * FROM users
  <where>
    <if test="Keyword != null">
      AND (user_name LIKE #{Keyword} OR email LIKE #{Keyword})
    </if>
    <if test="Status != null">
      AND status = #{Status}
    </if>
    <if test="RoleIds != null">
      AND role_id IN
      <foreach collection="RoleIds" item="rid" open="(" separator="," close=")">
        #{rid}
      </foreach>
    </if>
    <if test="StartDate != null">
      AND created_at >= #{StartDate}
    </if>
    <if test="EndDate != null">
      AND created_at &lt;= #{EndDate}
    </if>
  </where>
  ORDER BY
  <choose>
    <when test="SortBy == 'name'">user_name</when>
    <otherwise>created_at DESC</otherwise>
  </choose>
  LIMIT #{PageSize} OFFSET #{Offset}
</select>
```
