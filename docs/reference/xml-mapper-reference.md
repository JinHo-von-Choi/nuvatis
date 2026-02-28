# XML Mapper Tag Reference

작성자: 최진호
작성일: 2026-03-01

모든 XML 매퍼 태그와 속성의 완전한 레퍼런스.

---

## mapper (루트 요소)

모든 Mapper XML 파일의 루트 요소.

```xml
<mapper namespace="MyApp.Mappers.IUserMapper">
  <!-- 태그들 -->
</mapper>
```

**속성**

| 속성 | 필수 | 설명 |
|------|------|------|
| `namespace` | 예 | C# Mapper 인터페이스의 완전한 이름. `[NuVatisMapper]` 인터페이스의 FQN과 정확히 일치해야 한다. |

**주의**: namespace 불일치 시 Source Generator가 NV002를 발생시켜 빌드가 실패한다.

---

## select

SELECT 쿼리를 정의한다. 단일 행 또는 여러 행 조회 모두 이 태그를 사용한다.

```xml
<select id="GetById" resultMap="UserResult" resultType="User" useCache="false" commandTimeout="30">
  SELECT id, user_name, email FROM users WHERE id = #{Id}
</select>
```

**속성**

| 속성 | 필수 | 기본값 | 설명 |
|------|------|--------|------|
| `id` | 예 | — | Statement 식별자. Mapper 인터페이스 메서드명과 일치해야 한다 |
| `resultMap` | 조건부 | — | 결과 매핑에 사용할 `<resultMap>` id. `resultType`과 중복 불가 |
| `resultType` | 조건부 | — | 결과 C# 타입명. `resultMap`과 중복 불가. 컬럼명 자동 매칭(ColumnMapper 사용) |
| `useCache` | 아니오 | `false` | `true`이면 Second-Level Cache를 사용. `<cache>` 태그가 선언된 경우에만 유효 |
| `commandTimeout` | 아니오 | 팩토리 기본값 | Statement별 실행 타임아웃 (초). 기본값은 `NuVatisOptions.DefaultTimeout` |
| `fetchSize` | 아니오 | 드라이버 기본값 | 한 번에 가져올 행 수. 스트리밍 성능 조정에 사용 |

**resultMap vs resultType**

| 방식 | 장점 | 단점 |
|------|------|------|
| `resultMap` | 명시적 컬럼-프로퍼티 매핑, Association/Collection 지원, Source Generator 최적화 | XML 정의 필요 |
| `resultType` | 정의 없이 간단히 사용 | 런타임 ColumnMapper(리플렉션), Association/Collection 불가 |

**예제 — 단일 행 반환**

```csharp
// 인터페이스
User? GetById(int id);
```

```xml
<select id="GetById" resultMap="UserResult">
  SELECT id, user_name, email FROM users WHERE id = #{Id}
</select>
```

**예제 — 여러 행 반환**

인터페이스 메서드의 반환 타입이 `IList<T>` / `IEnumerable<T>` / `Task<IList<T>>` 이면 Source Generator가 `SelectList` 호출 코드를 생성한다.

```csharp
IList<User> GetAll();
Task<IList<User>> SearchAsync(UserSearchParam param);
```

```xml
<select id="GetAll" resultMap="UserResult">
  SELECT id, user_name, email FROM users ORDER BY id
</select>
```

**예제 — 스트리밍 반환**

인터페이스 메서드의 반환 타입이 `IAsyncEnumerable<T>`이면 `SelectStream` 호출 코드가 생성된다.

```csharp
IAsyncEnumerable<LogEntry> StreamAll(CancellationToken ct = default);
```

```xml
<select id="StreamAll" resultType="LogEntry">
  SELECT id, message, level, created_at FROM logs ORDER BY created_at
</select>
```

---

## insert

INSERT 쿼리를 정의한다. 반환값은 영향받은 행 수(`int`).

```xml
<insert id="Insert" commandTimeout="30">
  INSERT INTO users (user_name, email, created_at)
  VALUES (#{UserName}, #{Email}, #{CreatedAt})
</insert>
```

**속성**

| 속성 | 필수 | 기본값 | 설명 |
|------|------|--------|------|
| `id` | 예 | — | Statement 식별자. Mapper 메서드명과 일치 |
| `commandTimeout` | 아니오 | 팩토리 기본값 | 실행 타임아웃 (초) |

**Auto-Increment Key 반환**

