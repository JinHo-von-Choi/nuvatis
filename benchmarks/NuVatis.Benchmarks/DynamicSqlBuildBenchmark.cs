using System.Data.Common;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Order;
using Microsoft.Data.Sqlite;
using NuVatis.Binding;

namespace NuVatis.Benchmarks;

/**
 * 동적 SQL 조립 경로의 리플렉션 비용 비중 측정.
 *
 * BuildSql_Search 본문은 GeneratorGoldenSnapshotTests가 방출하는 실제 생성 코드에서
 * 그대로 옮긴 것이며, __getprop_ 호출 횟수(5회)까지 원본과 동일하다.
 * 기준선 해시: 14db368de9a452b7c290920aac07ed063ecbb4e3691084be9d5bcd43bbe17db9
 * (where 조건 사이 구분 공백 수정 반영본 — 수정이 없으면 2개 조건이 겹칠 때
 *  "WHERE name = @p0AND age >= @p1" 이 되어 FullQuery 계열이 SQL 문법 오류로 실패한다)
 *
 * 측정 목적은 "제거 가능한 리플렉션 비용이 실제 쿼리 경로에서 얼마나 되는가" 이다.
 * - Generated_Current    : 현재 방출 코드 (__getprop_ 리플렉션)
 * - Proposed_DirectAccess : B안 — 심볼이 해석되면 정적 타입으로 직접 접근
 * - FullQuery_Current    : 위 조립 + SQLite 인메모리 실행 (분모)
 * - FullQuery_DirectAccess: 위 조립 + SQLite 인메모리 실행 (분자 후보)
 *
 * 주의: 정적 타입으로 바꾸면 __getprop_의 object? 반환이 string?/int? 로 좁혀져
 * "??" DBNull.Value 가 컴파일되지 않는다. 그래서 (object?) 캐스트가 들어가 있다.
 *
 * @author 최진호
 * @date   2026-10-04
 */
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[HideColumns(Column.Error, Column.StdDev)]
public class DynamicSqlBuildBenchmark {

    private SqliteConnection _connection = null!;
    private SearchParam      _param       = null!;

