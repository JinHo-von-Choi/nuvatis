namespace NuVatis.Testing;

/**
 * 쿼리 캡처 검증 유틸리티.
 * InMemorySqlSession과 함께 사용하여 쿼리 실행 여부를 검증한다.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public static class QueryCapture {

    /**
     * 세션에 캡처된 모든 쿼리를 반환한다.
     */
    public static IReadOnlyList<CapturedQuery> Of(InMemorySqlSession session) {
        return session.CapturedQueries;
    }

    /**
     * 특정 statementId의 쿼리가 실행되었는지 확인한다.
     */
    public static bool HasQuery(InMemorySqlSession session, string statementId) {
        return session.CapturedQueries.Any(q => q.StatementId == statementId);
    }

    /**
     * 특정 statementId의 쿼리 실행 횟수를 반환한다.
     */
    public static int QueryCount(InMemorySqlSession session, string statementId) {
        return session.CapturedQueries.Count(q => q.StatementId == statementId);
    }

    /**
     * 마지막으로 실행된 쿼리를 반환한다.
     */
    public static CapturedQuery? LastQuery(InMemorySqlSession session) {
        return session.CapturedQueries.LastOrDefault();
    }
}
