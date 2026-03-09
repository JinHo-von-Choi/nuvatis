namespace NuVatis.Mapping;

/**
 * 1:1 관계 매핑을 표현한다.
 * <association property="Department" resultMap="DepartmentResult" /> 에 대응.
 *
 * @author 최진호
 * @date   2026-02-24
 */
/// <summary>1:1 관계 매핑을 표현한다. &lt;association&gt; 엘리먼트에 대응한다.</summary>
public sealed class AssociationMapping {
    /// <summary>Gets the name of the navigation property on the parent type.</summary>
    public required string Property    { get; init; }
    /// <summary>Gets the result map identifier used to map the associated object.</summary>
    public string? ResultMapId         { get; init; }
    /// <summary>Gets the column prefix applied to disambiguate columns for this association.</summary>
    public string? ColumnPrefix        { get; init; }
    /// <summary>Gets the statement identifier used for lazy-load select, or null for join-based loading.</summary>
    public string? Select              { get; init; }
    /// <summary>Gets the foreign key column passed to the lazy-load select statement.</summary>
    public string? Column              { get; init; }
    /// <summary>Gets the fetch strategy. Defaults to <see cref="FetchType.Eager"/>.</summary>
    public FetchType FetchType         { get; init; } = FetchType.Eager;
}
