namespace NuVatis.Sql;

/**
 * SQL 식별자(테이블명, 컬럼명 등)를 타입 안전하게 래핑하는 sealed 클래스.
 *
 * ${} 문자열 치환 시 string 대신 이 타입을 사용하면 런타임에서 SQL Injection
 * 패턴을 감지하여 즉시 예외를 발생시킨다.
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
        new char[] { ';', '\'', '"', '\n', '\r', '\0' };

    private static readonly string[] _forbiddenPatterns =
        new string[] { "--", "/*", "*/", " union ", " or ", " and ", " select ", " drop ", " insert " };

    private readonly string _value;

    private SqlIdentifier(string value) => _value = value;

    /**
     * 문자열로부터 SqlIdentifier를 생성한다.
     * SQL Injection 패턴이 감지되면 ArgumentException을 발생시킨다.
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
