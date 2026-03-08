namespace NuVatis.QueryBuilder.Execution;

using System.Data.Common;
using NuVatis.QueryBuilder.Ast;
using NuVatis.QueryBuilder.Rendering;

/** Task 9에서 실제 구현됩니다. 현재는 컴파일용 스텁입니다. */
public static class QueryExecutor {
    public static List<T> Fetch<T>(SelectQuery query, DbConnection conn, ISqlDialect dialect)
        where T : new()
        => throw new NotImplementedException("Task 9에서 구현됩니다.");

    public static T? FetchOne<T>(SelectQuery query, DbConnection conn, ISqlDialect dialect)
        where T : new()
        => throw new NotImplementedException("Task 9에서 구현됩니다.");

    public static Task<List<T>> FetchAsync<T>(
        SelectQuery        query,
        DbConnection       conn,
        ISqlDialect        dialect,
        CancellationToken  ct)
        where T : new()
        => throw new NotImplementedException("Task 9에서 구현됩니다.");

    public static int Execute(object query, DbConnection conn, ISqlDialect dialect)
        => throw new NotImplementedException("Task 9에서 구현됩니다.");
}
