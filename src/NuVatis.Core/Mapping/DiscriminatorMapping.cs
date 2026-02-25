namespace NuVatis.Mapping;

/**
 * MyBatis <discriminator> 매핑 모델.
 * 특정 컬럼 값에 따라 서로 다른 ResultMap을 적용하는 다형성 매핑 전략.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public sealed class DiscriminatorMapping {
    public required string              Column   { get; init; }
    public required string              JdbcType { get; init; }
    public Dictionary<string, string>   Cases    { get; init; } = new();
}
