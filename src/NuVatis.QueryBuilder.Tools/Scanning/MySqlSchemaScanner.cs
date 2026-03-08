namespace NuVatis.QueryBuilder.Tools.Scanning;

using MySqlConnector;

public sealed class MySqlSchemaScanner : ISchemaScanner {
    public async Task<IReadOnlyList<TableInfo>> ScanAsync(string connStr, string schema) {
        await using var conn = new MySqlConnection(connStr);
        await conn.OpenAsync();

        var tableNames = await GetTableNamesAsync(conn, schema);
        var result     = new List<TableInfo>();

        foreach (var tableName in tableNames) {
            var columns = await GetColumnsAsync(conn, schema, tableName);
            result.Add(new TableInfo(schema, tableName, columns));
        }

        return result;
    }

    private static async Task<List<string>> GetTableNamesAsync(
        MySqlConnection conn, string schema) {
        var list = new List<string>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT TABLE_NAME
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = @schema AND TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_NAME
            """;
        cmd.Parameters.AddWithValue("@schema", schema);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) list.Add(reader.GetString(0));
        return list;
    }

    private static async Task<List<ColumnInfo>> GetColumnsAsync(
        MySqlConnection conn, string schema, string table) {
        var pks  = await GetPrimaryKeysAsync(conn, schema, table);
        var cols = new List<ColumnInfo>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, ORDINAL_POSITION
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table
            ORDER BY ORDINAL_POSITION
            """;
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@table",  table);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            cols.Add(new ColumnInfo(
                ColumnName:      reader.GetString(0),
                DbType:          reader.GetString(1),
                IsNullable:      reader.GetString(2) == "YES",
                IsPrimaryKey:    pks.Contains(reader.GetString(0)),
                OrdinalPosition: reader.GetInt32(3)));
        return cols;
    }

    private static async Task<HashSet<string>> GetPrimaryKeysAsync(
        MySqlConnection conn, string schema, string table) {
        var pks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COLUMN_NAME
            FROM information_schema.KEY_COLUMN_USAGE
            WHERE CONSTRAINT_NAME = 'PRIMARY'
              AND TABLE_SCHEMA = @schema
              AND TABLE_NAME   = @table
            """;
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@table",  table);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) pks.Add(reader.GetString(0));
        return pks;
    }
}
