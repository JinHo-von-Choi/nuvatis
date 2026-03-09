namespace NuVatis.QueryBuilder.Dsl;

using System.Data.Common;
using NuVatis.QueryBuilder.Ast;
using NuVatis.QueryBuilder.Rendering;

/** 사용자 접점 Fluent API 진입점. dialect와 선택적 DbConnection을 주입받아 각 Step을 생성합니다. */
public sealed class DslContext {
    private readonly ISqlDialect   _dialect;
    private readonly DbConnection? _connection;

    public DslContext(ISqlDialect dialect, DbConnection? connection = null) {
        _dialect    = dialect;
        _connection = connection;
    }

    public SelectStep Select(params FieldNode[] fields)
        => new(new SelectQuery().Select(fields), _dialect, _connection);

    public InsertStep InsertInto(TableNode table)
        => new(new InsertQuery(table), _dialect, _connection);

    public UpdateStep Update(TableNode table)
        => new(new UpdateQuery(table), _dialect, _connection);

    public DeleteStep DeleteFrom(TableNode table)
        => new(new DeleteQuery(table), _dialect, _connection);
}
