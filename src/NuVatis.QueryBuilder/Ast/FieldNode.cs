namespace NuVatis.QueryBuilder.Ast;

public abstract class FieldNode : QueryNode {
    public TableNode Table      { get; }
    public string    ColumnName { get; }

    protected FieldNode(TableNode table, string columnName) {
        Table      = table;
        ColumnName = columnName;
    }

    public SortField Asc()  => new(this, SortOrder.Asc);
    public SortField Desc() => new(this, SortOrder.Desc);
}

public sealed class FieldNode<T> : FieldNode {
    public FieldNode(TableNode table, string columnName) : base(table, columnName) { }
}
