using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Order;
using Dapper;
using Microsoft.Data.Sqlite;

namespace NuVatis.Benchmarks;

/**
 * SQLite in-memory 기반 쓰기 벤치마크.
 * Insert / BatchInsert 시나리오를 Dapper와 비교한다.
 *
 * @author 최진호
 * @date   2026-02-26
 */
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[HideColumns(Column.Error, Column.StdDev)]
public class SqliteWriteBenchmark {

    private SqliteConnection _connection = null!;

    [GlobalSetup]
    public void Setup() {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE bench_items (
                id    INTEGER PRIMARY KEY AUTOINCREMENT,
                name  TEXT NOT NULL,
                value INTEGER NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    [IterationSetup]
    public void IterationSetup() {
        using var cmd   = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM bench_items";
        cmd.ExecuteNonQuery();
    }

    [GlobalCleanup]
    public void Cleanup() {
        _connection.Dispose();
    }

    [Benchmark(Baseline = true, Description = "Dapper: Insert x100")]
    public void Dapper_Insert100() {
        for (var i = 0; i < 100; i++) {
            _connection.Execute(
                "INSERT INTO bench_items (name, value) VALUES (@Name, @Value)",
                new { Name = $"item{i}", Value = i });
        }
    }

    [Benchmark(Description = "RawADO: Insert x100")]
    public void RawAdo_Insert100() {
        for (var i = 0; i < 100; i++) {
            using var cmd   = _connection.CreateCommand();
            cmd.CommandText = "INSERT INTO bench_items (name, value) VALUES (@Name, @Value)";

            var pName         = cmd.CreateParameter();
            pName.ParameterName = "@Name";
            pName.Value         = $"item{i}";
            cmd.Parameters.Add(pName);

            var pValue         = cmd.CreateParameter();
            pValue.ParameterName = "@Value";
            pValue.Value         = i;
            cmd.Parameters.Add(pValue);

            cmd.ExecuteNonQuery();
        }
    }

    [Benchmark(Description = "Dapper: BatchInsert (Transaction) x100")]
    public void Dapper_BatchInsert100() {
        using var tx = _connection.BeginTransaction();
        for (var i = 0; i < 100; i++) {
            _connection.Execute(
                "INSERT INTO bench_items (name, value) VALUES (@Name, @Value)",
                new { Name = $"batch{i}", Value = i }, tx);
        }
        tx.Commit();
    }

    [Benchmark(Description = "RawADO: BatchInsert (Transaction) x100")]
    public void RawAdo_BatchInsert100() {
        using var tx = _connection.BeginTransaction();
        for (var i = 0; i < 100; i++) {
            using var cmd   = _connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO bench_items (name, value) VALUES (@Name, @Value)";

            var pName         = cmd.CreateParameter();
            pName.ParameterName = "@Name";
            pName.Value         = $"batch{i}";
            cmd.Parameters.Add(pName);

            var pValue         = cmd.CreateParameter();
            pValue.ParameterName = "@Value";
            pValue.Value         = i;
            cmd.Parameters.Add(pValue);

            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }
}
