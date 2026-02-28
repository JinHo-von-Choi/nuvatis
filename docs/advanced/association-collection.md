# Advanced Mapping: Association & Collection

작성자: 최진호
작성일: 2026-03-01

1:1 관계(Association), 1:N 관계(Collection), 다형성 매핑(Discriminator), 지연 로딩(Lazy Loading)의 상세 가이드.

---

## Association (1:1 관계)

하나의 엔티티에 연관된 다른 엔티티 하나를 함께 로드한다.

### 방식 1: JOIN + ResultMap 참조 (권장)

단일 쿼리로 연관 데이터를 함께 가져온다. N+1 문제 없음. Source Generator가 빌드타임에 최적화된 매핑 코드를 생성한다.

**스키마**

```sql
CREATE TABLE users (
    id       BIGINT PRIMARY KEY,
    user_name VARCHAR(100),
    dept_id   BIGINT REFERENCES departments(id)
);

CREATE TABLE departments (
    id         BIGINT PRIMARY KEY,
    dept_name  VARCHAR(100),
    location   VARCHAR(100)
);
```

**ResultMap 정의**

`columnPrefix`를 활용하면 컬럼 이름 충돌 없이 매핑할 수 있다.

```xml
<resultMap id="DeptResult" type="Department">
  <id     column="d_id"       property="Id" />
  <result column="d_name"     property="DeptName" />
  <result column="d_location" property="Location" />
</resultMap>

<resultMap id="UserWithDeptResult" type="User">
  <id     column="id"        property="Id" />
  <result column="user_name" property="UserName" />
  <association property="Department" resultMap="DeptResult" />
</resultMap>

<select id="GetWithDept" resultMap="UserWithDeptResult">
  SELECT u.id, u.user_name,
         d.id AS d_id, d.dept_name AS d_name, d.location AS d_location
  FROM users u
  LEFT JOIN departments d ON d.id = u.dept_id
  WHERE u.id = #{Id}
</select>
```

**C# 모델**

```csharp
public class User {
    public long        Id         { get; set; }
    public string      UserName   { get; set; } = "";
    public Department? Department { get; set; }  // nullable: LEFT JOIN
}

public class Department {
    public long   Id       { get; set; }
    public string DeptName { get; set; } = "";
    public string Location { get; set; } = "";
}
```

**인터페이스**

```csharp
[NuVatisMapper]
public interface IUserMapper {
    User? GetWithDept(long id);
}
```

### 방식 2: SELECT 방식 (Lazy Loading)

연관 데이터를 별도 쿼리로 가져온다. `fetchType="lazy"` 설정 시 프로퍼티에 처음 접근하는 시점에 쿼리가 실행된다.

```xml
<!-- 부서 조회 Statement -->
<select id="GetById" resultMap="DeptResult">
  SELECT id, dept_name, location FROM departments WHERE id = #{DeptId}
</select>

<!-- 사용자 ResultMap - 부서를 별도 SELECT로 로드 -->
<resultMap id="UserWithLazyDeptResult" type="User">
  <id     column="id"        property="Id" />
  <result column="user_name" property="UserName" />
  <result column="dept_id"   property="DeptId" />
  <association property="Department"
               select="MyApp.Mappers.IDeptMapper.GetById"
               column="dept_id"
               fetchType="lazy" />
</resultMap>
```

**C# 모델 (Lazy Loading 지원)**

`LazyValue<T>` 래퍼로 지연 로딩을 표현한다.

```csharp
public class User {
    public long              Id         { get; set; }
    public string            UserName   { get; set; } = "";
    public long              DeptId     { get; set; }
    public LazyValue<Department?> Department { get; set; } = default!;
}

// 사용
var user = mapper.GetById(1);
var dept = user.Department.Value;  // 이 시점에 DB 쿼리 실행 (lazy)
```

**언제 Lazy Loading을 사용하는가**

- 연관 데이터가 필요한 경우와 필요하지 않은 경우가 혼재
- JOIN이 부담스러울 정도로 연관 테이블이 많을 때
- 대부분의 경우에는 JOIN 방식이 성능상 유리하다 (쿼리 1회 vs 2회)

---

## Collection (1:N 관계)

하나의 엔티티에 연관된 여러 엔티티 목록을 함께 로드한다.

### 방식 1: JOIN + ResultMap 참조 (권장)

ResultMap의 `<id>` 태그를 기반으로 중복 행을 자동 합산하여 컬렉션을 구성한다.

**스키마**

