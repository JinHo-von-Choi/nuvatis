# EF Core + NuVatis 하이브리드 패턴

작성자: 최진호
작성일: 2026-02-26

---

## 개요

EF Core와 NuVatis를 동일 프로젝트에서 함께 사용하는 CQRS 하이브리드 패턴.

- Command (Create, Update, Delete): EF Core의 Change Tracking, Migrations 활용
- Query (Read): NuVatis의 직접 SQL 제어, ResultMap 매핑, 2차 캐시 활용

핵심은 동일 DbConnection과 DbTransaction을 공유하는 것이다.

---

## 설정

### 1. 패키지 설치

```bash
dotnet add package NuVatis.Core
dotnet add package NuVatis.Generators
dotnet add package NuVatis.PostgreSql
dotnet add package NuVatis.Extensions.DependencyInjection
dotnet add package NuVatis.Extensions.EntityFrameworkCore
```

### 2. DI 등록

```csharp
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddNuVatis(options => {
    options.ConnectionString = connectionString;
    options.Provider         = new PostgreSqlProvider();
    options.RegisterMappers(NuVatisMapperRegistry.RegisterAll);
});

builder.Services.AddNuVatisEntityFrameworkCore<AppDbContext>();
```

---

## 트랜잭션 공유 패턴

### 패턴 1: EF Core 트랜잭션을 NuVatis가 공유

```csharp
public class OrderService {
    private readonly AppDbContext _context;
    private readonly ISqlSessionFactory _sessionFactory;

    public async Task CreateOrderAsync(OrderRequest request) {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try {
            var order = new Order { /* ... */ };
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            var dbConnection = _context.Database.GetDbConnection();
            await using var session = _sessionFactory.FromExistingConnection(dbConnection);

            await session.InsertAsync("AuditMapper.LogAction", new {
                Action   = "OrderCreated",
                EntityId = order.Id
            });

            await transaction.CommitAsync();
        } catch {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
```

### 패턴 2: NuVatisEfCoreExtensions 사용 (권장)

`AddNuVatisEntityFrameworkCore`를 등록하면 자동으로 DbContext의 커넥션을 공유한다.

```csharp
public class ReportService {
    private readonly AppDbContext _context;
    private readonly ISqlSession _session;

    public ReportService(AppDbContext context, ISqlSession session) {
        _context = context;
        _session = session;
    }

    public async Task<DashboardData> GetDashboardAsync() {
        var recentOrders = await _context.Orders
            .OrderByDescending(o => o.CreatedAt)
            .Take(10)
            .ToListAsync();

        var salesReport = await _session.SelectListAsync<MonthlySales>(
            "ReportMapper.GetMonthlySales");

        var topProducts = await _session.SelectListAsync<TopProduct>(
            "ReportMapper.GetTopProducts", new { Limit = 5 });

        return new DashboardData {
            RecentOrders = recentOrders,
            MonthlySales = salesReport,
            TopProducts  = topProducts
        };
    }
}
```

---

## 아키텍처 가이드

### 레이어별 역할 분리

```
Controller / API Layer
    |
    v
Application Service (Use Case)
    |
    +-- EF Core Repository (CUD)
    |       -> DbContext, Change Tracking
    |       -> Migrations 관리
    |
    +-- NuVatis Mapper (Query)
            -> XML SQL, ResultMap
            -> 복잡한 JOIN, 리포트
            -> 2차 캐시
```

### 디렉토리 구조 예시

```
src/MyApp/
  Infrastructure/
    EfCore/
      AppDbContext.cs
      Configurations/    # EF Core Entity 설정
    NuVatis/
      Mappers/           # XML 매퍼 파일
        UserMapper.xml
        ReportMapper.xml
      Interfaces/        # Mapper 인터페이스
        IUserQueryMapper.cs
        IReportMapper.cs
  Domain/
    Entities/            # EF Core + NuVatis 공유 도메인 모델
    DTOs/                # NuVatis 전용 읽기 모델
```

### 네이밍 컨벤션

- EF Core Repository: `IUserRepository`, `UserRepository`
- NuVatis Mapper: `IUserQueryMapper`, `IReportMapper`
- EF Core Entity: `User`, `Order` (도메인 모델)
- NuVatis DTO: `UserSummaryDto`, `SalesReportDto` (읽기 전용 프로젝션)

---

## 주의사항

1. 동일 테이블에 대해 EF Core와 NuVatis가 동시에 쓰기를 하면 Change Tracking이 깨질 수 있다. 쓰기는 하나의 경로로 통일한다.

2. EF Core의 `AsNoTracking()`을 읽기 전용 쿼리에 사용하면 NuVatis와 유사한 성능을 얻을 수 있다. 단순한 읽기는 굳이 NuVatis로 옮기지 않아도 된다.

3. 마이그레이션은 EF Core Migrations로 통합 관리한다. NuVatis는 스키마를 변경하지 않는다.

4. 테스트 시 EF Core는 InMemory Provider, NuVatis는 `InMemorySqlSession`을 사용한다.
