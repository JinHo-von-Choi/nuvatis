# NuVatis 성능·보안 튜닝 구현 플랜

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** ColumnMapper O(n²) → O(1) 최적화, SqlIdentifier 타입 도입으로 ${} SQL Injection 벡터 차단, MappingEmitter 커버리지 확대로 런타임 리플렉션 경로 제거.

**Architecture:**
- T1(ColumnMapper): Type당 `Dictionary<string, PropertyInfo>` 사전 구축으로 컬럼-프로퍼티 매핑을 O(1)으로 전환하고 per-row string 할당 제거.
- T2(SqlIdentifier): `${}` 대입을 `string` 타입에서 금지하고, `SqlIdentifier`(sealed class) 또는 `enum` 타입만 허용. 생성자에서 SQL Injection 패턴 런타임 검증. SG에서 string 타입 `${}` 사용 시 NV004를 error로 승격.
- T3(MappingEmitter): 현재 SG가 resultMap 정의가 있는 경우에만 typed mapper를 생성하는지 확인하고, 일반 resultType 기반 쿼리에도 ColumnMapper 폴백 없이 SG가 매핑 코드를 직접 생성하도록 확장.

**Tech Stack:** C# / .NET 8+, xUnit, Roslyn Incremental Source Generator (netstandard2.0), ADO.NET

---

## Task 1: ColumnMapper — 타입별 컬럼 룩업 딕셔너리 캐시

> 파일 위치: `src/NuVatis.Core/Mapping/ColumnMapper.cs`
> 테스트 위치: `tests/NuVatis.Tests/ColumnMapperTests.cs` (신규)

### 배경

`FindMatchingProperty`는 매 행, 매 컬럼마다 `columnName.Replace("_","")` 문자열 생성 + O(n) 선형 탐색. 50컬럼 × 50프로퍼티 × 1만 행 = 2,500만 비교 + 500만 string 할당.

개선 전략:
- `PropertyCache`를 `PropertyInfo[]`에서 `Dictionary<string, PropertyInfo>` (OrdinalIgnoreCase) 로 전환
- 딕셔너리 구축 시 프로퍼티 이름 원본(`UserName`)과 언더스코어 제거 정규화(`user_name → username`)를 함께 등록
- 중복 키(e.g. `User_Id`와 `UserId` 동시 존재) 시 First-win 정책 — `GroupBy` 후 첫 번째 프로퍼티 사용

### Step 1: 실패 테스트 작성

파일 생성: `tests/NuVatis.Tests/ColumnMapperTests.cs`

