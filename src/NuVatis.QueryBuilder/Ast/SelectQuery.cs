namespace NuVatis.QueryBuilder.Ast;

public sealed class SelectQuery : QueryNode {
    private readonly List<FieldNode>  _fields  = [];
    private readonly List<JoinClause> _joins   = [];
    private readonly List<SortField>  _orderBy = [];
    private readonly List<FieldNode>  _groupBy = [];

    public IReadOnlyList<FieldNode>  Fields          => _fields;
    public TableNode?                FromTable        { get; private set; }
    public IReadOnlyList<JoinClause> Joins            => _joins;
    public ConditionNode?            WhereCondition   { get; private set; }
    public IReadOnlyList<SortField>  OrderByFields    => _orderBy;
    public int?                      LimitValue       { get; private set; }
    public int?                      OffsetValue      { get; private set; }
    public IReadOnlyList<FieldNode>  GroupByFields    => _groupBy;
    public ConditionNode?            HavingCondition  { get; private set; }

    public SelectQuery Select(params FieldNode[] fields) {
        _fields.AddRange(fields);
        return this;
    }

    public SelectQuery From(TableNode table) {
        FromTable = table;
        return this;
    }

    public JoinStep InnerJoin(TableNode table) => AddJoin(JoinType.Inner, table);
    public JoinStep LeftJoin(TableNode table)  => AddJoin(JoinType.Left,  table);
    public JoinStep RightJoin(TableNode table) => AddJoin(JoinType.Right, table);

    private JoinStep AddJoin(JoinType type, TableNode table) {
        var clause = new JoinClause(type, table);
        _joins.Add(clause);
        return new JoinStep(this, clause);
    }

    public SelectQuery Where(ConditionNode condition) {
        WhereCondition = condition;
        return this;
    }

    public SelectQuery OrderBy(params SortField[] fields) {
        _orderBy.AddRange(fields);
        return this;
    }

    public SelectQuery Limit(int limit) {
        LimitValue = limit;
        return this;
    }

    public SelectQuery Offset(int offset) {
        OffsetValue = offset;
        return this;
    }

    public SelectQuery GroupBy(params FieldNode[] fields) {
        _groupBy.AddRange(fields);
        return this;
    }

    public SelectQuery Having(ConditionNode condition) {
        HavingCondition = condition;
        return this;
    }
}

public sealed class JoinStep {
    private readonly SelectQuery _query;
    private readonly JoinClause  _clause;

    internal JoinStep(SelectQuery query, JoinClause clause) {
        _query  = query;
        _clause = clause;
    }

    public SelectQuery On(ConditionNode condition) {
        _clause.On = condition;
        return _query;
    }
}
