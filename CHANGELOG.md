# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0-alpha.1] - 2026-02-25

### Added

- Core runtime: ISqlSession, SqlSessionFactory, SimpleExecutor, ParameterBinder, ColumnMapper
- XML Mapper parser with dynamic SQL tags (if, choose/when/otherwise, where, set, foreach, sql/include)
- Roslyn Source Generator: compile-time proxy generation, mapper registry, attribute-based SQL
- ResultMap: explicit column-to-property mapping with association/collection support
- [NuVatisMapper] attribute for explicit opt-in Source Generator scanning
- NV004 compile-time warning for ${} string substitution (SQL injection risk)
- Diagnostic codes NV001-NV006 for compile-time validation
- PostgreSQL provider (Npgsql)
- MySQL provider (MySqlConnector)
- SQL Server provider (Microsoft.Data.SqlClient)
- Microsoft DI integration (AddNuVatis, Scoped ISqlSession)
- ASP.NET Core Health Check (AddNuVatis for IHealthChecksBuilder)
- OpenTelemetry distributed tracing (ActivitySource "NuVatis.SqlSession")
- Prometheus metrics via System.Diagnostics.Metrics (MetricsInterceptor)
- EF Core integration: DbConnection/DbTransaction sharing (AddNuVatisEntityFrameworkCore)
- IAsyncEnumerable streaming (SelectStream)
- Multi-ResultSet support (SelectMultiple, ResultSetGroup)
- Second-Level Cache: namespace-scoped LRU with auto-invalidation on writes
- Command timeout per statement
- External connection/transaction sharing (FromExistingConnection)
- Interceptor pipeline (Before/After with elapsed time, exception context)
- Lazy connection acquisition (first query triggers connection open)
- Thread safety guard (Interlocked-based concurrent access detection)
- autoCommit mode with automatic rollback on uncommitted dispose
- ExecuteInTransactionAsync helper
- InMemorySqlSession and QueryCapture for unit testing
- XML Schema files (nuvatis-mapper.xsd, nuvatis-config.xsd) for IDE auto-completion
- Custom DB provider support via IDbProvider
- .NET 7.0 / .NET 8.0 multi-targeting
- Native AOT compatibility (.NET 8)
- pack.sh packaging script (build, test, pack, verify 9 packages)
- DocFX documentation site structure with cookbook and security guides
- GitHub Actions CI matrix (2 OS x 2 .NET x 3 DB)
- GitHub Actions Trusted Publishing workflow (OIDC 기반 NuGet.org 자동 배포, API 키 불필요)

### Changed

- Renamed AutoMapper to ColumnMapper to avoid naming confusion with AutoMapper NuGet package
- Refactored SqlSession: extracted ExecuteTimed/ExecuteTimedAsync to eliminate Stopwatch+Interceptor code duplication across 8 methods

### Fixed

- Source Generator scanning conflict with AutoMapper: replaced suffix-based global scan with [NuVatisMapper] attribute opt-in mechanism

### Security

- #{} parameter binding as default (SQL injection prevention)
- ${} string substitution detected at compile-time with NV004 warning
- Security documentation with whitelist validation guide for unavoidable ${} usage

## Packages

| Package | Version |
|---------|---------|
| NuVatis.Core | 0.1.0-alpha.1 |
| NuVatis.Generators | 0.1.0-alpha.1 |
| NuVatis.PostgreSql | 0.1.0-alpha.1 |
| NuVatis.MySql | 0.1.0-alpha.1 |
| NuVatis.SqlServer | 0.1.0-alpha.1 |
| NuVatis.Extensions.DependencyInjection | 0.1.0-alpha.1 |
| NuVatis.Extensions.OpenTelemetry | 0.1.0-alpha.1 |
| NuVatis.Extensions.EntityFrameworkCore | 0.1.0-alpha.1 |
| NuVatis.Testing | 0.1.0-alpha.1 |
