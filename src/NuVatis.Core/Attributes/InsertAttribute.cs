namespace NuVatis.Attributes;

/** 정적 INSERT SQL을 인라인으로 정의한다. */
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class InsertAttribute : Attribute {
    public string Sql { get; }
    public InsertAttribute(string sql) => Sql = sql;
}
