namespace NuVatis.QueryBuilder.Ast;

public sealed class UpdateQuery : QueryNode {
    private readonly List<(FieldNode Field, object? Value)> _sets = [];

    public TableNode                                         Table          { get; }
    public IReadOnlyList<(FieldNode Field, object? Value)>   Sets           => _sets;
    public ConditionNode?                                    WhereCondition { get; private set; }

    public UpdateQuery(TableNode table) {
        Table = table;
    }

    public UpdateQuery Set<T>(FieldNode<T> field, T value) {
        _sets.Add((field, value));
        return this;
    }

    public UpdateQuery Where(ConditionNode condition) {
        WhereCondition = condition;
        return this;
    }
}
