namespace NuVatis.Testing;

/**
 * 캡처된 쿼리 정보 레코드.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public record CapturedQuery(string StatementId, object? Parameter, string OperationType);