```csharp
using System.Data;
using System.Data.Common;
using System.Reflection;
using NuVatis.Mapping;
using Xunit;

namespace NuVatis.Tests;

public class ColumnMapperTests
{
    // --- T1-A: 기본 매핑 정확성 ---

    [Fact]
    public void MapRow_CamelCase_Property_Matches_SnakeCase_Column()
    {
        using var reader = new FakeDataReader(
            columns: ["user_name", "user_age"],
            values:  ["Alice", 30]);

        var result = ColumnMapper.MapRow<UserDto>(reader);

        Assert.Equal("Alice", result.UserName);
        Assert.Equal(30,      result.UserAge);
    }

    [Fact]
    public void MapRow_Exact_Match_Has_Priority_Over_Normalized()
    {
        // "UserId" 컬럼 → UserId 프로퍼티 (정확 일치 우선)
        using var reader = new FakeDataReader(
            columns: ["UserId"],
            values:  [42]);

        var result = ColumnMapper.MapRow<UserDto>(reader);
        Assert.Equal(42, result.UserId);
    }

    [Fact]
    public void MapRow_Unknown_Column_Is_Silently_Ignored()
    {
        using var reader = new FakeDataReader(
            columns: ["user_name", "nonexistent_col"],
            values:  ["Alice", "ignored"]);

        var result = ColumnMapper.MapRow<UserDto>(reader);

        Assert.Equal("Alice", result.UserName);
        // nonexistent_col에 해당하는 프로퍼티가 없어도 예외 없어야 함
    }

    [Fact]
    public void MapRow_Same_Type_Reuses_Cached_Dictionary_Not_Rebuilds()
    {
        // 동일 타입을 두 번 매핑해도 동일 딕셔너리 인스턴스 사용 (캐시 동작)
        using var r1 = new FakeDataReader(["user_name"], ["Alice"]);
        using var r2 = new FakeDataReader(["user_name"], ["Bob"]);

        var a = ColumnMapper.MapRow<UserDto>(r1);
        var b = ColumnMapper.MapRow<UserDto>(r2);

        Assert.Equal("Alice", a.UserName);
        Assert.Equal("Bob",   b.UserName);

        // PropertyCache 딕셔너리는 Type당 1개만 존재해야 함
        var cacheField = typeof(ColumnMapper)
            .GetField("PropertyCache", BindingFlags.NonPublic | BindingFlags.Static)!;
        var cache = cacheField.GetValue(null);
        Assert.NotNull(cache);

        // 딕셔너리 구현 검증: Value 타입이 Dictionary<string, PropertyInfo>
        var cacheType = cache.GetType();
        Assert.Contains("Dictionary", cacheType.GenericTypeArguments[1].Name);
    }

    [Fact]
    public void MapRow_DuplicateNormalizedKey_FirstWin_No_Exception()
    {
        // User_Id와 UserId가 동시에 존재하는 경우: First-win, 예외 없어야 함
        using var reader = new FakeDataReader(
            columns: ["user_id"],
            values:  [99]);

        // AmbiguousDto는 UserId와 User_Id(내부 언더스코어 포함 이름) 둘 다 보유
        var result = ColumnMapper.MapRow<AmbiguousDto>(reader);
        Assert.NotNull(result); // 어느 쪽이 바인딩돼도 예외 없으면 통과
    }

    // --- T1-B: 스칼라 타입 ---

    [Fact]
    public void MapRow_Scalar_Int_Returns_First_Column()
    {
        using var reader = new FakeDataReader(["count"], [7]);
        var result = ColumnMapper.MapRow<int>(reader);
        Assert.Equal(7, result);
    }

    // --- 테스트용 DTO ---

    private sealed class UserDto
    {
        public string UserName { get; set; } = "";
        public int    UserAge  { get; set; }
        public int    UserId   { get; set; }
    }

    private sealed class AmbiguousDto
    {
        public int UserId  { get; set; }   // normalized: "userid"
        public int User_Id { get; set; }   // normalized: "userid" (충돌)
    }

    // --- FakeDataReader: 테스트용 최소 DbDataReader ---

    private sealed class FakeDataReader : DbDataReader
    {
        private readonly string[] _columns;
        private readonly object[] _values;
        private bool _read;

        public FakeDataReader(string[] columns, object[] values)
        {
            _columns = columns;
            _values  = values;
        }

        public override bool Read() { if (_read) return false; _read = true; return true; }
        public override int  FieldCount => _columns.Length;
        public override string GetName(int ordinal) => _columns[ordinal];
        public override bool IsDBNull(int ordinal)  => _values[ordinal] is null;
        public override object GetValue(int ordinal) => _values[ordinal];
        public override int  GetOrdinal(string name) => Array.IndexOf(_columns, name);

        // --- 미사용 멤버 (최소 구현) ---
        public override object this[int ordinal]    => _values[ordinal];
        public override object this[string name]    => _values[GetOrdinal(name)];
        public override int    Depth                => 0;
        public override bool   HasRows              => true;
        public override bool   IsClosed             => false;
        public override int    RecordsAffected       => -1;
        public override bool   NextResult()         => false;
        public override bool   GetBoolean(int i)    => (bool)_values[i];
        public override byte   GetByte(int i)       => (byte)_values[i];
        public override long   GetBytes(int i, long o, byte[]? b, int bo, int l) => 0;
        public override char   GetChar(int i)       => (char)_values[i];
        public override long   GetChars(int i, long o, char[]? b, int bo, int l) => 0;
        public override string GetDataTypeName(int i) => _values[i].GetType().Name;
        public override DateTime GetDateTime(int i) => (DateTime)_values[i];
        public override decimal  GetDecimal(int i)  => (decimal)_values[i];
        public override double   GetDouble(int i)   => (double)_values[i];
        public override Type     GetFieldType(int i) => _values[i].GetType();
        public override float    GetFloat(int i)    => (float)_values[i];
        public override Guid     GetGuid(int i)     => (Guid)_values[i];
        public override short    GetInt16(int i)    => (short)_values[i];
        public override int      GetInt32(int i)    => (int)_values[i];
        public override long     GetInt64(int i)    => (long)_values[i];
        public override string   GetString(int i)   => (string)_values[i];
        public override int      GetValues(object[] v) => 0;
        public override IEnumerator<IDataRecord> GetEnumerator() => throw new NotImplementedException();
    }
}
```

