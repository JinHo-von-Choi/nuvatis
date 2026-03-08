namespace NuVatis.QueryBuilder.Ast;

public class TableNode : QueryNode {
    public string  Schema { get; }
    public string  Name   { get; }
    public string? Alias  { get; init; }

    public TableNode(string schema, string name) {
        Schema = schema;
        Name   = name;
    }

    public TableNode As(string alias) => new TableNode(Schema, Name) { Alias = alias };
}
