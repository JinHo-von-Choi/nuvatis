namespace NuVatis.QueryBuilder.Ast;

public class TableNode : QueryNode {
    public string  Schema { get; }
    public string  Name   { get; }
    public string? Alias  { get; private set; }

    public TableNode(string schema, string name) {
        Schema = schema;
        Name   = name;
    }

    public TableNode As(string alias) {
        Alias = alias;
        return this;
    }
}
