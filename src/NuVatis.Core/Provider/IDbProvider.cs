using System.Data.Common;

namespace NuVatis.Provider;

/**
 * 데이터베이스별 방언(dialect)을 추상화하는 인터페이스.
 * 각 DB Provider(PostgreSQL, SQL Server 등)가 이를 구현한다.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public interface IDbProvider {
    /** Provider 식별 이름 (예: "PostgreSql", "SqlServer") */
    string Name { get; }

    /** DB 커넥션을 생성한다. 커넥션은 열지 않은 상태로 반환. */
    DbConnection CreateConnection(string connectionString);

    /** 파라미터 접두사 (PostgreSQL: @, Oracle: :) */
    string ParameterPrefix { get; }

    /** 인덱스 기반 파라미터 이름 생성 (예: @p0, @p1) */
    string GetParameterName(int index);

    /** 식별자 래핑 (PostgreSQL: "name", MySQL: `name`) */
    string WrapIdentifier(string name);
}
