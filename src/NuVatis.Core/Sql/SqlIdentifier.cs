using System.Globalization;

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
    // 식별자 형태 화이트리스트: 문자(유니코드 \p{L})/밑줄 시작, 문자·숫자·밑줄·$·# 구성,
    // 점(.)으로 구분된 다단계(schema.table.column) 허용.
    // $, #은 Oracle 스타일 식별자, \p{L}·\p{N}은 한글 등 비ASCII 식별자 지원.
    private static readonly System.Text.RegularExpressions.Regex _identifierPattern =
        new(@"^[\p{L}_][\p{L}\p{N}_$#]*(\.[\p{L}_][\p{L}\p{N}_$#]*)*$",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    // \b triggers at '.' boundaries (schema.or would false-positive with \b).
    // Use negative lookbehind/ahead that includes '.' so dot-qualified identifiers
    // like schema.or_table are NOT flagged.
    private static readonly System.Text.RegularExpressions.Regex _forbiddenKeywords =
        new(@"(?<![.\w])(union|select|drop|insert|or|and)(?![.\w])",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.Compiled);

    private readonly string _value;

    private SqlIdentifier(string value) => _value = value;

    /// <summary>
    /// 문자열로부터 <see cref="SqlIdentifier"/>를 생성한다.
    /// 식별자 형식(문자/밑줄 시작, 문자·숫자·밑줄·$·# 구성, 점 구분 다단계 — 유니코드 문자 허용)을 벗어나거나
    /// SQL 키워드(union, select, drop, insert, or, and)인 경우 <see cref="ArgumentException"/>을 발생시킨다.
    /// 인용 식별자(대괄호, 백틱, 따옴표)는 지원하지 않는다 — <c>IDbProvider.WrapIdentifier</c>를 사용하라.
    /// </summary>
    /// <param name="value">SQL 식별자로 사용할 문자열. 빈 문자열 불가.</param>
    /// <returns>검증된 <see cref="SqlIdentifier"/> 인스턴스.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/>가 null인 경우.</exception>
    /// <exception cref="ArgumentException">빈 문자열이거나 식별자 형식이 아니거나 SQL 키워드인 경우.</exception>
    public static SqlIdentifier From(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Length == 0)
            throw new ArgumentException("SQL 식별자는 빈 문자열일 수 없습니다.", nameof(value));

        if (!_identifierPattern.IsMatch(value))
            throw new ArgumentException(
                $"유효한 SQL 식별자 형식이 아닙니다: '{value}'. " +
                "허용 형식: 문자/밑줄로 시작하는 문자·숫자·밑줄·$·# 조합, 점(.)으로 구분된 다단계 식별자.",
                nameof(value));

        if (_forbiddenKeywords.IsMatch(value))
            throw new ArgumentException(
                $"SQL 키워드는 식별자로 사용할 수 없습니다: '{value}'", nameof(value));

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
    /// 허용된 값 타입 컬렉션을 SQL WHERE IN 절에 안전하게 인라인할 수 있는
    /// 쉼표 구분 문자열로 변환한다.
    /// </summary>
    /// <remarks>
    /// 지원 타입: 숫자형(byte, sbyte, short, ushort, int, uint, long, ulong, float, double, decimal),
    /// enum(underlying 정수값으로 인라인), Guid, DateTime, DateTimeOffset, DateOnly, TimeOnly.
    /// 그 외 struct 타입은 <see cref="ArgumentException"/>을 발생시킨다.
    ///
    /// 모든 값은 <see cref="CultureInfo.InvariantCulture"/> 고정 포맷으로 렌더링된다.
    /// Guid와 날짜·시간 타입은 따옴표로 감싸고, 숫자형과 enum은 그대로 출력한다.
    ///
    /// 빈 컬렉션은 SQL 오류를 유발하므로 <see cref="ArgumentException"/>을 발생시킨다.
    /// </remarks>
    /// <param name="values">WHERE IN 절에 인라인할 값 컬렉션.</param>
    /// <typeparam name="T">지원 목록에 포함된 struct 타입.</typeparam>
    /// <returns>쉼표로 구분된 SQL 리터럴 문자열.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="values"/>가 null인 경우.</exception>
    /// <exception cref="ArgumentException"><paramref name="values"/>가 비어 있거나 지원되지 않는 타입인 경우.</exception>
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

        return string.Join(",", list.Select(v => FormatSqlLiteral(v)));
    }

    private static readonly HashSet<Type> _numericTypes = new()
    {
        typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
        typeof(int), typeof(uint), typeof(long), typeof(ulong),
        typeof(float), typeof(double), typeof(decimal)
    };

    private static string FormatSqlLiteral<T>(T value) where T : struct
    {
        var type = typeof(T);

        if (type.IsEnum)
        {
            var underlying = Convert.ChangeType(
                value, Enum.GetUnderlyingType(type), CultureInfo.InvariantCulture);
            return ((IFormattable)underlying).ToString(null, CultureInfo.InvariantCulture);
        }

        if (_numericTypes.Contains(type))
            return ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture);

        return value switch
        {
            Guid g           => $"'{g:D}'",
            DateTime dt      => $"'{dt.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture)}'",
            DateTimeOffset o => $"'{o.ToString("yyyy-MM-dd HH:mm:ss.fffffff zzz", CultureInfo.InvariantCulture)}'",
            DateOnly d       => $"'{d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}'",
            TimeOnly t       => $"'{t.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture)}'",
            _ => throw new ArgumentException(
                $"JoinTyped가 지원하지 않는 타입입니다: {type.FullName}. " +
                "지원 타입: 숫자형(byte~decimal), enum, Guid, DateTime, DateTimeOffset, DateOnly, TimeOnly. " +
                "그 외 값은 파라미터 바인딩(#{})을 사용하세요.", "values")
        };
    }
}
