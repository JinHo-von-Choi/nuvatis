# Installation

## NuGet Packages

```bash
dotnet add package NuVatis.Core
dotnet add package NuVatis.Generators
dotnet add package NuVatis.PostgreSql          # PostgreSQL
dotnet add package NuVatis.MySql               # MySQL/MariaDB
dotnet add package NuVatis.SqlServer           # SQL Server
dotnet add package NuVatis.Sqlite              # SQLite
dotnet add package NuVatis.Extensions.DependencyInjection  # ASP.NET Core DI
```

## Optional Packages

```bash
dotnet add package NuVatis.Extensions.OpenTelemetry         # 분산 추적
dotnet add package NuVatis.Extensions.EntityFrameworkCore    # EF Core 통합
dotnet add package NuVatis.Extensions.Aspire                # .NET Aspire 통합
dotnet add package NuVatis.Testing                          # 테스트 유틸리티
```

## Requirements

- .NET 7.0 이상 (.NET 7 / .NET 8 멀티 타겟)
- C# 11+
- Roslyn 4.8+ (Source Generator 동작에 필요)

## XML Mapper 파일 설정

XML Mapper 파일은 프로젝트에 `AdditionalFiles`로 등록해야 Source Generator가 인식한다.

```xml
<!-- .csproj -->
<ItemGroup>
  <AdditionalFiles Include="Mappers/**/*.xml" />
</ItemGroup>
```

## XML Schema (IDE 자동완성)

```xml
<?xml version="1.0" encoding="utf-8" ?>
<mapper namespace="..."
        xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
        xsi:noNamespaceSchemaLocation="schemas/nuvatis-mapper.xsd">
```
