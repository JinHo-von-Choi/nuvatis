using System.Data.Common;
using Microsoft.Data.Sqlite;
using NuVatis.Configuration;
using NuVatis.Executor;
using NuVatis.Provider;
using NuVatis.Session;
using NuVatis.Statement;
using NuVatis.Testing;
using NuVatis.Transaction;

/**
 * NuVatis Sample Application
 *
 * SQLite 인메모리 DB를 사용하여 NuVatis의 핵심 기능을 시연한다.
 *
 * @author 최진호
 * @date   2026-02-24
 */

Console.WriteLine("=== NuVatis Sample Application ===\n");

var provider = new SqliteProvider();
var connStr  = "Data Source=SampleDb;Mode=Memory;Cache=Shared";

var keepAlive = new SqliteConnection(connStr);
keepAlive.Open();

SetupDatabase(keepAlive);

var config = new NuVatisConfiguration {
    DataSource = new DataSourceConfig {
        ProviderName     = "Sqlite",
        ConnectionString = connStr
    },
    Statements = {
        ["User.SelectAll"] = new MappedStatement {
            Id = "SelectAll", Namespace = "User",
            Type = StatementType.Select,
            SqlSource = "SELECT id, name, email, age FROM users ORDER BY id"
        },
        ["User.SelectById"] = new MappedStatement {
            Id = "SelectById", Namespace = "User",
            Type = StatementType.Select,
            SqlSource = "SELECT id, name, email, age FROM users WHERE id = 1"
        },
        ["User.Count"] = new MappedStatement {
            Id = "Count", Namespace = "User",
            Type = StatementType.Select,
            SqlSource = "SELECT COUNT(*) FROM users"
        },
        ["User.Insert"] = new MappedStatement {
            Id = "Insert", Namespace = "User",
            Type = StatementType.Insert,
            SqlSource = "INSERT INTO users (name, email, age) VALUES ('David', 'david@test.com', 28)"
        },
        ["User.Delete"] = new MappedStatement {
            Id = "Delete", Namespace = "User",
            Type = StatementType.Delete,
            SqlSource = "DELETE FROM users WHERE name = 'David'"
        }
    }
};

var factory = new SqlSessionFactory(config, provider);

DemoBasicCrud(factory, provider, connStr);
DemoTransaction(factory, provider, connStr);
DemoInMemorySession();

keepAlive.Dispose();
Console.WriteLine("\n=== Sample 완료 ===");

static void SetupDatabase(SqliteConnection conn) {
    using var cmd = conn.CreateCommand();
    cmd.CommandText = """
        CREATE TABLE users (
            id    INTEGER PRIMARY KEY AUTOINCREMENT,
            name  TEXT NOT NULL,
            email TEXT NOT NULL,
            age   INTEGER NOT NULL
        );
        INSERT INTO users (name, email, age) VALUES ('Alice', 'alice@test.com', 30);
        INSERT INTO users (name, email, age) VALUES ('Bob', 'bob@test.com', 25);
        INSERT INTO users (name, email, age) VALUES ('Charlie', 'charlie@test.com', 35);
    """;
    cmd.ExecuteNonQuery();
    Console.WriteLine("[Setup] 테이블 생성 및 초기 데이터 3건 삽입 완료\n");
}

static void DemoBasicCrud(SqlSessionFactory factory, IDbProvider provider, string connStr) {
    Console.WriteLine("--- 1. Basic CRUD ---");

    var tx  = new AdoTransaction(provider, connStr, autoCommit: true);
    using var executor = new SimpleExecutor(tx);

    var count = executor.SelectOne(
        new MappedStatement { Id = "Count", Namespace = "User", Type = StatementType.Select, SqlSource = "SELECT COUNT(*) FROM users" },
        "SELECT COUNT(*) FROM users",
        Array.Empty<DbParameter>(),
        r => r.GetInt64(0));
    Console.WriteLine($"  전체 사용자 수: {count}");

    using var session = factory.OpenSession(autoCommit: true);
    var affected = session.Insert("User.Insert");
    Console.WriteLine($"  INSERT 영향 행: {affected}");

    var count2 = executor.SelectOne(
        new MappedStatement { Id = "Count", Namespace = "User", Type = StatementType.Select, SqlSource = "SELECT COUNT(*) FROM users" },
        "SELECT COUNT(*) FROM users",
        Array.Empty<DbParameter>(),
        r => r.GetInt64(0));
    Console.WriteLine($"  INSERT 후 사용자 수: {count2}");

    using var session2 = factory.OpenSession(autoCommit: true);
    var deleted = session2.Delete("User.Delete");
    Console.WriteLine($"  DELETE 영향 행: {deleted}\n");
}

static void DemoTransaction(SqlSessionFactory factory, IDbProvider provider, string connStr) {
    Console.WriteLine("--- 2. Transaction (Commit / Rollback) ---");

    using (var session = factory.OpenSession(autoCommit: false)) {
        session.Insert("User.Insert");
        session.Rollback();
        Console.WriteLine("  INSERT + Rollback 실행");
    }

    var tx1 = new AdoTransaction(provider, connStr, autoCommit: true);
    using var exe1 = new SimpleExecutor(tx1);
    var c1 = exe1.SelectOne(
        new MappedStatement { Id = "Count", Namespace = "User", Type = StatementType.Select, SqlSource = "SELECT COUNT(*) FROM users" },
        "SELECT COUNT(*) FROM users",
        Array.Empty<DbParameter>(),
        r => r.GetInt64(0));
    Console.WriteLine($"  Rollback 후 사용자 수: {c1} (변경 없음)");

    using (var session = factory.OpenSession(autoCommit: false)) {
        session.Insert("User.Insert");
        session.Commit();
        Console.WriteLine("  INSERT + Commit 실행");
    }

    var tx2 = new AdoTransaction(provider, connStr, autoCommit: true);
    using var exe2 = new SimpleExecutor(tx2);
    var c2 = exe2.SelectOne(
        new MappedStatement { Id = "Count", Namespace = "User", Type = StatementType.Select, SqlSource = "SELECT COUNT(*) FROM users" },
        "SELECT COUNT(*) FROM users",
        Array.Empty<DbParameter>(),
        r => r.GetInt64(0));
    Console.WriteLine($"  Commit 후 사용자 수: {c2} (1건 증가)\n");
}

static void DemoInMemorySession() {
    Console.WriteLine("--- 3. InMemorySqlSession (Testing) ---");

    var session = new InMemorySqlSession();
    session.Setup("User.Count", 42L);
    session.SetupList("User.SelectAll", (IList<string>)new List<string> { "Alice", "Bob" });

    var count = session.SelectOne<long>("User.Count");
    Console.WriteLine($"  Mock SelectOne: {count}");

    var users = session.SelectList<string>("User.SelectAll");
    Console.WriteLine($"  Mock SelectList: [{string.Join(", ", users)}]");

    Console.WriteLine($"  캡처된 쿼리 수: {session.CapturedQueries.Count}");
    Console.WriteLine($"  HasQuery(User.Count): {QueryCapture.HasQuery(session, "User.Count")}");

    var last = QueryCapture.LastQuery(session);
    Console.WriteLine($"  LastQuery: {last?.StatementId} ({last?.OperationType})\n");
}

/**
 * SQLite용 IDbProvider.
 */
sealed class SqliteProvider : IDbProvider {
    public string Name => "Sqlite";
    public DbConnection CreateConnection(string connectionString) =>
        new SqliteConnection(connectionString);
    public string ParameterPrefix => "@";
    public string GetParameterName(int index) => $"@p{index}";
    public string WrapIdentifier(string name) => $"\"{name}\"";
}
