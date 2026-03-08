namespace NuVatis.QueryBuilder.Rendering;

using NuVatis.QueryBuilder.Ast;

public interface ISqlDialect {
    RenderedSql Render(SelectQuery query);
    RenderedSql Render(InsertQuery query);
    RenderedSql Render(UpdateQuery query);
    RenderedSql Render(DeleteQuery query);

    string QuoteIdentifier(string name);

    /** SQL 텍스트에 삽입되는 파라미터 자리표시자. 예: $1 (PostgreSQL), @p0 (MySQL). */
    string Placeholder(int index);

    /**
     * DbParameter.ParameterName에 할당할 이름을 반환합니다.
     * PostgreSQL처럼 positional binding을 사용하는 경우 빈 문자열을 반환해야 합니다.
     * Named binding(MySQL 등)은 Placeholder와 동일한 값을 반환합니다.
     */
    string ParameterName(int index);
}
