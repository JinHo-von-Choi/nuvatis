# NuVatis Documentation

MyBatis-style SQL Mapper for .NET, powered by Roslyn Source Generators.

## What is NuVatis?

NuVatis는 Entity Framework의 성능 오버헤드와 인라인 SQL의 유지보수성 문제를 동시에 해결하는 SQL Mapper 프레임워크다.

- SQL은 XML 또는 C# Attribute로 별도 관리
- Roslyn Source Generator가 빌드타임에 매핑 코드를 자동 생성
- 런타임 리플렉션 제로, Native AOT 호환 (.NET 8)
- ADO.NET 기반 최소 추상화, 최대 성능
- PostgreSQL, MySQL, SQL Server, SQLite 멀티 DB 지원

## When to Use NuVatis

| Use Case | NuVatis | EF Core |
|----------|--------|---------|
| 통계/집계 쿼리 | Optimal | Overhead |
| CRUD + 비즈니스 로직 | Good | Optimal |
| 복잡한 JOIN/서브쿼리 | Optimal | Difficult |
| 대용량 데이터 스트리밍 | IAsyncEnumerable | Limited |
| Native AOT | Full support | Partial |

CQRS 패턴에서 Command(CUD)는 EF Core, Query(R)는 NuVatis로 분리하는 것이 이상적이다.

## Quick Links

- [Installation](getting-started/installation.md)
- [Quick Start](getting-started/quick-start.md)
- [Cookbook](cookbook/crud-operations.md)
- [Security Guide](security/sql-injection-prevention.md)
- [API Reference](api/public-api-reference.md)
- [Migration from Dapper](cookbook/migration-from-dapper.md)
- [Migration from EF Core](cookbook/migration-from-efcore.md)
- [Hybrid EF Core + NuVatis](cookbook/hybrid-efcore-nuvatis.md)
- [CHANGELOG](../CHANGELOG.md)
