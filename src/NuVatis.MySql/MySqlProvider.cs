using System.Data.Common;
using NuVatis.Provider;
using MySqlConnector;

namespace NuVatis.MySql;

/**
 * MySQL/MariaDB용 IDbProvider 구현.
 * MySqlConnector를 사용하여 DB 커넥션을 생성한다.
 *
 * @author 최진호
 * @date   2026-02-24
 */
[NuVatisProvider("MySql")]
public sealed class MySqlProvider : IDbProvider {
    public string Name => "MySql";

    public DbConnection CreateConnection(string connectionString) {
        return new MySqlConnection(connectionString);
    }

    public string ParameterPrefix => "@";

    public string GetParameterName(int index) => $"@p{index}";

    public string WrapIdentifier(string name) => $"`{name}`";
}
