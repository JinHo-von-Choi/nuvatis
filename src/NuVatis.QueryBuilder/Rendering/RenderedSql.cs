namespace NuVatis.QueryBuilder.Rendering;

public sealed record RenderedSql(string Sql, IReadOnlyList<object?> Parameters);