    [GlobalSetup]
    public void Setup() {
        _param = new SearchParam {
            Name   = "Search",
            MinAge = 30,
            Status = "open",
            SortBy = "age",
        };

        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        using var cmd   = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE orders (
                id     INTEGER PRIMARY KEY,
                name   TEXT    NOT NULL,
                age    INTEGER NOT NULL,
                status TEXT    NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();

        for (var i = 1; i <= 200; i++) {
            using var ins   = _connection.CreateCommand();
            ins.CommandText = $"INSERT INTO orders VALUES ({i}, 'Order{i}', {20 + i % 50}, 'open')";
            ins.ExecuteNonQuery();
        }
    }

    [GlobalCleanup]
    public void Cleanup() => _connection.Dispose();

    // ─────────────────────────────────────────────
    // BuildSql_Search — 현재 생성 코드 (리플렉션)
    // ─────────────────────────────────────────────

    [Benchmark(Baseline = true, Description = "Generated_Current (리플렉션)")]
    public (string, List<DbParameter>) BuildSql_Current() {
        var __param_ = (object?)_param;
        var __sb_     = new System.Text.StringBuilder(256);
        var __params_ = new System.Collections.Generic.List<DbParameter>();
        var __idx_    = 0;

        static object? __getprop_(object? o_, string n_) {
            if (o_ == null) return null;
            var p_ = o_.GetType().GetProperty(n_,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.IgnoreCase);
            return p_?.GetValue(o_);
        }

        __sb_.Append(@"
    SELECT * FROM orders
    ");
        {
            var __wSb_ = new System.Text.StringBuilder();
            var __outerSb_ = __sb_; __sb_ = __wSb_;
            __sb_.Append(' ');
            if (__getprop_(__param_, "Name") != null) {
                __sb_.Append(@"AND name = ");
                {
                    var __pn_ = "@p" + __idx_++;
                    __sb_.Append(__pn_);
                    __params_.Add(ParameterBinder.CreateParameter(
                        __pn_, __getprop_(__param_, "Name") ?? DBNull.Value));
                }
            }
            __sb_.Append(' ');
            if (__getprop_(__param_, "MinAge") != null) {
                __sb_.Append(@"AND age >= ");
                {
                    var __pn_ = "@p" + __idx_++;
                    __sb_.Append(__pn_);
                    __params_.Add(ParameterBinder.CreateParameter(
                        __pn_, __getprop_(__param_, "MinAge") ?? DBNull.Value));
                }
            }
            __sb_.Append(' ');
            if (__getprop_(__param_, "Status") != null) {
                __sb_.Append(@"AND status = 'open'");
            }
            __sb_ = __outerSb_;
            var __wc_ = __wSb_.ToString().Trim();
            if (!string.IsNullOrEmpty(__wc_)) {
                if (__wc_.StartsWith("AND ", StringComparison.OrdinalIgnoreCase))
                    __wc_ = __wc_.Substring(4);
                else if (__wc_.StartsWith("OR ", StringComparison.OrdinalIgnoreCase))
                    __wc_ = __wc_.Substring(3);
                __sb_.Append(" WHERE ").Append(__wc_);
            }
        }
        if (__getprop_(__param_, "SortBy") != null) {
            __sb_.Append(@"ORDER BY name");
        }
        else if (__getprop_(__param_, "SortBy") != null) {
            __sb_.Append(@"ORDER BY age DESC");
        }
        else {
            __sb_.Append(@"ORDER BY id");
        }
        return (__sb_.ToString(), __params_);
    }

    // ─────────────────────────────────────────────
    // BuildSql_Search — B안 (정적 타입 직접 접근)
    // ─────────────────────────────────────────────

    [Benchmark(Description = "Proposed_DirectAccess (리플렉션 0)")]
    public (string, List<DbParameter>) BuildSql_DirectAccess() {
        var __param_ = (SearchParam?)_param;
        var __sb_     = new System.Text.StringBuilder(256);
        var __params_ = new System.Collections.Generic.List<DbParameter>();
        var __idx_    = 0;

        __sb_.Append(@"
    SELECT * FROM orders
    ");
        {
            var __wSb_ = new System.Text.StringBuilder();
            var __outerSb_ = __sb_; __sb_ = __wSb_;
            __sb_.Append(' ');
            if (__param_?.Name != null) {
                __sb_.Append(@"AND name = ");
                {
                    var __pn_ = "@p" + __idx_++;
                    __sb_.Append(__pn_);
                    __params_.Add(ParameterBinder.CreateParameter(
                        __pn_, (object?)__param_.Name ?? DBNull.Value));
                }
            }
            __sb_.Append(' ');
            if (__param_?.MinAge != null) {
                __sb_.Append(@"AND age >= ");
                {
                    var __pn_ = "@p" + __idx_++;
                    __sb_.Append(__pn_);
                    __params_.Add(ParameterBinder.CreateParameter(
                        __pn_, (object?)__param_.MinAge ?? DBNull.Value));
                }
            }
            __sb_.Append(' ');
            if (__param_?.Status != null) {
                __sb_.Append(@"AND status = 'open'");
            }
            __sb_ = __outerSb_;
            var __wc_ = __wSb_.ToString().Trim();
            if (!string.IsNullOrEmpty(__wc_)) {
                if (__wc_.StartsWith("AND ", StringComparison.OrdinalIgnoreCase))
                    __wc_ = __wc_.Substring(4);
                else if (__wc_.StartsWith("OR ", StringComparison.OrdinalIgnoreCase))
                    __wc_ = __wc_.Substring(3);
                __sb_.Append(" WHERE ").Append(__wc_);
            }
        }
        if (__param_?.SortBy != null) {
            __sb_.Append(@"ORDER BY name");
        }
        else if (__param_?.SortBy != null) {
            __sb_.Append(@"ORDER BY age DESC");
        }
        else {
            __sb_.Append(@"ORDER BY id");
        }
        return (__sb_.ToString(), __params_);
    }

    // ─────────────────────────────────────────────
    // 조립 + 실제 SQL 실행 (분모 측정)
    // ─────────────────────────────────────────────

    [Benchmark(Description = "FullQuery_Current (조립+SQLite 실행)")]
    public int FullQuery_Current() {
        var (sql, pars) = BuildSql_Current();
        return Execute(sql, pars);
    }

    [Benchmark(Description = "FullQuery_DirectAccess (조립+SQLite 실행)")]
    public int FullQuery_DirectAccess() {
        var (sql, pars) = BuildSql_DirectAccess();
        return Execute(sql, pars);
    }

    private int Execute(string sql, List<DbParameter> parameters) {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;

        // SimpleExecutor.CreateCommand와 동일한 복사 경로 —
        // 생성 코드가 만드는 GenericDbParameter는 프로바이더 파라미터로 옮겨 담아야 한다.
        foreach (var source in parameters) {
            var p           = cmd.CreateParameter();
            p.ParameterName = source.ParameterName;
            p.Value         = source.Value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }

        var count = 0;
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) count++;
        return count;
    }

    // ─────────────────────────────────────────────
    // 대상 모델
    // ─────────────────────────────────────────────

    public class SearchParam {
        public string? Name   { get; set; }
        public int?    MinAge { get; set; }
        public string? Status { get; set; }
        public string? SortBy { get; set; }
    }
}
