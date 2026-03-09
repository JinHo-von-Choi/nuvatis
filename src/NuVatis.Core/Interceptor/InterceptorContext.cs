using System.Data.Common;
using NuVatis.Statement;

namespace NuVatis.Interceptor;

/**
 * 인터셉터에 전달되는 SQL 실행 컨텍스트.
 * Sql, Parameters는 BeforeExecute에서 수정 가능하여 쿼리 변환에 활용된다.
 *
 * @author 최진호
 * @date   2026-02-24
 */
/// <summary>인터셉터에 전달되는 SQL 실행 컨텍스트. BeforeExecute와 AfterExecute 양쪽에서 공유된다.</summary>
public sealed class InterceptorContext {
    /// <summary>Gets or sets the fully-qualified statement identifier (namespace.id).</summary>
    public required string                     StatementId         { get; set; }
    /// <summary>Gets or sets the SQL string to be executed. BeforeExecute에서 수정 가능하다.</summary>
    public required string                     Sql                 { get; set; }
    /// <summary>Gets or sets the parameter list bound to the SQL. BeforeExecute에서 수정 가능하다.</summary>
    public required IReadOnlyList<DbParameter> Parameters          { get; set; }
    /// <summary>Gets or sets the original parameter object passed by the caller.</summary>
    public object?                             Parameter           { get; set; }
    /// <summary>Gets or sets the type of the SQL statement being executed.</summary>
    public StatementType                       StatementType       { get; set; }
    /// <summary>Gets or sets the elapsed time in milliseconds. AfterExecute 시점에 설정된다.</summary>
    public long                                ElapsedMilliseconds { get; set; }
    /// <summary>Gets or sets the number of rows affected. DML 실행 후 AfterExecute 시점에 설정된다.</summary>
    public int?                                AffectedRows        { get; set; }
    /// <summary>Gets or sets the exception that occurred during execution. 정상 실행 시 null이다.</summary>
    public Exception?                          Exception           { get; set; }

    /**
     * 인터셉터 간 데이터 전달용 딕셔너리.
     * BeforeExecute에서 저장한 값을 AfterExecute에서 참조할 때 사용.
     * 예: OpenTelemetry Activity 인스턴스 전달.
     */
    public Dictionary<string, object?> Items { get; } = new();
}
