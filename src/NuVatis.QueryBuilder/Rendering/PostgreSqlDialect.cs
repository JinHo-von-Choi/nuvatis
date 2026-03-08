namespace NuVatis.QueryBuilder.Rendering;

public sealed class PostgreSqlDialect : BaseDialect {
    public override string QuoteIdentifier(string name) => $"\"{name}\"";
    public override string Placeholder(int index)        => $"${index + 1}";

    /**
     * Npgsql은 $1/$2 positional placeholder를 사용할 때 NpgsqlParameter.ParameterName을
     * 빈 문자열로 설정해야 positional binding이 올바르게 동작합니다.
     */
    public override string ParameterName(int index) => "";
}
