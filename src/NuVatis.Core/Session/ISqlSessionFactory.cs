using System.Data.Common;
using NuVatis.Configuration;

namespace NuVatis.Session;

/// <summary>
/// <see cref="ISqlSession"/>을 생성하는 팩토리 인터페이스.
/// 애플리케이션 생명주기 동안 Singleton으로 유지된다.
/// </summary>
public interface ISqlSessionFactory {

    /// <summary>새 세션을 생성한다. <paramref name="autoCommit"/>이 false이면 명시적 Commit이 필요하다.</summary>
    /// <param name="autoCommit">true이면 각 문장 실행 후 자동 커밋. 기본값은 false.</param>
    /// <returns>새로 생성된 <see cref="ISqlSession"/>.</returns>
    ISqlSession OpenSession(bool autoCommit = false);

    /// <summary>autoCommit=true인 읽기 전용 세션을 생성한다.</summary>
    /// <returns>읽기 전용 <see cref="ISqlSession"/>.</returns>
    ISqlSession OpenReadOnlySession();

    /// <summary>
    /// 배치 모드 세션을 생성한다.
    /// Insert/Update/Delete 호출 시 쿼리를 배치에 누적하고,
    /// <see cref="ISqlSession.FlushStatements()"/> 호출 시 일괄 실행한다.
    /// Select 연산은 즉시 실행된다.
    /// Commit 전 미처리 배치가 있으면 자동 Flush 후 Commit한다.
    /// </summary>
    /// <returns>배치 모드 <see cref="ISqlSession"/>.</returns>
    ISqlSession OpenBatchSession();

    /// <summary>
    /// 이미 열린 <see cref="DbConnection"/>과 선택적 <see cref="DbTransaction"/>을 사용하여 세션을 생성한다.
    /// EF Core 등 다른 프레임워크와 동일 커넥션/트랜잭션을 공유할 때 사용.
    /// <para>
    /// 반환된 세션의 특성:
    /// <list type="bullet">
    ///   <item>Dispose 시 커넥션/트랜잭션을 닫지 않음 (외부에서 관리)</item>
    ///   <item>Commit/Rollback 호출을 무시함 (외부에서 제어)</item>
    ///   <item><paramref name="transaction"/>이 null이면 autoCommit 모드로 동작</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="connection">이미 Open 상태인 <see cref="DbConnection"/>.</param>
    /// <param name="transaction">외부에서 시작한 <see cref="DbTransaction"/>. null 가능.</param>
    /// <returns>외부 커넥션에 바인딩된 <see cref="ISqlSession"/>.</returns>
    ISqlSession FromExistingConnection(DbConnection connection, DbTransaction? transaction = null);

    /// <summary>현재 팩토리의 NuVatis 설정 객체.</summary>
    NuVatisConfiguration Configuration { get; }
}