일부 DB는 생성된 PK를 반환한다. `SelectOne<long>` 또는 DB별 `RETURNING` 절 활용:

```xml
<!-- PostgreSQL RETURNING 절 -->
<select id="InsertAndReturnId" resultType="long">
  INSERT INTO users (user_name, email) VALUES (#{UserName}, #{Email})
  RETURNING id
</select>
```

```csharp
long newId = mapper.InsertAndReturnId(param);
```

---

## update

UPDATE 쿼리를 정의한다. 반환값은 업데이트된 행 수(`int`).

```xml
<update id="Update">
  UPDATE users SET user_name = #{UserName}, email = #{Email}
  WHERE id = #{Id}
</update>
```

**속성**

| 속성 | 필수 | 설명 |
|------|------|------|
| `id` | 예 | Statement 식별자 |
| `commandTimeout` | 아니오 | 실행 타임아웃 (초) |

---

## delete

DELETE 쿼리를 정의한다. 반환값은 삭제된 행 수(`int`).

```xml
<delete id="Delete">
  DELETE FROM users WHERE id = #{Id}
</delete>
```

---

## resultMap

컬럼-프로퍼티 매핑 규칙을 정의한다. Source Generator가 이를 기반으로 리플렉션 없는 매핑 코드를 생성한다.

```xml
<resultMap id="UserResult" type="User" extends="BaseResult">
  <id column="user_id" property="Id" />
  <result column="user_name" property="UserName" />
  <result column="email" property="Email" />
  <result column="status" property="Status" typeHandler="EnumStringTypeHandler`1" />
  <association property="Department" resultMap="DepartmentResult" />
  <collection property="Orders" resultMap="OrderResult" ofType="Order" />
  <discriminator column="type" javaType="string">
    <case value="premium" resultMap="PremiumUserResult" />
    <case value="guest"   resultMap="GuestUserResult" />
  </discriminator>
</resultMap>
```

**속성**

| 속성 | 필수 | 설명 |
|------|------|------|
| `id` | 예 | ResultMap 식별자. `<select>`의 `resultMap` 속성에서 참조 |
| `type` | 예 | 매핑할 C# 타입명. 완전한 이름 또는 타입 별칭 |
| `extends` | 아니오 | 상속할 다른 ResultMap id. 지정된 ResultMap의 매핑 규칙을 상속 |

### id

Primary Key 컬럼을 매핑한다. `result`와 동일하게 동작하지만 Association/Collection 매핑 시 중복 제거 기준으로 사용된다.

```xml
<id column="user_id" property="Id" />
```

| 속성 | 필수 | 설명 |
|------|------|------|
| `column` | 예 | DB 컬럼명 |
| `property` | 예 | C# 프로퍼티명 |
| `javaType` | 아니오 | 타입 힌트 (대부분 불필요) |
| `typeHandler` | 아니오 | 적용할 TypeHandler 클래스명 |

### result

일반 컬럼을 프로퍼티에 매핑한다.

```xml
<result column="user_name" property="UserName" />
<result column="status" property="Status" typeHandler="EnumStringTypeHandler`1" />
```

| 속성 | 필수 | 설명 |
|------|------|------|
| `column` | 예 | DB 컬럼명 |
| `property` | 예 | C# 프로퍼티명 |
| `typeHandler` | 아니오 | 적용할 TypeHandler 클래스명. 등록된 핸들러 이름과 일치해야 한다 |

### association (1:1 관계)

1:1 관계 객체를 매핑한다.

```xml
<!-- ResultMap 참조 방식 -->
<association property="Department" resultMap="DepartmentResult" columnPrefix="dept_" />

<!-- 별도 SELECT 방식 (Lazy Loading) -->
<association property="Department"
             select="DepartmentMapper.GetById"
             column="dept_id"
             fetchType="lazy" />
```

| 속성 | 필수 | 설명 |
|------|------|------|
| `property` | 예 | 부모 객체의 C# 프로퍼티명 |
| `resultMap` | 조건부 | 참조할 ResultMap id. `select`와 중복 불가 |
| `columnPrefix` | 아니오 | ResultMap 참조 시, 이 프리픽스를 컬럼명에서 제거하여 매핑 |
| `select` | 조건부 | 추가 SELECT 실행 시 Statement id. `resultMap`과 중복 불가 |
| `column` | 조건부 | `select` 방식에서 추가 SELECT에 전달할 FK 컬럼명 |
| `fetchType` | 아니오 | `eager`(기본) 또는 `lazy`. `lazy`는 `select` 방식에서만 유효 |

### collection (1:N 관계)

1:N 관계 목록을 매핑한다.

```xml
<!-- ResultMap 참조 방식 -->
<collection property="Orders" resultMap="OrderResult" ofType="Order" />

