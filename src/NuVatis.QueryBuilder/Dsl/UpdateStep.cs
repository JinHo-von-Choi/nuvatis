namespace NuVatis.QueryBuilder.Dsl;

using System.Data.Common;
using NuVatis.QueryBuilder.Ast;
using NuVatis.QueryBuilder.Execution;
using NuVatis.QueryBuilder.Rendering;

/** UPDATE 체인 빌더. Set() 여러 번 체인 가능하며, Where() 로 조건을 추가합니다. */
public sealed class UpdateStep {
    private readonly UpdateQuery   _query;
    private readonly ISqlDialect   _dialect;
    private readonly DbConnection? _connection;

    internal UpdateStep(UpdateQuery query, ISqlDialect dialect, DbConnection? connection) {
        _query      = query;
        _dialect    = dialect;
        _connection = connection;
    }

    public UpdateStep Set<T>(FieldNode<T> field, T value) { _query.Set(field, value); return this; }
    public UpdateStep Where(ConditionNode condition)       { _query.Where(condition);  return this; }

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
