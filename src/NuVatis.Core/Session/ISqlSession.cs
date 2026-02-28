using System.Data.Common;
using NuVatis.Mapping;

namespace NuVatis.Session;

/// <summary>
/// SQL 세션 인터페이스.
/// 쿼리 실행, 트랜잭션 관리, Mapper 인스턴스 획득을 담당한다.
/// 기본 동작은 autoCommit=false (MyBatis 호환).
/// <para>Thread-safe하지 않음. 병렬 처리 시 별도 세션 사용 필요.</para>
/// </summary>
public interface ISqlSession : IDisposable, IAsyncDisposable {

    /// <summary>단일 행을 조회하여 <typeparamref name="T"/>로 매핑한다. 결과가 없으면 <c>default(T)</c>를 반환한다.</summary>
    /// <typeparam name="T">결과 매핑 대상 타입.</typeparam>
    /// <param name="statementId">XML 매퍼의 statement id. 형식: <c>Namespace.Id</c></param>
    /// <param name="parameter">바인딩 파라미터 객체. null이면 파라미터 없음.</param>
    /// <returns>매핑된 결과 객체. 결과가 없으면 <c>default(T)</c>.</returns>
    T? SelectOne<T>(string statementId, object? parameter = null);

    /// <inheritdoc cref="SelectOne{T}(string, object?)"/>
    /// <param name="ct">취소 토큰.</param>
    Task<T?> SelectOneAsync<T>(string statementId, object? parameter = null, CancellationToken ct = default);

    /// <summary>복수 행을 조회하여 <see cref="IList{T}"/>로 매핑한다.</summary>
    /// <typeparam name="T">결과 매핑 대상 타입.</typeparam>
    /// <param name="statementId">XML 매퍼의 statement id. 형식: <c>Namespace.Id</c></param>
    /// <param name="parameter">바인딩 파라미터 객체. null이면 파라미터 없음.</param>
    /// <returns>매핑된 결과 목록. 결과가 없으면 빈 목록.</returns>
    IList<T> SelectList<T>(string statementId, object? parameter = null);

    /// <inheritdoc cref="SelectList{T}(string, object?)"/>
    /// <param name="ct">취소 토큰.</param>
    Task<IList<T>> SelectListAsync<T>(string statementId, object? parameter = null, CancellationToken ct = default);

    /// <summary>
    /// 대용량 결과를 <see cref="IAsyncEnumerable{T}"/>로 스트리밍 반환한다.
    /// 결과를 <see cref="IList{T}"/>에 적재하지 않고 yield return으로 소비하여 메모리 사용을 최소화한다.
    /// 열거가 완료되거나 취소될 때까지 세션의 커넥션이 유지된다.
    /// </summary>
    /// <typeparam name="T">결과 매핑 대상 타입.</typeparam>
    /// <param name="statementId">XML 매퍼의 statement id. 형식: <c>Namespace.Id</c></param>
    /// <param name="parameter">바인딩 파라미터 객체. null이면 파라미터 없음.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>행 단위로 스트리밍되는 <see cref="IAsyncEnumerable{T}"/>.</returns>
    IAsyncEnumerable<T> SelectStream<T>(string statementId, object? parameter = null, CancellationToken ct = default);

    /// <summary>
    /// Multi-ResultSet 쿼리를 실행하고 <see cref="ResultSetGroup"/>을 반환한다.
    /// 반환된 <see cref="ResultSetGroup"/>에서 Read/ReadList를 순서대로 호출하여 각 결과셋을 소비한다.
    /// 사용 완료 후 <see cref="ResultSetGroup"/>을 반드시 Dispose해야 한다.
    /// </summary>
    /// <param name="statementId">XML 매퍼의 statement id. 형식: <c>Namespace.Id</c></param>
    /// <param name="parameter">바인딩 파라미터 객체. null이면 파라미터 없음.</param>
    /// <returns>순차 소비 가능한 <see cref="ResultSetGroup"/>.</returns>
    ResultSetGroup SelectMultiple(string statementId, object? parameter = null);

    /// <inheritdoc cref="SelectMultiple(string, object?)"/>
    /// <param name="ct">취소 토큰.</param>
    Task<ResultSetGroup> SelectMultipleAsync(string statementId, object? parameter = null, CancellationToken ct = default);

    /// <summary>INSERT 문을 실행하고 영향받은 행 수를 반환한다.</summary>
    /// <param name="statementId">XML 매퍼의 statement id. 형식: <c>Namespace.Id</c></param>
    /// <param name="parameter">바인딩 파라미터 객체. null이면 파라미터 없음.</param>
    /// <returns>영향받은 행 수.</returns>
    int Insert(string statementId, object? parameter = null);

    /// <inheritdoc cref="Insert(string, object?)"/>
    /// <param name="ct">취소 토큰.</param>
    Task<int> InsertAsync(string statementId, object? parameter = null, CancellationToken ct = default);

    /// <summary>UPDATE 문을 실행하고 영향받은 행 수를 반환한다.</summary>
    /// <param name="statementId">XML 매퍼의 statement id. 형식: <c>Namespace.Id</c></param>
    /// <param name="parameter">바인딩 파라미터 객체. null이면 파라미터 없음.</param>
    /// <returns>영향받은 행 수.</returns>
    int Update(string statementId, object? parameter = null);

