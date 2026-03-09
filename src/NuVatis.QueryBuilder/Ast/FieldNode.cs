namespace NuVatis.QueryBuilder.Ast;

public abstract class FieldNode : QueryNode {
    public TableNode? Table      { get; }
    public string     ColumnName { get; }

    protected FieldNode(TableNode? table, string columnName) {
        Table      = table;
        ColumnName = columnName;
    }

    public SortField       Asc()       => new(this, SortOrder.Asc);
    public SortField       Desc()      => new(this, SortOrder.Desc);
    public IsNullCondition IsNull()    => new(this, negated: false);
    public IsNullCondition IsNotNull() => new(this, negated: true);

    // JOIN ON 절에서 필드 간 비교용 (cross-type)
    public ConditionNode Eq(FieldNode other) => new BinaryCondition(this, "=", other);
}

public sealed class FieldNode<T> : FieldNode {
    public FieldNode(TableNode table, string columnName) : base(table, columnName) { }

    public ConditionNode Eq(T value)  => new BinaryCondition(this, "=",  value);
    public ConditionNode Ne(T value)  => new BinaryCondition(this, "<>", value);
    public ConditionNode Gt(T value)  => new BinaryCondition(this, ">",  value);
    public ConditionNode Ge(T value)  => new BinaryCondition(this, ">=", value);
    public ConditionNode Lt(T value)  => new BinaryCondition(this, "<",  value);
    public ConditionNode Le(T value)  => new BinaryCondition(this, "<=", value);
    public ConditionNode Like(string pattern) => new BinaryCondition(this, "LIKE", pattern);
    public ConditionNode In(IEnumerable<T> values)
        => new InCondition(this, values.Cast<object?>());
}

/// <summary>집계 함수 FieldNode의 비제네릭 기반 클래스. 패턴 매칭 및 렌더링에 사용한다.</summary>
public abstract class AggregateFieldBase : FieldNode {
    /// <summary>SQL 함수 이름. COUNT, SUM, AVG, MAX, MIN 등.</summary>
    public string FunctionName { get; }

    protected AggregateFieldBase(string functionName, string columnExpr)
        : base(null, columnExpr) {
        FunctionName = functionName;
    }
}

/// <summary>COUNT, SUM, AVG, MAX, MIN 같은 집계 함수를 표현하는 FieldNode.</summary>
public sealed class AggregateField<T> : AggregateFieldBase {
    public AggregateField(string functionName, string columnExpr)
        : base(functionName, columnExpr) { }

    public ConditionNode Gt(T value) => new BinaryCondition(this, ">",  value);
    public ConditionNode Ge(T value) => new BinaryCondition(this, ">=", value);
    public ConditionNode Lt(T value) => new BinaryCondition(this, "<",  value);
    public ConditionNode Le(T value) => new BinaryCondition(this, "<=", value);
    public ConditionNode Eq(T value) => new BinaryCondition(this, "=",  value);
    public ConditionNode Ne(T value) => new BinaryCondition(this, "<>", value);
}

/// <summary>집계 함수 FieldNode 생성 팩토리.</summary>
public static class Agg {
    public static AggregateField<int>     Count(string col = "*")  => new("COUNT", col);
    public static AggregateField<long>    CountL(string col = "*") => new("COUNT", col);
    public static AggregateField<decimal> Sum(string col)          => new("SUM",   col);
    public static AggregateField<double>  Avg(string col)          => new("AVG",   col);
    public static AggregateField<T>       Max<T>(string col)       => new("MAX",   col);
    public static AggregateField<T>       Min<T>(string col)       => new("MIN",   col);
}
