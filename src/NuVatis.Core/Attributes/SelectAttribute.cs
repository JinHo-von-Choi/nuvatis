namespace NuVatis.Attributes;

/** 정적 SELECT SQL을 인라인으로 정의한다. */
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class SelectAttribute : Attribute {
    /// <summary>실행할 SELECT SQL 문자열.</summary>
    public string Sql { get; }

    /// <summary>
    /// <see cref="SelectAttribute"/>를 초기화한다.
    /// </summary>
    /// <param name="sql">실행할 SELECT SQL.</param>
    public SelectAttribute(string sql) => Sql = sql;
}
