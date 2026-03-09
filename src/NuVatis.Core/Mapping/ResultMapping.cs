namespace NuVatis.Mapping;

/**
 * 단일 column → property 매핑을 표현한다.
 * <result column="user_name" property="UserName" /> 에 대응.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public sealed class ResultMapping {
    /// <summary>매핑할 DB 컬럼 이름.</summary>
    public required string Column   { get; init; }
    /// <summary>컬럼 값을 설정할 .NET 객체의 프로퍼티 이름.</summary>
    public required string Property { get; init; }
    /// <summary>커스텀 타입 변환에 사용할 TypeHandler 이름. null이면 기본 변환을 사용한다.</summary>
    public string? TypeHandler      { get; init; }
    /// <summary>true이면 이 컬럼이 ResultMap의 ID(기본 키)임을 나타낸다. 1:N 그룹핑 키로 사용된다.</summary>
    public bool IsId                { get; init; }
}
