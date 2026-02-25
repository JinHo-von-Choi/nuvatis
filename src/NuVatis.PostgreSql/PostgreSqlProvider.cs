using System.Data.Common;
using NuVatis.Provider;
using Npgsql;

namespace NuVatis.PostgreSql;

/**
 * PostgreSQL용 IDbProvider 구현.
 * Npgsql을 사용하여 DB 커넥션을 생성한다.
 *
 * @author 최진호
 * @date   2026-02-24
 */
[NuVatisProvider("PostgreSql")]
public sealed class PostgreSqlProvider : IDbProvider {
    public string Name => "PostgreSql";

    public DbConnection CreateConnection(string connectionString) {
        return new NpgsqlConnection(connectionString);
    }

    public string ParameterPrefix => "@";

    public string GetParameterName(int index) => $"@p{index}";

    public string WrapIdentifier(string name) => $"\"{name}\"";
}
