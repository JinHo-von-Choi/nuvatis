using System.Data.Common;
using NuVatis.QueryBuilder.Ast;
using NuVatis.QueryBuilder.Rendering;

namespace NuVatis.QueryBuilder.Execution;

public static class QueryExecutor {
    public static List<T> Fetch<T>(SelectQuery query, DbConnection conn, ISqlDialect dialect)
        where T : new() {
        var rendered     = dialect.Render(query);
        using var cmd    = CreateCommand(conn, rendered.Sql, rendered.Parameters, dialect);
        using var reader = cmd.ExecuteReader();
        var result       = new List<T>();
        while (reader.Read()) result.Add(RecordMapper.MapRow<T>(reader));
        return result;
    }

    public static T? FetchOne<T>(SelectQuery query, DbConnection conn, ISqlDialect dialect)
        where T : new() {
        var rendered     = dialect.Render(query);
        using var cmd    = CreateCommand(conn, rendered.Sql, rendered.Parameters, dialect);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? RecordMapper.MapRow<T>(reader) : default;
    }

    public static async Task<List<T>> FetchAsync<T>(
        SelectQuery       query,
        DbConnection      conn,
        ISqlDialect       dialect,
        CancellationToken ct)
        where T : new() {
        var rendered          = dialect.Render(query);
        await using var cmd    = CreateCommand(conn, rendered.Sql, rendered.Parameters, dialect);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var result             = new List<T>();
        while (await reader.ReadAsync(ct)) result.Add(RecordMapper.MapRow<T>(reader));
        return result;
    }

    /// <summary>INSERT/UPDATE/DELETE 쿼리를 실행합니다.</summary>
    /// <param name="query">InsertQuery, UpdateQuery, DeleteQuery 중 하나여야 합니다.</param>
    /// <param name="conn">열린 상태의 DB 연결.</param>
    /// <param name="dialect">렌더링에 사용할 SQL 방언.</param>
    /// <returns>영향받은 행 수.</returns>
    /// <exception cref="ArgumentException">지원하지 않는 쿼리 타입 전달 시.</exception>
    public static int Execute(object query, DbConnection conn, ISqlDialect dialect) {
        var rendered = query switch {
            InsertQuery q => dialect.Render(q),
            UpdateQuery q => dialect.Render(q),
            DeleteQuery q => dialect.Render(q),
            _ => throw new ArgumentException($"지원하지 않는 쿼리 타입: {query.GetType().Name}")
        };
        using var cmd = CreateCommand(conn, rendered.Sql, rendered.Parameters, dialect);
        return cmd.ExecuteNonQuery();
    }

    private static DbCommand CreateCommand(
        DbConnection            conn,
        string                  sql,
        IReadOnlyList<object?>  pars,
        ISqlDialect             dialect) {
        var cmd         = conn.CreateCommand();
        cmd.CommandText = sql;
        for (int i = 0; i < pars.Count; i++) {
            var p           = cmd.CreateParameter();
            p.ParameterName = dialect.Placeholder(i);
            p.Value         = pars[i] ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }
        return cmd;
    }
}