```sql
CREATE TABLE users (id BIGINT PRIMARY KEY, user_name VARCHAR(100));
CREATE TABLE orders (
    id      BIGINT PRIMARY KEY,
    user_id BIGINT REFERENCES users(id),
    amount  DECIMAL(10, 2),
    status  VARCHAR(20)
);
```

**ResultMap 정의**

```xml
<resultMap id="OrderResult" type="Order">
  <id     column="o_id"     property="Id" />
  <result column="o_amount" property="Amount" />
  <result column="o_status" property="Status" />
</resultMap>

<resultMap id="UserWithOrdersResult" type="User">
  <id     column="id"        property="Id" />
  <result column="user_name" property="UserName" />
  <collection property="Orders" resultMap="OrderResult" ofType="Order" />
</resultMap>

<select id="GetWithOrders" resultMap="UserWithOrdersResult">
  SELECT u.id, u.user_name,
         o.id AS o_id, o.amount AS o_amount, o.status AS o_status
  FROM users u
  LEFT JOIN orders o ON o.user_id = u.id
  WHERE u.id = #{Id}
</select>
```

**C# 모델**

```csharp
public class User {
    public long        Id       { get; set; }
    public string      UserName { get; set; } = "";
    public List<Order> Orders   { get; set; } = new();
}

public class Order {
    public long    Id     { get; set; }
    public decimal Amount { get; set; }
    public string  Status { get; set; } = "";
}
```

**중복 제거 메커니즘**: 조인 결과에서 `User.Id`가 동일한 행은 같은 User로 처리되고, `Order`들이 `Orders` 리스트에 누적된다.

```
// DB 결과 (JOIN)          → // 매핑 후
id | user_name | o_id       User { Id=1, UserName="jinho",
1  | jinho     | 10           Orders = [
1  | jinho     | 11                      {Id=10, Amount=50},
1  | jinho     | 12                      {Id=11, Amount=100},
                                         {Id=12, Amount=200}] }
```

### 방식 2: SELECT 방식 (Lazy Loading)

```xml
<resultMap id="UserLazyOrdersResult" type="User">
  <id     column="id"        property="Id" />
  <result column="user_name" property="UserName" />
  <collection property="Orders"
              select="MyApp.Mappers.IOrderMapper.GetByUserId"
              column="id"
              ofType="Order"
              fetchType="lazy" />
</resultMap>
```

### 다중 레벨 중첩 매핑

User → Orders → OrderItems 구조:

```xml
<resultMap id="OrderItemResult" type="OrderItem">
  <id     column="oi_id"       property="Id" />
  <result column="oi_product"  property="ProductName" />
  <result column="oi_quantity" property="Quantity" />
</resultMap>

<resultMap id="OrderWithItemsResult" type="Order">
  <id     column="o_id"     property="Id" />
  <result column="o_amount" property="Amount" />
  <collection property="Items" resultMap="OrderItemResult" ofType="OrderItem" />
</resultMap>

<resultMap id="UserWithOrdersAndItemsResult" type="User">
  <id     column="id"        property="Id" />
  <result column="user_name" property="UserName" />
  <collection property="Orders" resultMap="OrderWithItemsResult" ofType="Order" />
</resultMap>

<select id="GetWithOrdersAndItems" resultMap="UserWithOrdersAndItemsResult">
  SELECT u.id, u.user_name,
         o.id AS o_id, o.amount AS o_amount,
         oi.id AS oi_id, oi.product_name AS oi_product, oi.quantity AS oi_quantity
  FROM users u
  LEFT JOIN orders o ON o.user_id = u.id
  LEFT JOIN order_items oi ON oi.order_id = o.id
  WHERE u.id = #{Id}
</select>
```

---

## Discriminator (다형성 매핑)

컬럼 값에 따라 서로 다른 C# 타입(또는 ResultMap)으로 매핑한다. 상속 계층 또는 다형 엔티티에 활용한다.

### 예제: 다형 알림 타입

```sql
CREATE TABLE notifications (
    id      BIGINT PRIMARY KEY,
    type    VARCHAR(20),    -- 'email', 'sms', 'push'
    title   VARCHAR(200),
    email   VARCHAR(100),   -- type='email'일 때
    phone   VARCHAR(20),    -- type='sms'일 때
    device_token VARCHAR(200)  -- type='push'일 때
);
```

**C# 모델 계층**