<!-- 별도 SELECT 방식 (Lazy Loading) -->
<collection property="Orders"
            select="OrderMapper.GetByUserId"
            column="id"
            ofType="Order"
            fetchType="lazy" />
```

| 속성 | 필수 | 설명 |
|------|------|------|
| `property` | 예 | 부모 객체의 C# 컬렉션 프로퍼티명 (`List<T>`, `IList<T>`) |
| `resultMap` | 조건부 | 참조할 ResultMap id |
| `ofType` | 예 | 컬렉션 요소의 C# 타입명 |
| `select` | 조건부 | 추가 SELECT Statement id |
| `column` | 조건부 | 추가 SELECT에 전달할 FK 컬럼명 |
| `fetchType` | 아니오 | `eager`(기본) 또는 `lazy` |

### discriminator (다형성 매핑)

컬럼 값에 따라 다른 ResultMap을 적용한다.

```xml
<discriminator column="user_type" javaType="string">
  <case value="premium" resultMap="PremiumUserResult" />
  <case value="guest"   resultMap="GuestUserResult" />
</discriminator>
```

| 속성 | 필수 | 설명 |
|------|------|------|
| `column` | 예 | 판별자 컬럼명 |
| `javaType` | 예 | 컬럼의 C# 타입명 (`string`, `int` 등) |

`<case>` 속성:

| 속성 | 설명 |
|------|------|
| `value` | 판별 기준 값 |
| `resultMap` | 적용할 ResultMap id |

---

## cache

Namespace 단위 Second-Level Cache를 설정한다. 파일 최상단, 첫 번째 Statement 선언 전에 배치한다.

```xml
<cache eviction="LRU" flushInterval="600000" size="512" />
```

**속성**

| 속성 | 기본값 | 설명 |
|------|--------|------|
| `eviction` | `LRU` | 제거 정책. `LRU` (Least Recently Used)만 지원 |
| `flushInterval` | 없음 (수동만) | 자동 캐시 초기화 주기 (밀리초). 예: `600000` = 10분 |
| `size` | `1024` | 캐시에 저장할 최대 항목 수 |

**동작 규칙**

- `<select useCache="true">` 쿼리만 캐시를 사용한다
- 같은 Namespace에서 INSERT/UPDATE/DELETE가 실행되면 해당 Namespace 캐시가 자동 무효화된다
- `ICacheProvider` 구현체를 교체하여 Redis 등 외부 캐시로 전환 가능하다

---

## if

조건이 참일 때만 해당 SQL 조각을 포함한다.

```xml
<if test="UserName != null">
  AND user_name LIKE #{UserName}
</if>
```

**속성**

| 속성 | 필수 | 설명 |
|------|------|------|
| `test` | 예 | 평가할 C# 표현식. `true`이면 내부 SQL 포함. 파라미터 객체의 프로퍼티에 직접 접근 가능 |

**test 표현식 규칙**

- 파라미터가 POCO인 경우: 프로퍼티명 직접 참조 (`UserName != null`, `Age > 0`)
- 문자열 비교: `SortBy == 'name'` (작은따옴표 사용)
- null 체크: `Property != null`
- 숫자 비교: `Count > 0`, `Age >= 18`
- 복합 조건: `&&`, `||` 사용 (`Name != null && Age > 0`)

**Source Generator**는 `test` 표현식을 컴파일 타임에 검증한다. 잘못된 표현식은 NV005 오류.

---

## where

자동 WHERE 절 처리기. 내부 조건들이 모두 비어 있으면 WHERE 자체를 생성하지 않는다. 첫 조건의 선행 `AND`/`OR`을 자동 제거한다.

```xml
<where>
  <if test="UserName != null">AND user_name LIKE #{UserName}</if>
  <if test="Email != null">AND email = #{Email}</if>
</where>
```

**동작 예시**

```sql
-- UserName만 있을 때
WHERE user_name LIKE @p0

-- UserName, Email 모두 있을 때
WHERE user_name LIKE @p0 AND email = @p1

