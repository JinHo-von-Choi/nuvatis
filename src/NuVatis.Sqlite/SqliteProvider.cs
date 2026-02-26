using System.Data.Common;
using Microsoft.Data.Sqlite;
using NuVatis.Provider;

namespace NuVatis.Sqlite;

/**
 * SQLite용 IDbProvider 구현.
 * Microsoft.Data.Sqlite를 사용하여 DB 커넥션을 생성한다.
 * Edge 컴퓨팅, 임베디드 환경, 테스트 환경에 적합하다.
 *
 * @author 최진호
 * @date   2026-02-26
 */
[NuVatisProvider("Sqlite")]
public sealed class SqliteProvider : IDbProvider {
    public string Name => "Sqlite";

    public DbConnection CreateConnection(string connectionString) {
        return new SqliteConnection(connectionString);
    }

    public string ParameterPrefix => "@";

    public string GetParameterName(int index) => $"@p{index}";

    public string WrapIdentifier(string name) => $"\"{name}\"";
}