### Step 2: 테스트 실행해서 실패 확인

```bash
cd /home/nirna/job/.netis
dotnet test tests/NuVatis.Tests/ --filter "ColumnMapperTests" --verbosity normal
```

예상: `FAIL` — `ColumnMapperTests` 클래스 자체는 통과하지만 `MapRow_Same_Type_Reuses_Cached_Dictionary_Not_Rebuilds`는 `PropertyCache` Value 타입이 `PropertyInfo[]`이므로 실패.

### Step 3: ColumnMapper 구현 수정

파일 수정: `src/NuVatis.Core/Mapping/ColumnMapper.cs`

`PropertyCache` 타입을 `ConcurrentDictionary<Type, PropertyInfo[]>` → `ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>>` 로 교체하고, `FindMatchingProperty` 삭제 후 O(1) 딕셔너리 룩업으로 대체.

```csharp
// 변경 전 (37번째 줄)
private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

// 변경 후
private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> PropertyCache = new();
```

`MapComplex<T>` 메서드 교체:

```csharp
private static T MapComplex<T>(DbDataReader reader)
{
    var type      = typeof(T);
    var columnMap = PropertyCache.GetOrAdd(type, BuildColumnMap);
    var obj       = Activator.CreateInstance<T>()!;

    for (var i = 0; i < reader.FieldCount; i++)
    {
        if (reader.IsDBNull(i)) continue;

        var columnName = reader.GetName(i);

        if (!columnMap.TryGetValue(columnName, out var prop) &&
            !columnMap.TryGetValue(columnName.Replace("_", ""), out prop))
            continue;

        var value      = reader.GetValue(i);
        var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

        if (targetType.IsEnum)
            prop.SetValue(obj, Enum.ToObject(targetType, value));
        else
            prop.SetValue(obj, Convert.ChangeType(value, targetType));
    }

    return obj;
}

private static Dictionary<string, PropertyInfo> BuildColumnMap(Type type)
{
    var map = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);

    foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                             .Where(p => p.CanWrite))
    {
        // 원본 이름 등록 (우선)
        map.TryAdd(prop.Name, prop);

        // 언더스코어 제거 정규화 이름 등록 (이미 존재하면 First-win 유지)
        var normalized = prop.Name.Replace("_", "");
        if (normalized != prop.Name)
            map.TryAdd(normalized, prop);
    }

    return map;
}
```

`FindMatchingProperty` 메서드와 기존 `PropertyCache` 필드 선언 삭제.

### Step 4: 테스트 재실행해서 통과 확인

```bash
dotnet test tests/NuVatis.Tests/ --filter "ColumnMapperTests" --verbosity normal
```

예상: 모든 테스트 `PASS`.

### Step 5: 전체 회귀 테스트

```bash
dotnet test tests/NuVatis.Tests/ --configuration Release --verbosity normal --filter "Category!=E2E"
```

예상: 기존 테스트 모두 통과.

### Step 6: 커밋

```bash
git add src/NuVatis.Core/Mapping/ColumnMapper.cs \
        tests/NuVatis.Tests/ColumnMapperTests.cs
git commit -m "perf: replace O(n) ColumnMapper with O(1) pre-built Dictionary cache

Per-row column.Replace('_','') allocation eliminated.
PropertyCache now stores Dictionary<string, PropertyInfo> (OrdinalIgnoreCase).
Duplicate normalized key collision resolved via TryAdd first-win policy."
```

