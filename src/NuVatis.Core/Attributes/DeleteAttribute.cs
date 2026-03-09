namespace NuVatis.Attributes;

/** 정적 DELETE SQL을 인라인으로 정의한다. */
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class DeleteAttribute : Attribute {
    /// <summary>실행할 DELETE SQL 문자열.</summary>
    public string Sql { get; }

    /// <summary>
    /// <see cref="DeleteAttribute"/>를 초기화한다.
    /// </summary>
    /// <param name="sql">실행할 DELETE SQL.</param>
    public DeleteAttribute(string sql) => Sql = sql;
}
