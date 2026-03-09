namespace NuVatis.QueryBuilder.Tools.Scanning;

using Npgsql;

public sealed class PostgreSqlSchemaScanner : ISchemaScanner {
    public async Task<IReadOnlyList<TableInfo>> ScanAsync(string connStr, string schema) {
        await using var conn = new NpgsqlConnection(connStr);
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
        NpgsqlConnection conn, string schema) {
        var list = new List<string>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = @schema AND table_type = 'BASE TABLE'
            ORDER BY table_name
            """;
        cmd.Parameters.AddWithValue("schema", schema);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) list.Add(reader.GetString(0));
        return list;
    }

    private static async Task<List<ColumnInfo>> GetColumnsAsync(
        NpgsqlConnection conn, string schema, string table) {
        var pks  = await GetPrimaryKeysAsync(conn, schema, table);
        var cols = new List<ColumnInfo>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT column_name, udt_name, is_nullable, ordinal_position
            FROM information_schema.columns
            WHERE table_schema = @schema AND table_name = @table
            ORDER BY ordinal_position
            """;
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("table",  table);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            cols.Add(new ColumnInfo(
                ColumnName:       reader.GetString(0),
                DbType:           reader.GetString(1),
                IsNullable:       reader.GetString(2) == "YES",
                IsPrimaryKey:     pks.Contains(reader.GetString(0)),
                OrdinalPosition:  reader.GetInt32(3)));
        return cols;
    }

    private static async Task<HashSet<string>> GetPrimaryKeysAsync(
        NpgsqlConnection conn, string schema, string table) {
        var pks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT kcu.column_name
            FROM information_schema.key_column_usage       kcu
            JOIN information_schema.table_constraints      tc
              ON kcu.constraint_name = tc.constraint_name
             AND kcu.table_schema    = tc.table_schema
            WHERE tc.constraint_type = 'PRIMARY KEY'
              AND kcu.table_schema   = @schema
              AND kcu.table_name     = @table
            """;
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("table",  table);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) pks.Add(reader.GetString(0));
        return pks;
    }
}
