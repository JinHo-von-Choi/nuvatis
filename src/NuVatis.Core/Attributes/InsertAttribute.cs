namespace NuVatis.Attributes;

/** 정적 INSERT SQL을 인라인으로 정의한다. */
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class InsertAttribute : Attribute {
    /// <summary>실행할 INSERT SQL 문자열.</summary>
    public string Sql { get; }

    /// <summary>
    /// <see cref="InsertAttribute"/>를 초기화한다.
    /// </summary>
    /// <param name="sql">실행할 INSERT SQL.</param>
    public InsertAttribute(string sql) => Sql = sql;
}
