namespace NuVatis.QueryBuilder.Ast;

public sealed class DeleteQuery : QueryNode {
    public TableNode      Table          { get; }
    public ConditionNode? WhereCondition { get; private set; }

    public DeleteQuery(TableNode table) {
        Table = table;
    }

    public DeleteQuery Where(ConditionNode condition) {
        WhereCondition = condition;
        return this;
    }
}
