namespace NuVatis.QueryBuilder.Rendering;

using System.Text;
using NuVatis.QueryBuilder.Ast;

/**
 * Oracle용 ISqlDialect 구현 (Oracle 12c+).
 * 식별자 래핑: "name", 파라미터 자리표시자: :p0.
 * ODP.NET DbParameter.ParameterName은 콜론 없는 p0 형식이어야 한다.
 * 페이지네이션은 OFFSET n ROWS FETCH NEXT m ROWS ONLY.
 *
 * @author 최진호
 * @date   2026-03-09
 */
public sealed class OracleDialect : BaseDialect {
    public override string QuoteIdentifier(string name) => $"\"{name}\"";
    public override string Placeholder(int index)        => $":p{index}";

    /** ODP.NET은 ParameterName을 콜론 없이 설정해야 named binding이 올바르게 동작한다. */
    public override string ParameterName(int index) => $"p{index}";

    protected override void AppendPagination(StringBuilder sb, SelectQuery q) {
        sb.Append($" OFFSET {q.OffsetValue ?? 0} ROWS FETCH NEXT {q.LimitValue} ROWS ONLY");
    }
}
