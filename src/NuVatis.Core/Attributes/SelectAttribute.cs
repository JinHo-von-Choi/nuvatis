namespace NuVatis.Attributes;

/** 정적 SELECT SQL을 인라인으로 정의한다. */
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class SelectAttribute : Attribute {
    public string Sql { get; }
    public SelectAttribute(string sql) => Sql = sql;
}