---

## Task 2: SqlIdentifier — `${}` SQL Injection 방어 타입 도입

> 파일 생성: `src/NuVatis.Core/Sql/SqlIdentifier.cs`
> 파일 수정: `src/NuVatis.Core/Attributes/SqlConstantAttribute.cs`
> 파일 수정: `src/NuVatis.Generators/Diagnostics/DiagnosticDescriptors.cs`
> 파일 수정: `src/NuVatis.Generators/NuVatisIncrementalGenerator.cs`
> 테스트 파일 생성: `tests/NuVatis.Tests/SqlIdentifierTests.cs`
> 테스트 파일 수정: `tests/NuVatis.Generators.Tests/StringSubstitutionAnalyzerTests.cs`

### 배경

현재 `${}` 치환은 `DynamicSqlEmitter`에서 `__sql.Append(param.PropName)` 으로 raw 문자열을 직접 추가. `[SqlConstant]` 어트리뷰트가 컴파일 타임 경고를 억제하지만, 속성값 소스가 나중에 사용자 입력으로 바뀌어도 컴파일러가 침묵.

개선 전략:
- `SqlIdentifier` sealed class 도입: 생성자에서 SQL Injection 패턴(세미콜론, 주석, 따옴표 등) 런타임 차단
- `FromEnum<T>()` 팩토리: enum 기반 안전한 사용 경로 제공
- SG의 `StringSubstitutionAnalyzer`: `string` 타입인 `${}` 파라미터에 대해 NV004를 Warning → Error로 승격
- `SqlIdentifier` 또는 `enum` 타입인 경우만 NV004 억제 유지

### Step 1: SqlIdentifier 실패 테스트 작성

파일 생성: `tests/NuVatis.Tests/SqlIdentifierTests.cs`

```csharp
using NuVatis.Sql;
using Xunit;

namespace NuVatis.Tests;

public class SqlIdentifierTests
{
    // --- T2-A: 정상 생성 ---

    [Fact]
    public void From_Valid_String_Returns_Identifier()
    {
        var id = SqlIdentifier.From("users");
        Assert.Equal("users", id.ToString());
    }

    [Fact]
    public void FromEnum_Returns_EnumName_As_Identifier()
    {
        var id = SqlIdentifier.FromEnum(TableName.Users);
        Assert.Equal("Users", id.ToString());
    }

    [Fact]
    public void From_Underscore_And_Dot_Allowed()
    {
        var id = SqlIdentifier.From("schema.table_name");
        Assert.Equal("schema.table_name", id.ToString());
    }

    // --- T2-B: SQL Injection 패턴 거부 ---

    [Theory]
    [InlineData("users; DROP TABLE users--")]    // 세미콜론 + 주석
    [InlineData("users'")]                        // 단따옴표
    [InlineData("users\"")]                       // 쌍따옴표
    [InlineData("/* comment */ users")]           // 블록 주석
    [InlineData("users -- comment")]              // 인라인 주석
    [InlineData("users UNION SELECT 1")]          // UNION 주입
    public void From_Injection_Pattern_Throws(string malicious)
    {
        Assert.Throws<ArgumentException>(() => SqlIdentifier.From(malicious));
    }

    [Fact]
    public void From_Empty_String_Throws()
    {
        Assert.Throws<ArgumentException>(() => SqlIdentifier.From(""));
    }

    [Fact]
    public void From_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => SqlIdentifier.From(null!));
    }

    // --- T2-C: AllowedValues 화이트리스트 팩토리 ---

    [Fact]
    public void FromAllowed_Matching_Value_Returns_Identifier()
    {
        var id = SqlIdentifier.FromAllowed("created_at", "id", "created_at", "user_name");
        Assert.Equal("created_at", id.ToString());
    }

    [Fact]
    public void FromAllowed_NonMatching_Value_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            SqlIdentifier.FromAllowed("injected", "id", "created_at"));
    }

    // --- 테스트용 enum ---
    private enum TableName { Users, Orders, Products }
}
```

