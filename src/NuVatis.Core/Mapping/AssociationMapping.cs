namespace NuVatis.Mapping;

/**
 * 1:1 관계 매핑을 표현한다.
 * <association property="Department" resultMap="DepartmentResult" /> 에 대응.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public sealed class AssociationMapping {
    public required string Property    { get; init; }
    public string? ResultMapId         { get; init; }
    public string? ColumnPrefix        { get; init; }
    public string? Select              { get; init; }
    public string? Column              { get; init; }
    public FetchType FetchType         { get; init; } = FetchType.Eager;
}
