namespace NuVatis.QueryBuilder.Rendering;

public sealed class MySqlDialect : BaseDialect {
    public override string QuoteIdentifier(string name) => $"`{name}`";
    public override string Placeholder(int index)        => $"@p{index}";
}
