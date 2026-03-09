namespace NuVatis.Attributes;

/** 정적 UPDATE SQL을 인라인으로 정의한다. */
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class UpdateAttribute : Attribute {
    /// <summary>실행할 UPDATE SQL 문자열.</summary>
    public string Sql { get; }

    /// <summary>
    /// <see cref="UpdateAttribute"/>를 초기화한다.
    /// </summary>
    /// <param name="sql">실행할 UPDATE SQL.</param>
    public UpdateAttribute(string sql) => Sql = sql;
}
