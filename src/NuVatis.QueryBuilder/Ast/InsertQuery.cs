namespace NuVatis.QueryBuilder.Ast;

public sealed class InsertQuery : QueryNode {
    private readonly List<IReadOnlyList<object?>> _rows = [];

    public TableNode                Table   { get; }
    public IReadOnlyList<FieldNode> Columns { get; private set; } = [];

    /// <summary>단일 행 VALUES (Legacy 호환). 다중 행은 Rows를 사용하라.</summary>
    public IReadOnlyList<object?> Values => _rows.Count == 1 ? _rows[0] : [];

    /// <summary>모든 행의 VALUES 목록.</summary>
    public IReadOnlyList<IReadOnlyList<object?>> Rows => _rows;

    public InsertQuery(TableNode table) {
        Table = table;
    }

    public InsertQuery Into(params FieldNode[] columns) {
        Columns = columns;
        return this;
    }

    /// <summary>단일 행 지정 (Legacy API). 내부적으로 AddRow를 사용한다.</summary>
    public InsertQuery WithValues(params object?[] values) {
        _rows.Clear();
        _rows.Add(values);
        return this;
    }

    /// <summary>행 추가 (BULK INSERT용). 여러 번 호출하여 다중 행을 지정한다.</summary>
    public InsertQuery AddRow(params object?[] values) {
        _rows.Add(values);
        return this;
    }
}
