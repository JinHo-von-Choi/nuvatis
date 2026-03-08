namespace NuVatis.QueryBuilder.Ast;

public sealed class InsertQuery : QueryNode {
    public TableNode                Table   { get; }
    public IReadOnlyList<FieldNode> Columns { get; private set; } = [];
    public IReadOnlyList<object?>   Values  { get; private set; } = [];

    public InsertQuery(TableNode table) {
        Table = table;
    }

    public InsertQuery Into(params FieldNode[] columns) {
        Columns = columns;
        return this;
    }

    public InsertQuery WithValues(params object?[] values) {
        Values = values;
        return this;
    }
}
