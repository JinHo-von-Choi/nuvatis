namespace NuVatis.QueryBuilder.Tools.Scanning;

public record ColumnInfo(
    string  ColumnName,
    string  DbType,
    bool    IsNullable,
    bool    IsPrimaryKey,
    int     OrdinalPosition);

public record TableInfo(string Schema, string Name, IReadOnlyList<ColumnInfo> Columns);

public interface ISchemaScanner {
    Task<IReadOnlyList<TableInfo>> ScanAsync(string connectionString, string schema);
}