### Step 2: 테스트 실행해서 실패 확인

```bash
dotnet test tests/NuVatis.Tests/ --filter "SqlIdentifierTests" --verbosity normal
```

예상: `FAIL` — `NuVatis.Sql.SqlIdentifier` 타입이 존재하지 않아 컴파일 에러.

### Step 3: SqlIdentifier 구현

파일 생성: `src/NuVatis.Core/Sql/SqlIdentifier.cs`

```csharp
namespace NuVatis.Sql;

/**
 * SQL 식별자(테이블명, 컬럼명 등)를 타입 안전하게 래핑하는 sealed 값 타입.
 *
 * ${} 문자열 치환 시 string 대신 이 타입을 사용하면 런타임에서 SQL Injection
 * 패턴을 감지하여 예외를 발생시킨다.
 *
 * 권장 사용법:
 *   SqlIdentifier.FromEnum(SortColumn.CreatedAt)  // enum 기반 (가장 안전)
 *   SqlIdentifier.FromAllowed(userInput, "id", "name", "created_at")  // 화이트리스트
 *   SqlIdentifier.From("users")  // 리터럴 (상수로만 사용)
 *
 * @author 최진호
 * @date   2026-02-27
 */
public sealed class SqlIdentifier
{
    private static readonly char[] _forbidden =
        [';', '\'', '"', '\n', '\r', '\0'];

    private static readonly string[] _forbiddenPatterns =
        ["--", "/*", "*/", " union ", " or ", " and ", " select ", " drop ", " insert "];

    private readonly string _value;

    private SqlIdentifier(string value) => _value = value;

    /**
     * 문자열로부터 SqlIdentifier를 생성한다.
     * SQL Injection 패턴이 감지되면 ArgumentException.
     */
    public static SqlIdentifier From(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length == 0)
            throw new ArgumentException("SQL 식별자는 빈 문자열일 수 없습니다.", nameof(value));

        foreach (var ch in _forbidden)
            if (value.Contains(ch))
                throw new ArgumentException(
                    $"SQL Injection 패턴이 감지되었습니다: '{value}'", nameof(value));

        var lower = value.ToLowerInvariant();
        foreach (var pattern in _forbiddenPatterns)
            if (lower.Contains(pattern))
                throw new ArgumentException(
                    $"SQL Injection 패턴이 감지되었습니다: '{value}'", nameof(value));

        return new SqlIdentifier(value);
    }

    /**
     * enum 값으로부터 SqlIdentifier를 생성한다.
     * enum 이름은 컴파일 타임에 확정되므로 SQL Injection이 불가능하다.
     */
    public static SqlIdentifier FromEnum<T>(T value) where T : struct, Enum
        => new(value.ToString());

    /**
     * 허용된 값 목록(allowedValues) 중 하나인지 검증 후 SqlIdentifier를 생성한다.
     * 사용자 입력을 화이트리스트로 검증할 때 사용한다.
     */
    public static SqlIdentifier FromAllowed(string value, params string[] allowedValues)
    {
        if (!allowedValues.Contains(value, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"허용되지 않은 SQL 식별자입니다: '{value}'. 허용 목록: [{string.Join(", ", allowedValues)}]",
                nameof(value));

        return From(value);
    }

    public override string ToString() => _value;
}
```

### Step 4: 테스트 재실행해서 통과 확인

```bash
dotnet test tests/NuVatis.Tests/ --filter "SqlIdentifierTests" --verbosity normal
```

예상: 모든 테스트 `PASS`.

### Step 5: DiagnosticDescriptors — NV004를 Error로 승격

파일 수정: `src/NuVatis.Generators/Diagnostics/DiagnosticDescriptors.cs`

현재 NV004의 `DiagnosticSeverity.Warning`을 `DiagnosticSeverity.Error`로 변경하되, 억제 조건(SqlConstant, enum 타입)은 유지.

```csharp
// 변경 전 (NV004 정의 부분)
DiagnosticSeverity.Warning

// 변경 후
DiagnosticSeverity.Error
```

