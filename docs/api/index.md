# NuVatis API Reference

이 섹션은 DocFX의 `metadata` 기능으로 C# 소스의 XML 문서 주석에서 자동 생성된다.

## 주요 네임스페이스

| 네임스페이스 | 설명 |
|-------------|------|
| `NuVatis.Session` | ISqlSession, ISqlSessionFactory 등 세션 관리 |
| `NuVatis.Configuration` | NuVatisConfiguration, DataSourceConfig 등 설정 |
| `NuVatis.Mapping` | ColumnMapper, ResultMapper, TypeHandler 등 매핑 |
| `NuVatis.Binding` | ParameterBinder 등 파라미터 바인딩 |
| `NuVatis.Cache` | ICacheProvider, MemoryCacheProvider 등 캐시 |
| `NuVatis.Interceptor` | ISqlInterceptor, InterceptorPipeline 등 인터셉터 |
| `NuVatis.Provider` | IDbProvider, DbProviderRegistry 등 DB 프로바이더 |
| `NuVatis.Attributes` | NuVatisMapper, Select, Insert 등 어트리뷰트 |
| `NuVatis.Transaction` | ITransaction, AdoTransaction 등 트랜잭션 |
| `NuVatis.Statement` | MappedStatement, StatementType 등 SQL 문 정의 |
| `NuVatis.Testing` | InMemorySqlSession, QueryCapture 등 테스트 지원 |

## 확장 패키지

| 패키지 | 네임스페이스 |
|--------|-------------|
| NuVatis.Extensions.DependencyInjection | `Microsoft.Extensions.DependencyInjection` (확장 메서드) |
| NuVatis.Extensions.OpenTelemetry | `NuVatis.Extensions.OpenTelemetry` |
| NuVatis.Extensions.EntityFrameworkCore | `NuVatis.Extensions.EntityFrameworkCore` |
| NuVatis.PostgreSql | `NuVatis.Provider.PostgreSql` |
| NuVatis.MySql | `NuVatis.Provider.MySql` |
| NuVatis.SqlServer | `NuVatis.Provider.SqlServer` |

---

`docfx metadata` 명령으로 이 디렉토리에 YAML API 문서가 자동 생성된다.
