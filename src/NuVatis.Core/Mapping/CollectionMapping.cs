namespace NuVatis.Mapping;

/**
 * 1:N 관계 매핑을 표현한다.
 * <collection property="Orders" resultMap="OrderResult" /> 에 대응.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public sealed class CollectionMapping {
    public required string Property    { get; init; }
    public string? ResultMapId         { get; init; }
    public string? OfType              { get; init; }
    public string? ColumnPrefix        { get; init; }
    public string? Select              { get; init; }
    public string? Column              { get; init; }
    public FetchType FetchType         { get; init; } = FetchType.Eager;
}
