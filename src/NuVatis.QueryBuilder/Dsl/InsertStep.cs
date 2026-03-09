namespace NuVatis.QueryBuilder.Dsl;

using System.Data.Common;
using NuVatis.QueryBuilder.Ast;
using NuVatis.QueryBuilder.Execution;
using NuVatis.QueryBuilder.Rendering;

/** INSERT 체인 빌더. Columns() → Values() → ToSql()/Execute() 순으로 체인합니다. */
public sealed class InsertStep {
    private readonly InsertQuery   _query;
    private readonly ISqlDialect   _dialect;
    private readonly DbConnection? _connection;

    internal InsertStep(InsertQuery query, ISqlDialect dialect, DbConnection? connection) {
        _query      = query;
        _dialect    = dialect;
        _connection = connection;
    }

    public InsertStep Columns(params FieldNode[] columns) { _query.Into(columns);       return this; }
    public InsertStep Values(params object?[] values)     { _query.WithValues(values);  return this; }
    public InsertStep AddRow(params object?[] values)     { _query.AddRow(values);      return this; }

    public RenderedSql ToSql() => _dialect.Render(_query);

    public int Execute() {
        EnsureConnection();
        return QueryExecutor.Execute(_query, _connection!, _dialect);
    }

    private void EnsureConnection() {
        if (_connection is null)
            throw new InvalidOperationException(
                "DbConnection이 설정되지 않았습니다. new DslContext(dialect, connection)으로 생성하세요.");
    }
}
