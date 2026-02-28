# Custom TypeHandler 작성 가이드

작성자: 최진호
작성일: 2026-03-01

내장 TypeHandler로 처리되지 않는 타입에 대해 커스텀 변환 로직을 작성하는 방법.

---

## 언제 TypeHandler가 필요한가

| 상황 | 예시 |
|------|------|
| DB에 JSON으로 저장된 복합 타입 | `Tags VARCHAR` → `List<string>` |
| DB에 문자열로 저장된 Enum | `status VARCHAR` → `OrderStatus` |
| DB에 int로 저장된 Boolean | `is_active INT` → `bool` |
| DB에 BLOB으로 저장된 값 | `binary_data BYTEA` → `MyStruct` |
| 커스텀 직렬화 형식 | `data VARCHAR` → `CompressedData` |

---

## ITypeHandler 인터페이스

```csharp
public interface ITypeHandler
{
    Type    TargetType { get; }
    object? GetValue(DbDataReader reader, int ordinal);
    void    SetParameter(DbParameter parameter, object? value);
}
```

| 멤버 | 설명 |
|------|------|
| `TargetType` | 이 핸들러가 처리하는 .NET 타입 |
| `GetValue` | DB → .NET 변환. `reader.IsDBNull(ordinal)` 체크 필수 |
| `SetParameter` | .NET → DB 변환. `parameter.Value` 설정 |

---

## 예제 1: JSON TypeHandler

복합 타입을 JSON 문자열로 직렬화/역직렬화한다.

```csharp
using System.Data.Common;
using System.Text.Json;
using NuVatis.Mapping;

public sealed class JsonTypeHandler<T> : ITypeHandler
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Type TargetType => typeof(T);

    public object? GetValue(DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            return default(T);

        var json = reader.GetString(ordinal);
        return JsonSerializer.Deserialize<T>(json, _options);
    }

    public void SetParameter(DbParameter parameter, object? value)
    {
        if (value is null)
        {
            parameter.Value = DBNull.Value;
            return;
        }

        parameter.Value = JsonSerializer.Serialize(value, _options);
    }
}
```

**등록 및 사용**

```csharp
// DI 등록
builder.Services.AddNuVatis(options => {
    options.RegisterTypeHandler<List<string>>(new JsonTypeHandler<List<string>>());
    options.RegisterTypeHandler<UserPreferences>(new JsonTypeHandler<UserPreferences>());
});
```

```xml
<!-- ResultMap에서 TypeHandler 지정 -->
<resultMap id="UserResult" type="User">
  <id     column="id"          property="Id" />
  <result column="tags"        property="Tags"        typeHandler="JsonTypeHandler`1" />
  <result column="preferences" property="Preferences" typeHandler="JsonTypeHandler`1" />
</resultMap>
```

```csharp
// C# 모델
public class User {
    public long             Id          { get; set; }
    public List<string>     Tags        { get; set; } = new();
    public UserPreferences  Preferences { get; set; } = new();
}
```

---

## 예제 2: Enum String TypeHandler

Enum 값을 DB에 문자열 이름으로 저장한다.

```csharp
public sealed class EnumStringTypeHandler<T> : ITypeHandler where T : struct, Enum
{
    public Type TargetType => typeof(T);

    public object? GetValue(DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            return default(T);

        var str = reader.GetString(ordinal);
        return Enum.Parse<T>(str, ignoreCase: true);
    }

    public void SetParameter(DbParameter parameter, object? value)
    {
        parameter.Value = value is T enumVal
            ? enumVal.ToString()
            : DBNull.Value;
    }
}
```

**사용 예제**

```csharp
public enum OrderStatus { Pending, Processing, Shipped, Delivered, Cancelled }

// 등록
options.RegisterTypeHandler<OrderStatus>(new EnumStringTypeHandler<OrderStatus>());
```

```xml
<result column="status" property="Status" typeHandler="EnumStringTypeHandler`1" />
```

```sql
-- DB에 저장: 'Pending', 'Shipped' 등 문자열
```

---

## 예제 3: PostgreSQL-specific TypeHandler

PostgreSQL의 `HSTORE` 타입을 `Dictionary<string, string>`으로 변환한다.

```csharp
using Npgsql;

public sealed class HstoreTypeHandler : ITypeHandler
{
    public Type TargetType => typeof(Dictionary<string, string>);

    public object? GetValue(DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            return null;

        // Npgsql이 HSTORE를 Dictionary로 자동 변환
        return reader.GetFieldValue<Dictionary<string, string>>(ordinal);
    }

