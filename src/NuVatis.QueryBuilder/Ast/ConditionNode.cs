namespace NuVatis.QueryBuilder.Ast;

public abstract class ConditionNode : QueryNode {
    public ConditionNode And(ConditionNode other) => new AndCondition(this, other);
    public ConditionNode Or(ConditionNode other)  => new OrCondition(this, other);
    public ConditionNode Not()                    => new NotCondition(this);
}

public sealed class BinaryCondition : ConditionNode {
    public FieldNode Field    { get; }
    public string    Operator { get; }
    public object?   Value    { get; }

    internal BinaryCondition(FieldNode field, string op, object? value) {
        Field    = field;
        Operator = op;
        Value    = value;
    }
}

public sealed class IsNullCondition : ConditionNode {
    public FieldNode Field   { get; }
    public bool      Negated { get; }

    internal IsNullCondition(FieldNode field, bool negated = false) {
        Field   = field;
        Negated = negated;
    }
}

public sealed class InCondition : ConditionNode {
    public FieldNode             Field  { get; }
    public IReadOnlyList<object?> Values { get; }

    internal InCondition(FieldNode field, IEnumerable<object?> values) {
        Field  = field;
        Values = values.ToList();
    }
}

public sealed class AndCondition : ConditionNode {
    public ConditionNode Left  { get; }
    public ConditionNode Right { get; }

    internal AndCondition(ConditionNode left, ConditionNode right) {
        Left  = left;
        Right = right;
    }
}

public sealed class OrCondition : ConditionNode {
    public ConditionNode Left  { get; }
    public ConditionNode Right { get; }

    internal OrCondition(ConditionNode left, ConditionNode right) {
        Left  = left;
        Right = right;
    }
}

public sealed class NotCondition : ConditionNode {
    public ConditionNode Inner { get; }

    internal NotCondition(ConditionNode inner) {
        Inner = inner;
    }
}
