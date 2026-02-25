using System.Data;
using System.Data.Common;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using Dapper;
using Microsoft.Data.Sqlite;

namespace NuVatis.Benchmarks;

/**
 * SQLite in-memory 기반 읽기 벤치마크.
 * Dapper vs NuVatis (raw ADO.NET 수준) 성능 비교.
 * Phase 1에서는 SG 생성 코드가 아직 없으므로, 런타임 코어 경로의 오버헤드를 측정.
 *
 * @author 최진호
 * @date   2026-02-24
 */
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[HideColumns(Column.Error, Column.StdDev)]
public class SqliteReadBenchmark {

    private SqliteConnection _connection = null!;

    [GlobalSetup]
    public void Setup() {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE users (
                id    INTEGER PRIMARY KEY,
                name  TEXT NOT NULL,
                email TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();

        for (var i = 1; i <= 1000; i++) {
            using var insert = _connection.CreateCommand();
            insert.CommandText = $"INSERT INTO users (id, name, email) VALUES ({i}, 'User{i}', 'user{i}@test.com')";
            insert.ExecuteNonQuery();
        }
    }

    [GlobalCleanup]
    public void Cleanup() {
        _connection.Dispose();
    }

    [Benchmark(Baseline = true, Description = "Dapper: SelectOne")]
    public User? Dapper_SelectOne() {
        return _connection.QueryFirstOrDefault<User>(
            "SELECT id, name, email FROM users WHERE id = @Id", new { Id = 500 });
    }

    [Benchmark(Description = "RawADO: SelectOne")]
    public User? RawAdo_SelectOne() {
        using var cmd     = _connection.CreateCommand();
        cmd.CommandText   = "SELECT id, name, email FROM users WHERE id = @Id";
        var param         = cmd.CreateParameter();
        param.ParameterName = "@Id";
        param.Value         = 500;
        cmd.Parameters.Add(param);

        using var reader = cmd.ExecuteReader();
        if (reader.Read()) {
            return new User {
                Id    = reader.GetInt32(0),
                Name  = reader.GetString(1),
                Email = reader.GetString(2)
            };
        }
        return null;
    }

    [Benchmark(Description = "Dapper: SelectList (1000 rows)")]
    public List<User> Dapper_SelectList() {
        return _connection.Query<User>("SELECT id, name, email FROM users").AsList();
    }

    [Benchmark(Description = "RawADO: SelectList (1000 rows)")]
    public List<User> RawAdo_SelectList() {
        using var cmd   = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, name, email FROM users";

        using var reader = cmd.ExecuteReader();
        var results      = new List<User>(1000);
        while (reader.Read()) {
            results.Add(new User {
                Id    = reader.GetInt32(0),
                Name  = reader.GetString(1),
                Email = reader.GetString(2)
            });
        }
        return results;
    }

    public class User {
        public int    Id    { get; set; }
        public string Name  { get; set; } = "";
        public string Email { get; set; } = "";
    }
}
