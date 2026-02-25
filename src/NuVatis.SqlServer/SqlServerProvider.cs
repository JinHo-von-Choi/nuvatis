using System.Data.Common;
using Microsoft.Data.SqlClient;
using NuVatis.Provider;

namespace NuVatis.SqlServer;

/**
 * SQL Server용 IDbProvider 구현.
 * Microsoft.Data.SqlClient를 사용하여 DB 커넥션을 생성한다.
 *
 * @author 최진호
 * @date   2026-02-24
 */
[NuVatisProvider("SqlServer")]
public sealed class SqlServerProvider : IDbProvider {
    public string Name => "SqlServer";

    public DbConnection CreateConnection(string connectionString) {
        return new SqlConnection(connectionString);
    }

    public string ParameterPrefix => "@";

    public string GetParameterName(int index) => $"@p{index}";

    public string WrapIdentifier(string name) => $"[{name}]";
}
