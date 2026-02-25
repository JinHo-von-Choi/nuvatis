namespace NuVatis.Attributes;

/** 정적 DELETE SQL을 인라인으로 정의한다. */
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class DeleteAttribute : Attribute {
    public string Sql { get; }
    public DeleteAttribute(string sql) => Sql = sql;
}
