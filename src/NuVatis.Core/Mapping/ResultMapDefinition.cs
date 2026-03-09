namespace NuVatis.Mapping;

/**
 * <resultMap> 요소 전체를 표현하는 모델.
 * id, result, association, collection 매핑을 모두 포함한다.
 *
 * @author 최진호
 * @date   2026-02-24
 */
/// <summary>&lt;resultMap&gt; 요소 전체를 표현하는 모델. id, result, association, collection 매핑을 포함한다.</summary>
public sealed class ResultMapDefinition {
    /// <summary>Gets the unique identifier of this result map within its namespace.</summary>
    public required string Id                                { get; init; }
    /// <summary>Gets the fully-qualified CLR type name that this result map maps to.</summary>
    public required string Type                              { get; init; }
    /// <summary>Gets the parent result map identifier that this map extends, or null if standalone.</summary>
    public string? Extends                                   { get; init; }
    /// <summary>Gets the list of column-to-property mappings defined by &lt;id&gt; and &lt;result&gt; elements.</summary>
    public List<ResultMapping> Mappings                      { get; init; } = new();
    /// <summary>Gets the list of 1:1 association mappings defined by &lt;association&gt; elements.</summary>
    public List<AssociationMapping> Associations              { get; init; } = new();
    /// <summary>Gets the list of 1:N collection mappings defined by &lt;collection&gt; elements.</summary>
    public List<CollectionMapping> Collections               { get; init; } = new();
    /// <summary>Gets the discriminator mapping for polymorphic result sets, or null if not used.</summary>
    public DiscriminatorMapping?   Discriminator             { get; init; }
}