    public void SetParameter(DbParameter parameter, object? value)
    {
        if (parameter is NpgsqlParameter npgsqlParam)
            npgsqlParam.NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Hstore;

        parameter.Value = value ?? DBNull.Value;
    }
}
```

---

## 예제 4: Value Object TypeHandler

도메인 Value Object를 DB 원시 타입과 변환한다.

```csharp
// Value Object
public record Money(decimal Amount, string Currency)
{
    public override string ToString() => $"{Amount}:{Currency}";

    public static Money Parse(string value)
    {
        var parts  = value.Split(':');
        return new Money(decimal.Parse(parts[0]), parts[1]);
    }
}

// TypeHandler
public sealed class MoneyTypeHandler : ITypeHandler
{
    public Type TargetType => typeof(Money);

    public object? GetValue(DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            return null;

        var raw = reader.GetString(ordinal);
        return Money.Parse(raw);
    }

    public void SetParameter(DbParameter parameter, object? value)
    {
        parameter.Value = value is Money money
            ? money.ToString()
            : DBNull.Value;
    }
}
```

**DB 스키마**

```sql
CREATE TABLE orders (
    id     BIGINT PRIMARY KEY,
    amount VARCHAR(50)  -- "100.00:USD" 형태로 저장
);
```

---

## 예제 5: DateOnly TypeHandler (.NET 6+)

`DateOnly` 타입을 DB `DATE` 컬럼과 변환한다. 내장 `DateOnlyTypeHandler`가 이미 제공되므로 참고용으로만 사용한다.

```csharp
public sealed class DateOnlyTypeHandler : ITypeHandler
{
    public Type TargetType => typeof(DateOnly);

    public object? GetValue(DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            return null;

        var dt = reader.GetDateTime(ordinal);
        return DateOnly.FromDateTime(dt);
    }

    public void SetParameter(DbParameter parameter, object? value)
    {
        parameter.Value = value is DateOnly date
            ? date.ToDateTime(TimeOnly.MinValue)
            : DBNull.Value;
    }
}
```

---

## TypeHandler 등록 방법

### DI 방식 (권장)

```csharp
builder.Services.AddNuVatis(options => {
    // 내장 핸들러
    options.RegisterTypeHandler<DateOnly>(new DateOnlyTypeHandler());
    options.RegisterTypeHandler<TimeOnly>(new TimeOnlyTypeHandler());

    // 커스텀 핸들러
    options.RegisterTypeHandler<List<string>>(new JsonTypeHandler<List<string>>());
    options.RegisterTypeHandler<OrderStatus>(new EnumStringTypeHandler<OrderStatus>());
    options.RegisterTypeHandler<Money>(new MoneyTypeHandler());
});
```

### Non-DI 방식

```csharp
var factory = new SqlSessionFactoryBuilder()
    .RegisterTypeHandler<DateOnly>(new DateOnlyTypeHandler())
    .RegisterTypeHandler<Money>(new MoneyTypeHandler())
    .Build();
```

---

## XML ResultMap에서 TypeHandler 지정

```xml
<resultMap id="OrderResult" type="Order">
  <id     column="id"     property="Id" />
  <result column="amount" property="Amount"     typeHandler="MoneyTypeHandler" />
  <result column="status" property="Status"     typeHandler="EnumStringTypeHandler`1" />
  <result column="tags"   property="Tags"       typeHandler="JsonTypeHandler`1" />
  <result column="meta"   property="Metadata"   typeHandler="HstoreTypeHandler" />
</resultMap>
```

**typeHandler 값 규칙**

- 클래스명 또는 완전한 이름 (`NuVatis.Mapping.DateOnlyTypeHandler`)
- 제네릭 타입은 `` ` `` 뒤에 타입 파라미터 수 표기: `JsonTypeHandler`1`

---

## 주의사항

1. `GetValue`에서 반드시 `reader.IsDBNull(ordinal)` 체크 후 `null` 또는 기본값 반환
2. `SetParameter`에서 `null` 값은 `DBNull.Value`로 설정
3. TypeHandler는 Thread-safe하게 설계해야 한다 (싱글턴으로 등록됨)
4. 예외 발생 시 구체적인 메시지 포함 (`ArgumentException`, `FormatException` 등)
5. 성능이 중요한 경우 `ObjectPool<T>` 또는 재사용 가능한 버퍼 사용 고려
