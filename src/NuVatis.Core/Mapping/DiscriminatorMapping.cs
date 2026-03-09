namespace NuVatis.Mapping;

/**
 * MyBatis <discriminator> 매핑 모델.
 * 특정 컬럼 값에 따라 서로 다른 ResultMap을 적용하는 다형성 매핑 전략.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public sealed class DiscriminatorMapping {
    /// <summary>판별 기준이 되는 DB 컬럼 이름.</summary>
    public required string              Column   { get; init; }
    /// <summary>판별 컬럼의 JDBC/DB 타입 이름 (예: "INTEGER", "VARCHAR").</summary>
    public required string              JdbcType { get; init; }
    /// <summary>컬럼 값(key) → 적용할 ResultMap ID(value) 매핑 테이블.</summary>
    public Dictionary<string, string>   Cases    { get; init; } = new();
}
