namespace NuVatis.Mapping;

/**
 * <resultMap> 요소 전체를 표현하는 모델.
 * id, result, association, collection 매핑을 모두 포함한다.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public sealed class ResultMapDefinition {
    public required string Id                                { get; init; }
    public required string Type                              { get; init; }
    public string? Extends                                   { get; init; }
    public List<ResultMapping> Mappings                      { get; init; } = new();
    public List<AssociationMapping> Associations              { get; init; } = new();
    public List<CollectionMapping> Collections               { get; init; } = new();
    public DiscriminatorMapping?   Discriminator             { get; init; }
}
