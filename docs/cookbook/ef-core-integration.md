# EF Core Integration Cookbook

## 왜 EF Core와 함께 사용하는가?

CQRS 패턴에서:
- Command (CUD): EF Core의 Change Tracking, 마이그레이션 활용
- Query (R): NuVatis의 직접 SQL 제어, 최적 성능 확보

EF Core와 NuVatis가 동일 트랜잭션을 공유하면 두 프레임워크의 장점을 모두 활용할 수 있다.

## 설정

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddNuVatis(options => {
    options.ConnectionString = connectionString;
    options.Provider         = new PostgreSqlProvider();
    options.RegisterMappers(NuVatisMapperRegistry.RegisterAll);
});

builder.Services.AddNuVatisEntityFrameworkCore<AppDbContext>();
```

## 트랜잭션 공유

EF Core의 DbContext에서 NuVatis 세션을 열면 동일 DbConnection/DbTransaction을 공유한다.

```csharp
public class OrderService {
    private readonly AppDbContext _dbContext;
    private readonly ISqlSessionFactory _sessionFactory;

    public async Task PlaceOrder(Order order) {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync();

        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        await using var nuvatisSession = await _dbContext.OpenNuVatisSessionAsync(_sessionFactory);
        await nuvatisSession.InsertAsync("OrderStats.UpdateDailyCount",
            new { Date = DateTime.UtcNow.Date });

        await transaction.CommitAsync();
    }
}
```

EF Core의 엔티티 저장과 NuVatis의 통계 업데이트가 하나의 트랜잭션 안에서 원자적으로 처리된다.

## 외부 커넥션 공유 (Non-DI)

```csharp
var connection  = dbContext.Database.GetDbConnection();
var transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();

using var session = factory.FromExistingConnection(connection, transaction);
var stats = session.SelectList<MonthlyStats>("Stats.GetMonthly");
```

커넥션 수명 관리는 EF Core(외부)에 위임된다. NuVatis는 커넥션을 닫거나 반환하지 않는다.

## 실무 패턴: Read Model과 Write Model 분리

```csharp
public class ProductService {
    private readonly AppDbContext _dbContext;
    private readonly IProductStatsMapper _statsMapper;

    public async Task<Product> CreateProduct(CreateProductDto dto) {
        var product = new Product { Name = dto.Name, Price = dto.Price };
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync();
        return product;
    }

    public async Task<ProductDashboard> GetDashboard(int categoryId) {
        return await _statsMapper.GetDashboardAsync(categoryId);
    }
}
```

Write는 EF Core, Read(복합 조회)는 NuVatis Mapper로 분리한다.
