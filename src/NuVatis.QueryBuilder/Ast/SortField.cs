namespace NuVatis.QueryBuilder.Ast;

public enum SortOrder { Asc, Desc }

public sealed class SortField {
    public FieldNode Field { get; }
    public SortOrder Order { get; }

    internal SortField(FieldNode field, SortOrder order) {
        Field = field;
        Order = order;
    }
}
