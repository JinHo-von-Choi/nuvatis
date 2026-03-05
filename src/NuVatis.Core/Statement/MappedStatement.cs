using NuVatis.Mapping;

namespace NuVatis.Statement;

/**
 * XML 또는 Attribute에서 파싱된 하나의 SQL 구문을 표현한다.
 * namespace + id 조합으로 유일하게 식별된다.
 *
 * @author 최진호
 * @date   2026-02-24
 * @modified 2026-02-25 UseCache 추가 (Phase 6.4 A-4)
 */
public sealed class MappedStatement {
    public required string Id            { get; init; }
    public required string Namespace     { get; init; }
    public required StatementType Type   { get; init; }
    public required string SqlSource     { get; init; }
    public string? ResultMapId           { get; init; }
    public Type? ResultType              { get; init; }
    public Type? ParameterType           { get; init; }
    public SelectKeyConfig? SelectKey    { get; init; }
    public int? CommandTimeout           { get; init; }

    /**
     * 2차 캐시 사용 여부. true이면 해당 Namespace에 등록된 캐시를 사용한다.
     * Select 타입에서만 유효하며, Insert/Update/Delete는 캐시 무효화를 트리거한다.
     */
    public bool UseCache                 { get; init; }

    /**
     * 동적 SQL 빌더. null이면 SqlSource + ParameterBinder 경로를 사용한다.
     * <foreach>, <if>, <where> 등 동적 태그가 포함된 statement에서
     * SG가 생성하는 람다로 설정된다.
     *
     * Func 파라미터: (object? parameter) → (string sql, List<DbParameter> parameters)
     */
    public Func<object?, (string Sql, List<System.Data.Common.DbParameter> Parameters)>? DynamicSqlBuilder { get; init; }

    /**
     * namespace.id 형태의 전체 식별자.
     */
    public string FullId => $"{Namespace}.{Id}";
}