```csharp
public abstract class Notification {
    public long   Id    { get; set; }
    public string Title { get; set; } = "";
}

public class EmailNotification : Notification {
    public string Email { get; set; } = "";
}

public class SmsNotification : Notification {
    public string Phone { get; set; } = "";
}

public class PushNotification : Notification {
    public string DeviceToken { get; set; } = "";
}
```

**ResultMap 정의**

```xml
<resultMap id="BaseNotificationResult" type="Notification">
  <id     column="id"    property="Id" />
  <result column="title" property="Title" />
  <discriminator column="type" javaType="string">
    <case value="email" resultMap="EmailNotificationResult" />
    <case value="sms"   resultMap="SmsNotificationResult" />
    <case value="push"  resultMap="PushNotificationResult" />
  </discriminator>
</resultMap>

<resultMap id="EmailNotificationResult" type="EmailNotification" extends="BaseNotificationResult">
  <result column="email" property="Email" />
</resultMap>

<resultMap id="SmsNotificationResult" type="SmsNotification" extends="BaseNotificationResult">
  <result column="phone" property="Phone" />
</resultMap>

<resultMap id="PushNotificationResult" type="PushNotification" extends="BaseNotificationResult">
  <result column="device_token" property="DeviceToken" />
</resultMap>

<select id="GetAll" resultMap="BaseNotificationResult">
  SELECT id, type, title, email, phone, device_token FROM notifications
</select>
```

**사용**

```csharp
IList<Notification> notifications = mapper.GetAll();

foreach (var n in notifications) {
    switch (n) {
        case EmailNotification email:
            Console.WriteLine($"Email → {email.Email}");
            break;
        case SmsNotification sms:
            Console.WriteLine($"SMS → {sms.Phone}");
            break;
        case PushNotification push:
            Console.WriteLine($"Push → {push.DeviceToken}");
            break;
    }
}
```

---

## ResultMap 상속 (extends)

공통 필드를 Base ResultMap에 정의하고 재사용한다.

```xml
<!-- Base: 공통 감사 필드 -->
<resultMap id="AuditResult" type="AuditBase">
  <result column="created_at"  property="CreatedAt" />
  <result column="updated_at"  property="UpdatedAt" />
  <result column="created_by"  property="CreatedBy" />
</resultMap>

<!-- User: AuditResult 상속 -->
<resultMap id="UserResult" type="User" extends="AuditResult">
  <id     column="id"        property="Id" />
  <result column="user_name" property="UserName" />
  <result column="email"     property="Email" />
</resultMap>
```

---

## columnPrefix 활용

JOIN 쿼리에서 컬럼명 충돌을 방지한다. `columnPrefix`를 지정하면 해당 프리픽스를 제거한 뒤 연관 ResultMap의 컬럼명과 매칭한다.

```xml
<resultMap id="DepartmentResult" type="Department">
  <id     column="id"   property="Id" />     <!-- 프리픽스 제거 후: id -->
  <result column="name" property="Name" />   <!-- 프리픽스 제거 후: name -->
</resultMap>

<resultMap id="UserResult" type="User">
  <id     column="user_id"   property="Id" />
  <result column="user_name" property="UserName" />
  <!-- columnPrefix="dept_" → DB 컬럼 "dept_id", "dept_name"을 DepartmentResult의 "id", "name"으로 매핑 -->
  <association property="Department" resultMap="DepartmentResult" columnPrefix="dept_" />
</resultMap>

<select id="GetWithDept" resultMap="UserResult">
  SELECT u.id    AS user_id,
         u.name  AS user_name,
         d.id    AS dept_id,    <!-- dept_ 프리픽스 -->
         d.name  AS dept_name   <!-- dept_ 프리픽스 -->
  FROM users u
  JOIN departments d ON d.id = u.dept_id
  WHERE u.id = #{Id}
</select>
```

---

## 성능 가이드라인

| 시나리오 | 권장 방식 | 이유 |
|---------|----------|------|
| 항상 연관 데이터가 필요한 경우 | JOIN + resultMap | 쿼리 1회, N+1 없음 |
| 연관 데이터가 가끔만 필요한 경우 | SELECT + lazy | 불필요한 쿼리 회피 |
| 깊은 중첩 (3단계 이상) | 단계별 별도 쿼리 | JOIN 결과 크기 제어 |
| 대용량 컬렉션 | SELECT + lazy 또는 별도 쿼리 | 카테시안 곱 방지 |

**카테시안 곱 주의**: User 1명에 Orders 100개, 각 Order에 Items 10개가 있을 때 단일 3-way JOIN은 1,000행의 결과를 반환한다. 이 경우 단계별 쿼리가 더 효율적이다.
