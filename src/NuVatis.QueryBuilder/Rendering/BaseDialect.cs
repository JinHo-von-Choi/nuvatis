namespace NuVatis.QueryBuilder.Rendering;

using System.Text;
using NuVatis.QueryBuilder.Ast;

public abstract class BaseDialect : ISqlDialect {
    public abstract string QuoteIdentifier(string name);
    public abstract string Placeholder(int index);

    /**
     * DbParameter.ParameterName에 할당할 이름. 기본값은 Placeholder와 동일합니다.
     * PostgreSQL처럼 positional binding을 쓰는 dialect는 빈 문자열로 오버라이드하세요.
     */
    public virtual string ParameterName(int index) => Placeholder(index);

    public RenderedSql Render(SelectQuery q) {
        if (q.FromTable is null)
            throw new InvalidOperationException("FROM 절이 없습니다.");

        var sb   = new StringBuilder();
        var pars = new List<object?>();

        sb.Append("SELECT ");
        if (q.Fields.Count == 0) {
            sb.Append('*');
        } else {
            sb.Append(string.Join(", ", q.Fields.Select(f => QuoteIdentifier(f.ColumnName))));
        }

        sb.Append(" FROM ");
        AppendTable(sb, q.FromTable);

        foreach (var join in q.Joins) {
            sb.Append($" {JoinTypeToSql(join.Type)} JOIN ");
            AppendTable(sb, join.Table);
            if (join.On != null) {
                sb.Append(" ON ");
                AppendCondition(sb, join.On, pars);
            }
        }

        if (q.WhereCondition != null) {
            sb.Append(" WHERE ");
            AppendCondition(sb, q.WhereCondition, pars);
        }

        if (q.OrderByFields.Count > 0) {
            sb.Append(" ORDER BY ");
            sb.Append(string.Join(", ", q.OrderByFields.Select(s =>
                $"{QuoteIdentifier(s.Field.ColumnName)} {(s.Order == SortOrder.Asc ? "ASC" : "DESC")}")));
        }

        if (q.LimitValue.HasValue)
            sb.Append($" LIMIT {q.LimitValue} OFFSET {q.OffsetValue ?? 0}");

        return new(sb.ToString(), pars);
    }

    public RenderedSql Render(InsertQuery q) {
        var sb   = new StringBuilder();
        var pars = new List<object?>();

        sb.Append("INSERT INTO ");
        AppendTable(sb, q.Table);
        sb.Append(" (");
        sb.Append(string.Join(", ", q.Columns.Select(f => QuoteIdentifier(f.ColumnName))));
        sb.Append(") VALUES (");

        for (int i = 0; i < q.Values.Count; i++) {
            if (i > 0) sb.Append(", ");
            pars.Add(q.Values[i]);
            sb.Append(Placeholder(pars.Count - 1));
        }
        sb.Append(')');

        return new(sb.ToString(), pars);
    }

    public RenderedSql Render(UpdateQuery q) {
        var sb   = new StringBuilder();
        var pars = new List<object?>();

        sb.Append("UPDATE ");
        AppendTable(sb, q.Table);
        sb.Append(" SET ");

        for (int i = 0; i < q.Sets.Count; i++) {
            if (i > 0) sb.Append(", ");
            var (field, value) = q.Sets[i];
            pars.Add(value);
            sb.Append($"{QuoteIdentifier(field.ColumnName)} = {Placeholder(pars.Count - 1)}");
        }

        if (q.WhereCondition != null) {
            sb.Append(" WHERE ");
            AppendCondition(sb, q.WhereCondition, pars);
        }

        return new(sb.ToString(), pars);
    }

    public RenderedSql Render(DeleteQuery q) {
        var sb   = new StringBuilder();
        var pars = new List<object?>();

        sb.Append("DELETE FROM ");
        AppendTable(sb, q.Table);

        if (q.WhereCondition != null) {
            sb.Append(" WHERE ");
            AppendCondition(sb, q.WhereCondition, pars);
        }

        return new(sb.ToString(), pars);
    }

    private void AppendTable(StringBuilder sb, TableNode t) {
        sb.Append($"{QuoteIdentifier(t.Schema)}.{QuoteIdentifier(t.Name)}");
        if (t.Alias != null) sb.Append($" AS {QuoteIdentifier(t.Alias)}");
    }

    protected void AppendCondition(StringBuilder sb, ConditionNode cond, List<object?> pars) {
        switch (cond) {
            case BinaryCondition { Value: FieldNode fn } b:
                sb.Append($"{QuoteIdentifier(b.Field.ColumnName)} {b.Operator} {QuoteIdentifier(fn.ColumnName)}");
                break;
            case BinaryCondition b:
                pars.Add(b.Value);
                sb.Append($"{QuoteIdentifier(b.Field.ColumnName)} {b.Operator} {Placeholder(pars.Count - 1)}");
                break;
            case IsNullCondition n:
                sb.Append($"{QuoteIdentifier(n.Field.ColumnName)} {(n.Negated ? "IS NOT NULL" : "IS NULL")}");
                break;
            case InCondition ic:
                sb.Append(QuoteIdentifier(ic.Field.ColumnName));
                sb.Append(" IN (");
                for (int i = 0; i < ic.Values.Count; i++) {
                    if (i > 0) sb.Append(", ");
                    pars.Add(ic.Values[i]);
                    sb.Append(Placeholder(pars.Count - 1));
                }
                sb.Append(')');
                break;
            case AndCondition a:
                AppendCondition(sb, a.Left, pars);
                sb.Append(" AND ");
                AppendCondition(sb, a.Right, pars);
                break;
            case OrCondition o:
                sb.Append('(');
                AppendCondition(sb, o.Left, pars);
                sb.Append(" OR ");
                AppendCondition(sb, o.Right, pars);
                sb.Append(')');
                break;
            case NotCondition n:
                sb.Append("NOT (");
                AppendCondition(sb, n.Inner, pars);
                sb.Append(')');
                break;
        }
    }

    private static string JoinTypeToSql(JoinType type) => type switch {
        JoinType.Inner => "INNER",
        JoinType.Left  => "LEFT",
        JoinType.Right => "RIGHT",
        JoinType.Cross => "CROSS",
        _              => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