-- 모두 null일 때
(WHERE 절 없음)
```

---

## set

자동 SET 절 처리기. 동적 UPDATE에 사용한다. 내부 조건들의 후행 쉼표를 자동 제거한다.

```xml
<update id="UpdatePartial">
  UPDATE users
  <set>
    <if test="UserName != null">user_name = #{UserName},</if>
    <if test="Email != null">email = #{Email},</if>
    <if test="IsActive != null">is_active = #{IsActive},</if>
  </set>
  WHERE id = #{Id}
</update>
```

**동작 예시**

```sql
-- Email만 있을 때
UPDATE users SET email = @p0 WHERE id = @p1

-- UserName, IsActive가 있을 때
UPDATE users SET user_name = @p0, is_active = @p1 WHERE id = @p2
```

---

## foreach

컬렉션을 반복하며 SQL을 생성한다. `WHERE IN` 절, 배치 INSERT 등에 활용한다.

```xml
<foreach collection="Ids" item="id" open="(" separator="," close=")">
  #{id}
</foreach>
```

**속성**

| 속성 | 필수 | 설명 |
|------|------|------|
| `collection` | 예 | 파라미터 객체의 컬렉션 프로퍼티명 |
| `item` | 예 | 각 요소를 참조할 변수명. `#{item}` 또는 `#{item.Property}` 형태로 사용 |
| `open` | 아니오 | 반복 시작 전 출력할 문자열 |
| `separator` | 아니오 | 각 요소 사이에 삽입할 문자열 |
| `close` | 아니오 | 반복 종료 후 출력할 문자열 |
| `index` | 아니오 | 현재 인덱스를 참조할 변수명 (0부터 시작) |

**WHERE IN 예제**

```xml
<select id="GetByIds" resultMap="UserResult">
  SELECT * FROM users WHERE id IN
  <foreach collection="Ids" item="id" open="(" separator="," close=")">
    #{id}
  </foreach>
</select>
```

```csharp
var users = mapper.GetByIds(new { Ids = new[] { 1, 2, 3, 4 } });
```

**배치 INSERT 예제**

```xml
<insert id="InsertBatch">
  INSERT INTO users (user_name, email) VALUES
  <foreach collection="Users" item="u" separator=",">
    (#{u.UserName}, #{u.Email})
  </foreach>
</insert>
```

**index 활용 예제**

```xml
<foreach collection="Items" item="item" index="idx" separator=",">
  (@p#{idx}_name, @p#{idx}_value)
</foreach>
```

---

## choose / when / otherwise

Switch-case 구조의 조건 분기. 첫 번째 `true`인 `<when>` 하나만 실행된다. 모든 `<when>`이 `false`이면 `<otherwise>`가 실행된다.

```xml
<choose>
  <when test="SortBy == 'name'">user_name ASC</when>
  <when test="SortBy == 'email'">email ASC</when>
  <when test="SortBy == 'date'">created_at DESC</when>
  <otherwise>id ASC</otherwise>
</choose>
```

`<if>`는 독립적으로 평가되지만, `<choose>`는 첫 번째 매칭 조건에서 멈춘다.

---

## bind

OGNL 표현식 결과를 변수로 바인딩한다. 반복 사용되는 표현식이나 SQL 파라미터로 직접 전달하기 어려운 값을 가공할 때 유용하다.

```xml
<bind name="searchPattern" value="'%' + Keyword + '%'" />
SELECT * FROM users WHERE user_name LIKE #{searchPattern}
```

**속성**

| 속성 | 필수 | 설명 |
|------|------|------|
| `name` | 예 | 바인딩 변수명. 이후 `#{name}` 또는 `${name}`으로 참조 |
| `value` | 예 | C# 표현식. 문자열 연결(`+`), 조건 연산자 등 사용 가능 |

**LIKE 패턴 예제**

```xml
<select id="Search" resultMap="UserResult">
  <bind name="namePattern" value="'%' + Name + '%'" />
  SELECT * FROM users WHERE user_name LIKE #{namePattern}
</select>
```

---

## sql / include

재사용 가능한 SQL 조각을 정의하고 참조한다.

### sql

```xml
<sql id="userColumns">
  id, user_name, email, created_at, is_active
</sql>
```

**속성**: `id` — SQL 조각의 식별자.

### include

```xml
<select id="GetById" resultMap="UserResult">
  SELECT <include refid="userColumns" />
  FROM users WHERE id = #{Id}
</select>
```

**속성**: `refid` — 참조할 `<sql>` 태그의 `id`.

**다른 파일의 sql 참조** (cross-file include):

```xml
<!-- 다른 namespace의 sql 참조 -->
<include refid="CommonMappers.ICommonMapper.commonColumns" />
```

