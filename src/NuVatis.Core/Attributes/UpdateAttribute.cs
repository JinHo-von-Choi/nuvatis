namespace NuVatis.Attributes;

/** 정적 UPDATE SQL을 인라인으로 정의한다. */
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class UpdateAttribute : Attribute {
    public string Sql { get; }
    public UpdateAttribute(string sql) => Sql = sql;
}