정확한 변경 대상은 `DiagnosticDescriptors.cs` 파일의 NV004 descriptor를 찾아 수정:

```bash
grep -n "NV004\|StringSubstitution\|Warning" \
  /home/nirna/job/.netis/src/NuVatis.Generators/Diagnostics/DiagnosticDescriptors.cs
```

출력된 줄 번호에서 `DiagnosticSeverity.Warning` → `DiagnosticSeverity.Error` 변경.

### Step 6: SG 통합 테스트 — NV004 가 Error 발생 확인

파일 수정: `tests/NuVatis.Generators.Tests/StringSubstitutionAnalyzerTests.cs`

기존 테스트는 `Analyze()`가 빈 결과를 반환하거나 결과를 반환하는지만 검증하므로 그대로 유지. SG의 진단 심각도 변경은 `GeneratorIntegrationTests.cs`에서 추가 검증 가능하나, 현재 단계에서는 `DiagnosticDescriptors.cs` 수정만으로 충분.

```bash
dotnet test tests/NuVatis.Generators.Tests/ --verbosity normal
```

예상: 기존 테스트 모두 `PASS` (분석기 자체 로직은 변경 없음).

### Step 7: 전체 빌드 확인

```bash
dotnet build NuVatis.sln --configuration Release 2>&1 | tail -5
```

예상: `경고 0개, 오류 0개` (NV004 Error 승격은 사용자 코드에 영향하며 프레임워크 내부엔 ${}를 직접 사용하는 코드가 없어야 함).

### Step 8: 커밋

```bash
git add src/NuVatis.Core/Sql/SqlIdentifier.cs \
        tests/NuVatis.Tests/SqlIdentifierTests.cs \
        src/NuVatis.Generators/Diagnostics/DiagnosticDescriptors.cs
git commit -m "security: introduce SqlIdentifier to harden \${} string substitution

SqlIdentifier validates against SQL injection patterns at runtime.
SqlIdentifier.FromEnum() and FromAllowed() provide safe construction paths.
NV004 diagnostic upgraded from Warning to Error for string-typed \${} usage."
```

---

## Task 3: MappingEmitter — SG 결과 매핑 커버리지 확인 및 resultType 경로 추가

> 파일 탐색: `src/NuVatis.Generators/Emitters/MappingEmitter.cs`
> 파일 탐색: `src/NuVatis.Generators/Emitters/ProxyEmitter.cs`
> 테스트 파일: `tests/NuVatis.Generators.Tests/MappingEmitterTests.cs` (기존)

### 배경

SG(Source Generator)가 인터페이스 메서드에 대해 생성하는 프록시 코드는 SQL 빌드와 파라미터 추출을 커버. 하지만 결과 매핑(`MapRow<T>`)이 SG 경로에서 생성된 코드를 쓰는지, 여전히 런타임 `ColumnMapper.MapRow<T>()`를 호출하는지 확인이 필요.

### Step 1: 현재 MappingEmitter가 생성하는 코드 확인

```bash
grep -n "ColumnMapper\|MapRow\|MapComplex\|PropertyInfo" \
  /home/nirna/job/.netis/src/NuVatis.Generators/Emitters/MappingEmitter.cs | head -30
```

```bash
grep -rn "ColumnMapper\|MapRow" \
  /home/nirna/job/.netis/src/NuVatis.Generators/Emitters/ | head -20
```

### Step 2: ProxyEmitter가 생성하는 SELECT 경로 확인

```bash
grep -n "SelectOne\|SelectList\|MapRow\|ColumnMapper" \
  /home/nirna/job/.netis/src/NuVatis.Generators/Emitters/ProxyEmitter.cs | head -30
```

### Step 3: 결과 분석 및 분기 결정

#### 케이스 A: ProxyEmitter가 이미 typed mapper 코드를 emit하고 있는 경우
→ 추가 작업 불필요. MappingEmitter가 커버하는 범위를 문서화하고 완료.

