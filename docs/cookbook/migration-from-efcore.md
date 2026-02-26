# EF Core에서 NuVatis로 마이그레이션

작성자: 최진호
작성일: 2026-02-26

---

## 언제 전환을 고려하는가

EF Core는 범용 ORM으로 훌륭하지만, 다음 상황에서 NuVatis가 더 적합하다:

- 복잡한 조인, 서브쿼리, 윈도우 함수가 많은 리포트 쿼리
- LINQ로 표현하기 어렵거나 생성되는 SQL이 비효율적인 경우
- DBA가 SQL을 직접 검수/최적화해야 하는 환경
- 레거시 DB 스키마와 직접 매핑이 필요한 경우
- AOT/트리밍 배포가 필수인 환경

전체 전환보다 하이브리드 접근(CUD는 EF Core, R은 NuVatis)을 권장한다.
하이브리드 패턴은 [hybrid-efcore-nuvatis.md](./hybrid-efcore-nuvatis.md) 참조.

---

## 단계별 전환

### 1단계: 읽기 전용 쿼리부터 시작

가장 안전한 전환 경로는 복잡한 SELECT 쿼리를 먼저 NuVatis로 옮기는 것이다.

EF Core:
```csharp
var report = await context.Orders
    .Where(o => o.Status == "Completed")
    .GroupBy(o => o.CustomerId)
    .Select(g => new CustomerReport {
        CustomerId  = g.Key,
        TotalAmount = g.Sum(o => o.Amount),
        OrderCount  = g.Count()
    })
    .OrderByDescending(r => r.TotalAmount)
    .Take(100)
    .ToListAsync();
```

NuVatis:
```xml
<select id="GetTopCustomers" resultType="CustomerReport">
  SELECT
    customer_id AS CustomerId,
    SUM(amount) AS TotalAmount,
    COUNT(*)    AS OrderCount
  FROM orders
  WHERE status = 'Completed'
  GROUP BY customer_id
  ORDER BY TotalAmount DESC
  LIMIT 100
</select>
```

```csharp
var report = await session.SelectListAsync<CustomerReport>("ReportMapper.GetTopCustomers");
```

장점: SQL이 명시적이므로 DBA가 직접 검수하고 실행 계획을 최적화할 수 있다.

### 2단계: 동적 검색 조건

EF Core:
```csharp
var query = context.Products.AsQueryable();
if (!string.IsNullOrEmpty(filter.Name))
    query = query.Where(p => p.Name.Contains(filter.Name));
if (filter.MinPrice.HasValue)
    query = query.Where(p => p.Price >= filter.MinPrice.Value);
if (filter.CategoryId.HasValue)
    query = query.Where(p => p.CategoryId == filter.CategoryId.Value);
var products = await query.ToListAsync();
```

NuVatis:
```xml
<select id="Search" resultType="Product">
  SELECT * FROM products
  <where>
    <if test="Name != null and Name != ''">
      AND name ILIKE '%' || @Name || '%'
    </if>
    <if test="MinPrice != null">
      AND price >= @MinPrice
    </if>
    <if test="CategoryId != null">
      AND category_id = @CategoryId
    </if>
  </where>
</select>
```

### 3단계: CUD 작업 전환 (선택적)

단순 CRUD는 EF Core가 편리하다. 전환은 선택적이다.

EF Core:
```csharp
context.Users.Add(new User { Name = "Jinho", Email = "jinho@example.com" });
await context.SaveChangesAsync();
```

NuVatis:
```xml
<insert id="Insert">
  INSERT INTO users (name, email)
  VALUES (@Name, @Email)
  RETURNING id
</insert>
```

### 4단계: 마이그레이션 관리

NuVatis는 스키마 마이그레이션 도구를 제공하지 않는다.
기존 EF Core Migrations를 계속 사용하거나, FluentMigrator, DbUp 등 독립 마이그레이션 도구를 사용한다.

```bash
# EF Core Migrations 계속 사용 가능
dotnet ef migrations add AddNewTable
dotnet ef database update
```

---

## EF Core vs NuVatis 비교

| 기능 | EF Core | NuVatis |
|------|---------|---------|
| 패러다임 | ORM (객체 중심) | SQL Mapper (SQL 중심) |
| SQL 제어 | LINQ -> 자동 생성 | 직접 작성 |
| Change Tracking | 지원 | 없음 |
| 마이그레이션 | 내장 | 없음 (외부 도구 사용) |
| 동적 SQL | LINQ 조합 | XML 태그 |
| 컴파일 타임 검증 | Compiled Models (.NET 8) | Source Generator |
| 학습 곡선 | 중간 | 중간 (SQL 지식 필수) |
| 성능 (읽기) | 보통 | 우수 (직접 SQL) |
| AOT 호환 | .NET 8+ 부분 지원 | 완전 지원 |
