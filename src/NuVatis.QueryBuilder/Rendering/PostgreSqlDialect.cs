namespace NuVatis.QueryBuilder.Rendering;

public sealed class PostgreSqlDialect : BaseDialect {
    public override string QuoteIdentifier(string name) => $"\"{name}\"";
    public override string Placeholder(int index)        => $"${index + 1}";
}
