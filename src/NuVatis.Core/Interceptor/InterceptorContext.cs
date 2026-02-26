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
public sealed class InterceptorContext {
    public required string                     StatementId         { get; set; }
    public required string                     Sql                 { get; set; }
    public required IReadOnlyList<DbParameter> Parameters          { get; set; }
    public object?                             Parameter           { get; set; }
    public StatementType                       StatementType       { get; set; }
    public long                                ElapsedMilliseconds { get; set; }
    public int?                                AffectedRows        { get; set; }
    public Exception?                          Exception           { get; set; }

    /**
     * 인터셉터 간 데이터 전달용 딕셔너리.
     * BeforeExecute에서 저장한 값을 AfterExecute에서 참조할 때 사용.
     * 예: OpenTelemetry Activity 인스턴스 전달.
     */
    public Dictionary<string, object?> Items { get; } = new();
}
