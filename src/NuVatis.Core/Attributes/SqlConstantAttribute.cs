namespace NuVatis.Attributes;

/**
 * ${} 문자열 치환에 안전하게 사용할 수 있는 SQL 상수를 표시한다.
 *
 * 이 어트리뷰트가 부착된 const/static readonly 필드는
 * ${} 파라미터로 사용되어도 NV004 SQL Injection 경고를 발생시키지 않는다.
 * 테이블명, 컬럼명, ORDER BY 절 등 화이트리스트 기반 동적 SQL에 사용한다.
 *
 * 사용 예시:
 * <code>
 * public static class SqlRef {
 *     [SqlConstant] public const string OrderByName = "user_name";
 *     [SqlConstant] public const string OrderByDate = "created_at";
 * }
 * </code>
 *
 * @author 최진호
 * @date   2026-02-26
 */
[AttributeUsage(
    AttributeTargets.Field | AttributeTargets.Property,
    AllowMultiple = false,
    Inherited     = false)]
public sealed class SqlConstantAttribute : Attribute {
}
