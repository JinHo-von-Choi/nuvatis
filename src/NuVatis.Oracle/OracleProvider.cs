using System.Data.Common;
using NuVatis.Provider;
using Oracle.ManagedDataAccess.Client;

namespace NuVatis.Oracle;

/**
 * Oracle용 IDbProvider 구현.
 * Oracle.ManagedDataAccess.Core를 사용하여 DB 커넥션을 생성한다.
 *
 * @author 최진호
 * @date   2026-03-09
 */
[NuVatisProvider("Oracle")]
public sealed class OracleProvider : IDbProvider {
    public string Name => "Oracle";

    public DbConnection CreateConnection(string connectionString) {
        return new OracleConnection(connectionString);
    }

    public string ParameterPrefix => ":";

    public string GetParameterName(int index) => $":p{index}";

    public string WrapIdentifier(string name) => $"\"{name}\"";
}
