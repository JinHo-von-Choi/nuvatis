namespace NuVatis.Core.Sql;

/// <summary>
/// SQL 식별자(테이블명, 컬럼명 등)를 타입 안전하게 래핑하는 sealed 클래스.
/// <para>
/// <c>${}</c> 문자열 치환 시 <see cref="string"/> 대신 이 타입을 사용하면 런타임에서 SQL Injection
/// 패턴을 감지하여 즉시 예외를 발생시킨다.
/// </para>
/// <para>권장 사용법:</para>
/// <list type="bullet">
///   <item><see cref="FromEnum{T}"/> — enum 기반 (가장 안전)</item>
///   <item><see cref="FromAllowed"/> — 화이트리스트 기반 사용자 입력 검증</item>
///   <item><see cref="From"/> — 리터럴 상수 (컴파일 타임에 확정된 값만 사용)</item>
/// </list>
/// </summary>
public sealed class SqlIdentifier
{
    private static readonly char[] _forbidden =
        new char[] { ';', '\'', '"', '\n', '\r', '\0' };

    private static readonly string[] _forbiddenSequences = new string[] { "--", "/*", "*/" };

    private static readonly System.Text.RegularExpressions.Regex _forbiddenKeywords =
        new(@"\b(union|select|drop|insert|or|and)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.Compiled);

    private readonly string _value;

    private SqlIdentifier(string value) => _value = value;

    /// <summary>
    /// 문자열로부터 <see cref="SqlIdentifier"/>를 생성한다.
    /// SQL Injection 패턴이 감지되면 <see cref="ArgumentException"/>을 발생시킨다.
    /// </summary>
    /// <param name="value">SQL 식별자로 사용할 문자열. 빈 문자열 불가.</param>
    /// <returns>검증된 <see cref="SqlIdentifier"/> 인스턴스.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/>가 null인 경우.</exception>
    /// <exception cref="ArgumentException">빈 문자열이거나 SQL Injection 패턴이 감지된 경우.</exception>
    public static SqlIdentifier From(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Length == 0)
            throw new ArgumentException("SQL 식별자는 빈 문자열일 수 없습니다.", nameof(value));

        // Check forbidden characters
        foreach (var ch in _forbidden)
            if (value.Contains(ch))
                throw new ArgumentException(
                    $"SQL Injection 패턴이 감지되었습니다: '{value}'", nameof(value));

        // Check forbidden sequences (comments)
        foreach (var seq in _forbiddenSequences)
            if (value.Contains(seq, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    $"SQL Injection 패턴이 감지되었습니다: '{value}'", nameof(value));

        // Check forbidden SQL keywords (word-boundary aware)
        if (_forbiddenKeywords.IsMatch(value))
            throw new ArgumentException(
                $"SQL Injection 패턴이 감지되었습니다: '{value}'", nameof(value));

        return new SqlIdentifier(value);
    }

    /// <summary>
    /// enum 값으로부터 <see cref="SqlIdentifier"/>를 생성한다.
    /// enum 이름은 컴파일 타임에 확정되므로 SQL Injection이 불가능하다.
    /// </summary>
    /// <typeparam name="T">enum 타입. Flags enum 조합은 지원하지 않는다.</typeparam>
    /// <param name="value">SQL 식별자로 사용할 enum 값.</param>
    /// <returns>검증된 <see cref="SqlIdentifier"/> 인스턴스.</returns>
    /// <exception cref="ArgumentException">Flags enum 조합 값이 전달된 경우.</exception>
    public static SqlIdentifier FromEnum<T>(T value) where T : struct, Enum
    {
        var name = value.ToString();
        // Flags enum combinations produce "Read, Write" — reject these
        if (name.Contains(','))
            throw new ArgumentException(
                "Flags enum 조합은 SQL 식별자로 사용할 수 없습니다. 단일 enum 값을 사용하세요.",
                nameof(value));
        return new SqlIdentifier(name);
    }

    /// <summary>
    /// 허용된 값 목록(<paramref name="allowedValues"/>) 중 하나인지 검증 후 <see cref="SqlIdentifier"/>를 생성한다.
    /// 사용자 입력을 화이트리스트로 검증할 때 사용한다.
    /// </summary>
    /// <param name="value">검증할 사용자 입력 문자열.</param>
    /// <param name="allowedValues">허용된 식별자 목록. 대소문자를 구분하지 않는다.</param>
    /// <returns>검증된 <see cref="SqlIdentifier"/> 인스턴스.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/>가 <paramref name="allowedValues"/>에 없거나 SQL Injection 패턴이 감지된 경우.</exception>
    public static SqlIdentifier FromAllowed(string value, params string[] allowedValues)
    {
        if (!allowedValues.Contains(value, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"허용되지 않은 SQL 식별자입니다: '{value}'. 허용 목록: [{string.Join(", ", allowedValues)}]",
                nameof(value));

        return From(value);
    }

    /// <inheritdoc/>
    public override string ToString() => _value;

    /// <summary>
    /// struct 제약 타입 컬렉션을 SQL WHERE IN 절에 안전하게 인라인할 수 있는
    /// 쉼표 구분 문자열로 변환한다.
    /// </summary>
    /// <remarks>
    /// struct 제약으로 컴파일타임에 임의 문자열 입력이 차단되므로
    /// SQL Injection 위험이 없다.
    ///
    /// Guid, DateTime, DateTimeOffset, DateOnly, TimeOnly는 따옴표로 감싸고,
    /// 숫자형(int, long, decimal 등)은 그대로 출력한다.
    ///
    /// 빈 컬렉션은 SQL 오류를 유발하므로 <see cref="ArgumentException"/>을 발생시킨다.
    /// </remarks>
    /// <param name="values">WHERE IN 절에 인라인할 struct 타입 컬렉션.</param>
    /// <typeparam name="T">struct 제약 타입. 문자열은 사용 불가.</typeparam>
    /// <returns>쉼표로 구분된 SQL 리터럴 문자열.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="values"/>가 null인 경우.</exception>
    /// <exception cref="ArgumentException"><paramref name="values"/>가 비어 있는 경우.</exception>
    /// <example>
    /// <code>
    /// var clause = SqlIdentifier.JoinTyped(new List&lt;int&gt; { 1, 2, 3 });
    /// // → "1,2,3"
    /// // SQL: $"SELECT * FROM t WHERE id IN ({clause})"
    /// </code>
    /// </example>
    public static string JoinTyped<T>(IEnumerable<T> values) where T : struct
    {
        ArgumentNullException.ThrowIfNull(values);

        var list = values as IList<T> ?? values.ToList();
        if (list.Count == 0)
            throw new ArgumentException(
                "WHERE IN 절에 비어 있는 컬렉션을 사용할 수 없습니다. " +
                "호출 전 컬렉션이 비어 있는지 확인하세요.",
                nameof(values));

        var needsQuotes = typeof(T) == typeof(Guid)
                       || typeof(T) == typeof(DateTime)
                       || typeof(T) == typeof(DateTimeOffset)
                       || typeof(T) == typeof(DateOnly)
                       || typeof(T) == typeof(TimeOnly);

        return needsQuotes
            ? string.Join(",", list.Select(v => $"'{v}'"))
            : string.Join(",", list);
    }
}
