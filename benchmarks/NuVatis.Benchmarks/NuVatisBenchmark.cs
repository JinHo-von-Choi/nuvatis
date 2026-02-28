using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Order;
using Dapper;
using Microsoft.Data.Sqlite;
using NuVatis.Mapping;

namespace NuVatis.Benchmarks;

/**
 * NuVatis ColumnMapper 런타임 리플렉션 경로와 Dapper / RawADO 기준선 비교.
 *
 * - Dapper            : 기준선 (Baseline=true)
 * - RawADO            : 이론적 최대 성능 (수동 매핑, 오버헤드 없음)
 * - NuVatis Runtime   : ColumnMapper.MapRow<T>() - O(1) ConcurrentDictionary 캐시 기반 리플렉션
 *
 * SQLite in-memory DB를 사용하므로 네트워크 레이턴시 없이 순수 매핑 오버헤드만 측정한다.
 * SelectOne (단건) 과 SelectList (1 000행) 두 시나리오로 커버.
 *
 * @author 최진호
 * @date   2026-02-28
 */
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[HideColumns(Column.Error, Column.StdDev)]
public class NuVatisBenchmark {

    private SqliteConnection _connection = null!;

    [GlobalSetup]
    public void Setup() {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        using var cmd   = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE bench_users (
                id    INTEGER PRIMARY KEY,
                name  TEXT    NOT NULL,
                email TEXT    NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();

        for (var i = 1; i <= 1000; i++) {
            using var ins   = _connection.CreateCommand();
            ins.CommandText = $"INSERT INTO bench_users VALUES ({i}, 'User{i}', 'u{i}@test.com')";
            ins.ExecuteNonQuery();
        }
    }

    [GlobalCleanup]
    public void Cleanup() => _connection.Dispose();

    // ─────────────────────────────────────────────
    // SelectOne 시나리오
    // ─────────────────────────────────────────────

    [Benchmark(Baseline = true, Description = "Dapper: SelectOne")]
    public BenchUser? Dapper_SelectOne() {
        return _connection.QueryFirstOrDefault<BenchUser>(
            "SELECT id, name, email FROM bench_users WHERE id = @Id",
            new { Id = 500 });
    }

    [Benchmark(Description = "RawADO: SelectOne")]
    public BenchUser? RawAdo_SelectOne() {
        using var cmd       = _connection.CreateCommand();
        cmd.CommandText     = "SELECT id, name, email FROM bench_users WHERE id = @Id";
        var param           = cmd.CreateParameter();
        param.ParameterName = "@Id";
        param.Value         = 500;
        cmd.Parameters.Add(param);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new BenchUser {
            Id    = reader.GetInt32(0),
            Name  = reader.GetString(1),
            Email = reader.GetString(2),
        };
    }

    [Benchmark(Description = "NuVatis Runtime: ColumnMapper SelectOne")]
    public BenchUser? NuVatis_Runtime_SelectOne() {
        using var cmd       = _connection.CreateCommand();
        cmd.CommandText     = "SELECT id, name, email FROM bench_users WHERE id = @p0";
        var param           = cmd.CreateParameter();
        param.ParameterName = "@p0";
        param.Value         = 500;
        cmd.Parameters.Add(param);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ColumnMapper.MapRow<BenchUser>(reader) : null;
    }

    // ─────────────────────────────────────────────
    // SelectList 시나리오 (1 000행)
    // ─────────────────────────────────────────────

    [Benchmark(Description = "Dapper: SelectList (1000 rows)")]
    public List<BenchUser> Dapper_SelectList() {
        return _connection.Query<BenchUser>(
            "SELECT id, name, email FROM bench_users").AsList();
    }

    [Benchmark(Description = "RawADO: SelectList (1000 rows)")]
    public List<BenchUser> RawAdo_SelectList() {
        using var cmd   = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, name, email FROM bench_users";

        using var reader = cmd.ExecuteReader();
        var results      = new List<BenchUser>(1000);
        while (reader.Read()) {
            results.Add(new BenchUser {
                Id    = reader.GetInt32(0),
                Name  = reader.GetString(1),
                Email = reader.GetString(2),
            });
        }
        return results;
    }

    [Benchmark(Description = "NuVatis Runtime: ColumnMapper SelectList (1000 rows)")]
    public List<BenchUser> NuVatis_Runtime_SelectList() {
        using var cmd   = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, name, email FROM bench_users";

        using var reader = cmd.ExecuteReader();
        var results      = new List<BenchUser>(1000);
        while (reader.Read()) {
            results.Add(ColumnMapper.MapRow<BenchUser>(reader));
        }
        return results;
    }

    // ─────────────────────────────────────────────
    // 대상 모델
    // ─────────────────────────────────────────────

    public class BenchUser {
        public int    Id    { get; set; }
        public string Name  { get; set; } = "";
        public string Email { get; set; } = "";
    }
}
