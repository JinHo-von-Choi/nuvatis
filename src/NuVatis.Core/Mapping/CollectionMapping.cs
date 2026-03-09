namespace NuVatis.Mapping;

/**
 * 1:N 관계 매핑을 표현한다.
 * <collection property="Orders" resultMap="OrderResult" /> 에 대응.
 *
 * @author 최진호
 * @date   2026-02-24
 */
/// <summary>1:N 관계 매핑을 표현한다. &lt;collection&gt; 엘리먼트에 대응한다.</summary>
public sealed class CollectionMapping {
    /// <summary>Gets the name of the collection property on the parent type.</summary>
    public required string Property    { get; init; }
    /// <summary>Gets the result map identifier used to map each element of the collection.</summary>
    public string? ResultMapId         { get; init; }
    /// <summary>Gets the fully-qualified CLR type of each element in the collection.</summary>
    public string? OfType              { get; init; }
    /// <summary>Gets the column prefix applied to disambiguate columns for this collection.</summary>
    public string? ColumnPrefix        { get; init; }
    /// <summary>Gets the statement identifier used for lazy-load select, or null for join-based loading.</summary>
    public string? Select              { get; init; }
    /// <summary>Gets the foreign key column passed to the lazy-load select statement.</summary>
    public string? Column              { get; init; }
    /// <summary>Gets the fetch strategy. Defaults to <see cref="FetchType.Eager"/>.</summary>
    public FetchType FetchType         { get; init; } = FetchType.Eager;
}
