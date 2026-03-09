namespace NuVatis.QueryBuilder.Ast;

public abstract class FieldNode : QueryNode {
    public TableNode Table      { get; }
    public string    ColumnName { get; }

    protected FieldNode(TableNode table, string columnName) {
        Table      = table;
        ColumnName = columnName;
    }

    public SortField       Asc()       => new(this, SortOrder.Asc);
    public SortField       Desc()      => new(this, SortOrder.Desc);
    public IsNullCondition IsNull()    => new(this, negated: false);
    public IsNullCondition IsNotNull() => new(this, negated: true);

    // JOIN ON 절에서 필드 간 비교용 (cross-type)
    public ConditionNode Eq(FieldNode other) => new BinaryCondition(this, "=", other);
}

public sealed class FieldNode<T> : FieldNode {
    public FieldNode(TableNode table, string columnName) : base(table, columnName) { }

    public ConditionNode Eq(T value)  => new BinaryCondition(this, "=",  value);
    public ConditionNode Ne(T value)  => new BinaryCondition(this, "<>", value);
    public ConditionNode Gt(T value)  => new BinaryCondition(this, ">",  value);
    public ConditionNode Ge(T value)  => new BinaryCondition(this, ">=", value);
    public ConditionNode Lt(T value)  => new BinaryCondition(this, "<",  value);
    public ConditionNode Le(T value)  => new BinaryCondition(this, "<=", value);
    public ConditionNode Like(string pattern) => new BinaryCondition(this, "LIKE", pattern);
    public ConditionNode In(IEnumerable<T> values)
        => new InCondition(this, values.Cast<object?>());
}
