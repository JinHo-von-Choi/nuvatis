namespace NuVatis.Mapping;

/**
 * 단일 column → property 매핑을 표현한다.
 * <result column="user_name" property="UserName" /> 에 대응.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public sealed class ResultMapping {
    public required string Column   { get; init; }
    public required string Property { get; init; }
    public string? TypeHandler      { get; init; }
    public bool IsId                { get; init; }
}
