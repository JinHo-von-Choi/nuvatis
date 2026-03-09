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
    /// <summary>Statement의 고유 식별자. XML id 속성 또는 Attribute name에 해당한다.</summary>
    public required string Id            { get; init; }
    /// <summary>Statement가 속한 네임스페이스. 매퍼 클래스 또는 XML namespace 속성에 해당한다.</summary>
    public required string Namespace     { get; init; }
    /// <summary>Statement 종류 (Select, Insert, Update, Delete).</summary>
    public required StatementType Type   { get; init; }
    /// <summary>원본 SQL 문자열. 동적 태그 처리 전의 원본 소스.</summary>
    public required string SqlSource     { get; init; }
    /// <summary>결과 매핑에 사용할 ResultMap 식별자. null이면 ResultType 또는 ColumnMapper 경로를 사용한다.</summary>
    public string? ResultMapId           { get; init; }
    /// <summary>결과 행을 매핑할 .NET 타입. ResultMapId가 없는 경우 ColumnMapper 리플렉션 폴백에 사용된다.</summary>
    public Type? ResultType              { get; init; }
    /// <summary>SQL 파라미터로 바인딩할 .NET 타입. null이면 익명 객체 또는 딕셔너리를 허용한다.</summary>
    public Type? ParameterType           { get; init; }
    /// <summary>INSERT 전후에 키를 생성하는 selectKey 설정. null이면 selectKey 없음.</summary>
    public SelectKeyConfig? SelectKey    { get; init; }
    /// <summary>DbCommand.CommandTimeout 재정의 값(초). null이면 연결 기본값을 사용한다.</summary>
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
    public string FullId                 => $"{Namespace}.{Id}";
}