---

## 파라미터 문법

### #{param} — 파라미터 바인딩 (안전)

```xml
WHERE id = #{Id}
```

값이 DB 드라이버의 파라미터(`@p0`)로 전달된다. SQL Injection이 원천 차단된다.

- `#{Property}` — POCO의 프로퍼티
- `#{nested.Property}` — 중첩 객체의 프로퍼티
- `#{item}` — foreach 내부에서 현재 요소

### ${param} — 문자열 치환 (위험)

```xml
ORDER BY ${Column}
```

값이 SQL 문자열에 직접 삽입된다. v2.0.0부터 파라미터 타입이 `string`이면 **NV004 빌드 오류**. `SqlIdentifier` 타입만 허용된다.

---

## 완성된 XML 매퍼 예제

```xml
<?xml version="1.0" encoding="utf-8" ?>
<mapper namespace="MyApp.Mappers.IUserMapper">

  <!-- Second-Level Cache 설정 -->
  <cache eviction="LRU" flushInterval="600000" size="512" />

  <!-- SQL 재사용 조각 -->
  <sql id="userColumns">
    u.id, u.user_name, u.email, u.status, u.created_at
  </sql>

  <!-- ResultMap 정의 -->
  <resultMap id="UserResult" type="User">
    <id     column="id"         property="Id" />
    <result column="user_name"  property="UserName" />
    <result column="email"      property="Email" />
    <result column="status"     property="Status" typeHandler="EnumStringTypeHandler`1" />
    <result column="created_at" property="CreatedAt" />
    <association property="Profile" resultMap="ProfileResult" columnPrefix="p_" />
    <collection  property="Roles"   resultMap="RoleResult" ofType="Role" />
  </resultMap>

  <resultMap id="ProfileResult" type="UserProfile">
    <id     column="p_id"       property="Id" />
    <result column="p_bio"      property="Bio" />
    <result column="p_avatar"   property="AvatarUrl" />
  </resultMap>

  <!-- 단일 행 조회 (캐시 사용) -->
  <select id="GetById" resultMap="UserResult" useCache="true">
    SELECT <include refid="userColumns" />,
           p.id AS p_id, p.bio AS p_bio, p.avatar_url AS p_avatar
    FROM users u
    LEFT JOIN profiles p ON p.user_id = u.id
    WHERE u.id = #{Id}
  </select>

  <!-- 동적 검색 -->
  <select id="Search" resultMap="UserResult">
    SELECT <include refid="userColumns" />
    FROM users u
    <where>
      <if test="UserName != null">AND u.user_name LIKE #{UserName}</if>
      <if test="Status != null">AND u.status = #{Status}</if>
      <if test="Ids != null">
        AND u.id IN
        <foreach collection="Ids" item="id" open="(" separator="," close=")">
          #{id}
        </foreach>
      </if>
      <if test="StartDate != null">AND u.created_at >= #{StartDate}</if>
    </where>
    ORDER BY
    <choose>
      <when test="SortBy == 'name'">u.user_name</when>
      <when test="SortBy == 'date'">u.created_at DESC</when>
      <otherwise>u.id</otherwise>
    </choose>
    LIMIT #{PageSize} OFFSET #{Offset}
  </select>

  <!-- INSERT -->
  <insert id="Insert">
    INSERT INTO users (user_name, email, status, created_at)
    VALUES (#{UserName}, #{Email}, #{Status}, #{CreatedAt})
  </insert>

  <!-- 동적 UPDATE -->
  <update id="Update">
    UPDATE users
    <set>
      <if test="UserName != null">user_name = #{UserName},</if>
      <if test="Email != null">email = #{Email},</if>
      <if test="Status != null">status = #{Status},</if>
    </set>
    WHERE id = #{Id}
  </update>

  <!-- DELETE -->
  <delete id="Delete">
    DELETE FROM users WHERE id = #{Id}
  </delete>

  <!-- 배치 INSERT -->
  <insert id="InsertBatch">
    INSERT INTO users (user_name, email) VALUES
    <foreach collection="Users" item="u" separator=",">
      (#{u.UserName}, #{u.Email})
    </foreach>
  </insert>

  <!-- 동적 정렬 (SqlIdentifier 필수) -->
  <select id="GetSorted" resultMap="UserResult">
    SELECT <include refid="userColumns" /> FROM users u
    ORDER BY ${Column} ${Direction}
  </select>

</mapper>
```
