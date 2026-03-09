namespace NuVatis.QueryBuilder.Rendering;

using System.Text;
using NuVatis.QueryBuilder.Ast;

/**
 * SQL Server용 ISqlDialect 구현.
 * 식별자 래핑: [name], 파라미터: @p0.
 * 페이지네이션은 OFFSET n ROWS FETCH NEXT m ROWS ONLY (ORDER BY 필수).
 *
 * @author 최진호
 * @date   2026-03-09
 */
public sealed class SqlServerDialect : BaseDialect {
    public override string QuoteIdentifier(string name) => $"[{name}]";
    public override string Placeholder(int index)        => $"@p{index}";

    protected override void AppendPagination(StringBuilder sb, SelectQuery q) {
        if (q.OrderByFields.Count == 0)
            throw new InvalidOperationException(
                "SQL Server는 OFFSET/FETCH 페이지네이션에 ORDER BY가 필요합니다.");

        sb.Append($" OFFSET {q.OffsetValue ?? 0} ROWS FETCH NEXT {q.LimitValue} ROWS ONLY");
    }
}
