namespace NuVatis.QueryBuilder.Ast;

public enum JoinType { Inner, Left, Right, Cross }

public sealed class JoinClause : QueryNode {
    public JoinType       Type  { get; }
    public TableNode      Table { get; }
    public ConditionNode? On    { get; internal set; }

    internal JoinClause(JoinType type, TableNode table) {
        Type  = type;
        Table = table;
    }
}