    /// <inheritdoc cref="Update(string, object?)"/>
    /// <param name="ct">취소 토큰.</param>
    Task<int> UpdateAsync(string statementId, object? parameter = null, CancellationToken ct = default);

    /// <summary>DELETE 문을 실행하고 영향받은 행 수를 반환한다.</summary>
    /// <param name="statementId">XML 매퍼의 statement id. 형식: <c>Namespace.Id</c></param>
    /// <param name="parameter">바인딩 파라미터 객체. null이면 파라미터 없음.</param>
    /// <returns>영향받은 행 수.</returns>
    int Delete(string statementId, object? parameter = null);

    /// <inheritdoc cref="Delete(string, object?)"/>
    /// <param name="ct">취소 토큰.</param>
    Task<int> DeleteAsync(string statementId, object? parameter = null, CancellationToken ct = default);

    /// <summary>현재 트랜잭션을 커밋한다.</summary>
    void Commit();

    /// <inheritdoc cref="Commit()"/>
    /// <param name="ct">취소 토큰.</param>
    Task CommitAsync(CancellationToken ct = default);

    /// <summary>현재 트랜잭션을 롤백한다.</summary>
    void Rollback();

    /// <inheritdoc cref="Rollback()"/>
    /// <param name="ct">취소 토큰.</param>
    Task RollbackAsync(CancellationToken ct = default);

    /// <summary>SG가 생성한 Mapper 인스턴스를 반환한다.</summary>
    /// <typeparam name="T">Mapper 인터페이스 타입.</typeparam>
    /// <returns>소스 제너레이터가 생성한 Mapper 구현체.</returns>
    T GetMapper<T>() where T : class;

    /// <summary>
    /// 트랜잭션 내에서 작업을 실행한다.
    /// 성공 시 자동 Commit, 예외 시 자동 Rollback 후 rethrow.
    /// </summary>
    /// <param name="action">트랜잭션 범위 내에서 실행할 비동기 작업.</param>
    /// <param name="ct">취소 토큰.</param>
    Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default);

    /// <summary>
    /// 배치 모드에서 누적된 Write 쿼리를 일괄 실행한다.
    /// 배치 모드가 아닌 세션에서 호출하면 0을 반환한다.
    /// </summary>
    /// <returns>영향받은 총 행 수.</returns>
    int FlushStatements();

    /// <summary>FlushStatements의 비동기 버전.</summary>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>영향받은 총 행 수.</returns>
    Task<int> FlushStatementsAsync(CancellationToken ct = default);

    /// <summary>현재 세션이 배치 모드인지 여부.</summary>
    bool IsBatchMode { get; }

    /// <summary>
    /// SG 생성 매핑 함수를 사용하여 단일 행을 조회한다.
    /// 런타임 리플렉션 매핑을 우회하여 성능을 개선한다.
    /// </summary>
    /// <typeparam name="T">결과 매핑 대상 타입.</typeparam>
    /// <param name="statementId">XML 매퍼의 statement id. 형식: <c>Namespace.Id</c></param>
    /// <param name="parameter">바인딩 파라미터 객체. null이면 파라미터 없음.</param>
    /// <param name="mapper">SG가 생성한 <see cref="DbDataReader"/> → <typeparamref name="T"/> 매핑 함수.</param>
    /// <returns>매핑된 결과 객체. 결과가 없으면 <c>default(T)</c>.</returns>
    T? SelectOne<T>(string statementId, object? parameter, Func<DbDataReader, T> mapper);

    /// <inheritdoc cref="SelectOne{T}(string, object?, Func{DbDataReader, T})"/>
    /// <param name="ct">취소 토큰.</param>
    Task<T?> SelectOneAsync<T>(string statementId, object? parameter, Func<DbDataReader, T> mapper, CancellationToken ct = default);

    /// <summary>
    /// SG 생성 매핑 함수를 사용하여 복수 행을 조회한다.
    /// 런타임 리플렉션 매핑을 우회하여 성능을 개선한다.
    /// </summary>
    /// <typeparam name="T">결과 매핑 대상 타입.</typeparam>
    /// <param name="statementId">XML 매퍼의 statement id. 형식: <c>Namespace.Id</c></param>
    /// <param name="parameter">바인딩 파라미터 객체. null이면 파라미터 없음.</param>
    /// <param name="mapper">SG가 생성한 <see cref="DbDataReader"/> → <typeparamref name="T"/> 매핑 함수.</param>
    /// <returns>매핑된 결과 목록. 결과가 없으면 빈 목록.</returns>
    IList<T> SelectList<T>(string statementId, object? parameter, Func<DbDataReader, T> mapper);

    /// <inheritdoc cref="SelectList{T}(string, object?, Func{DbDataReader, T})"/>
    /// <param name="ct">취소 토큰.</param>
    Task<IList<T>> SelectListAsync<T>(string statementId, object? parameter, Func<DbDataReader, T> mapper, CancellationToken ct = default);
}
