namespace NuVatis.QueryBuilder.Dsl;

using System.Data.Common;
using NuVatis.QueryBuilder.Ast;
using NuVatis.QueryBuilder.Execution;
using NuVatis.QueryBuilder.Rendering;

/** SELECT 체인 빌더. ToSql()은 DbConnection 없이도 동작하며, Fetch/FetchAsync는 연결 필요. */
public sealed class SelectStep {
    private readonly SelectQuery   _query;
    private readonly ISqlDialect   _dialect;
    private readonly DbConnection? _connection;

    internal SelectStep(SelectQuery query, ISqlDialect dialect, DbConnection? connection) {
        _query      = query;
        _dialect    = dialect;
        _connection = connection;
    }

    public SelectStep From(TableNode table)                { _query.From(table);      return this; }
    public SelectStep Where(ConditionNode condition)       { _query.Where(condition); return this; }
    public SelectStep GroupBy(params FieldNode[] fields)   { _query.GroupBy(fields);  return this; }
    public SelectStep Having(ConditionNode condition)       { _query.Having(condition); return this; }
    public SelectStep OrderBy(params SortField[] fields)   { _query.OrderBy(fields);  return this; }
    public SelectStep Limit(int limit)                     { _query.Limit(limit);     return this; }
    public SelectStep Offset(int offset)                   { _query.Offset(offset);   return this; }

    public JoinStepWrapper InnerJoin(TableNode table) => WrapJoin(_query.InnerJoin(table));
    public JoinStepWrapper LeftJoin(TableNode table)  => WrapJoin(_query.LeftJoin(table));
    public JoinStepWrapper RightJoin(TableNode table) => WrapJoin(_query.RightJoin(table));

    private JoinStepWrapper WrapJoin(JoinStep joinStep) => new(this, joinStep);

    public RenderedSql ToSql() => _dialect.Render(_query);

    public List<T> Fetch<T>() where T : new() {
        EnsureConnection();
        return QueryExecutor.Fetch<T>(_query, _connection!, _dialect);
    }

    public T? FetchOne<T>() where T : new() {
        EnsureConnection();
        return QueryExecutor.FetchOne<T>(_query, _connection!, _dialect);
    }

    public Task<List<T>> FetchAsync<T>(CancellationToken ct = default) where T : new() {
        EnsureConnection();
        return QueryExecutor.FetchAsync<T>(_query, _connection!, _dialect, ct);
    }

    private void EnsureConnection() {
        if (_connection is null)
            throw new InvalidOperationException(
                "DbConnection이 설정되지 않았습니다. new DslContext(dialect, connection)으로 생성하세요.");
    }
}

/** InnerJoin/LeftJoin/RightJoin 호출 후 On() 을 연결하여 다시 SelectStep으로 복귀합니다. */
public sealed class JoinStepWrapper {
    private readonly SelectStep _selectStep;
    private readonly JoinStep   _joinStep;

    internal JoinStepWrapper(SelectStep selectStep, JoinStep joinStep) {
        _selectStep = selectStep;
        _joinStep   = joinStep;
    }

    public SelectStep On(ConditionNode condition) {
        _joinStep.On(condition);
        return _selectStep;
    }
}