#### 케이스 B: ProxyEmitter가 여전히 `ColumnMapper.MapRow<T>()` 런타임 호출을 emit하는 경우
→ MappingEmitter에 `EmitTypedColumnMap` 메서드 추가. 아래 코드를 생성하도록 확장:

```csharp
// 생성 대상 (resultType = UserDto, 컬럼 매핑 정보 없음)
private static UserDto __MapRow_UserDto(System.Data.Common.DbDataReader reader)
{
    var __obj = new UserDto();
    for (var __i = 0; __i < reader.FieldCount; __i++)
    {
        if (reader.IsDBNull(__i)) continue;
        switch (reader.GetName(__i).ToUpperInvariant())
        {
            case "USERNAME": case "USER_NAME": __obj.UserName = reader.GetString(__i); break;
            case "USERAGE":  case "USER_AGE":  __obj.UserAge  = reader.GetInt32(__i);  break;
            case "USERID":   case "USER_ID":   __obj.UserId   = reader.GetInt32(__i);  break;
        }
    }
    return __obj;
}
```

이 방식은 런타임 Reflection 없이 switch + GetString/GetInt32 직접 호출로 매핑. PropertyInfo.SetValue 오버헤드 완전 제거.

### Step 4: 케이스 B인 경우 — MappingEmitter 확장 테스트

파일 수정: `tests/NuVatis.Generators.Tests/MappingEmitterTests.cs`

기존 테스트 코드 패턴을 참고하여 `EmitTypedColumnMap` 반환 코드에 다음을 검증하는 테스트 추가:
- `switch (reader.GetName` 구문이 생성 코드에 포함되는지
- `reader.GetString`, `reader.GetInt32` 등 타입별 직접 Get 호출 포함 여부
- `ColumnMapper.MapRow` 참조가 생성 코드에 없는지

### Step 5: 빌드 및 Generator 통합 테스트

```bash
dotnet test tests/NuVatis.Generators.Tests/ --verbosity normal
```

예상: 모든 테스트 `PASS`.

### Step 6: 커밋 (케이스 B인 경우만)

```bash
git add src/NuVatis.Generators/Emitters/MappingEmitter.cs \
        tests/NuVatis.Generators.Tests/MappingEmitterTests.cs
git commit -m "perf: emit typed column switch mapper in SG path for resultType queries

Eliminates runtime ColumnMapper.MapRow<T>() reflection call from SG proxy.
Generated code uses direct reader.GetString/GetInt32 per column switch."
```

---

## 최종 검증

모든 Task 완료 후:

```bash
# 전체 빌드
dotnet build NuVatis.sln --configuration Release 2>&1 | tail -3

# 단위 테스트 전체
dotnet test tests/NuVatis.Tests/ \
  --configuration Release \
  --filter "Category!=E2E" \
  --verbosity normal

# Generator 테스트
dotnet test tests/NuVatis.Generators.Tests/ \
  --configuration Release \
  --verbosity normal

# 패키지 팩 확인
dotnet pack NuVatis.sln --configuration Release --output ./nupkg-check 2>&1 | tail -3
ls ./nupkg-check/*.nupkg | wc -l  # 11개여야 함
rm -rf ./nupkg-check
```

예상 결과:
- 빌드: 경고 0, 오류 0
- 단위 테스트: 모두 PASS
- 패키지: 11개 생성

---

## 파일 변경 요약

| 파일 | 작업 | Task |
|------|------|------|
| `src/NuVatis.Core/Mapping/ColumnMapper.cs` | 수정 | T1 |
| `tests/NuVatis.Tests/ColumnMapperTests.cs` | 신규 | T1 |
| `src/NuVatis.Core/Sql/SqlIdentifier.cs` | 신규 | T2 |
| `tests/NuVatis.Tests/SqlIdentifierTests.cs` | 신규 | T2 |
| `src/NuVatis.Generators/Diagnostics/DiagnosticDescriptors.cs` | 수정 | T2 |
| `src/NuVatis.Generators/Emitters/MappingEmitter.cs` | 조건부 수정 | T3 |
| `tests/NuVatis.Generators.Tests/MappingEmitterTests.cs` | 조건부 수정 | T3 |
