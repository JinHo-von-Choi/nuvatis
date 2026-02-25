using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.RegularExpressions;

namespace NuVatis.Binding;

/**
 * #{paramName} 파라미터 바인딩을 런타임에 처리한다.
 * SqlSource 내의 #{...}을 @p0, @p1 등으로 치환하고
 * 파라미터 객체에서 값을 추출하여 DbParameter 리스트를 생성한다.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public static class ParameterBinder {

    private static readonly Regex ParamPattern = new(
        @"#\{(\w+(?:\.\w+)*)\}",
        RegexOptions.Compiled);

    private static readonly ConcurrentDictionary<(Type, string), PropertyInfo?> PropertyCache = new();

    /**
     * SQL 내의 #{...} 바인딩을 처리하여 실행 가능한 SQL과 DbParameter 리스트를 반환한다.
     *
     * @param sqlSource    원본 SQL (#{paramName} 포함)
     * @param parameter    파라미터 객체 (null이면 바인딩 스킵)
     * @param paramPrefix  DB별 파라미터 접두사 (기본: @)
     * @param dbFactory    DbParameter 생성용 DbProviderFactory (null이면 범용 파라미터 사용)
     * @returns (치환된 SQL, DbParameter 리스트)
     */
    public static (string Sql, List<DbParameter> Parameters) Bind(
        string sqlSource,
        object? parameter,
        string paramPrefix = "@",
        DbProviderFactory? dbFactory = null) {

        var parameters = new List<DbParameter>();
        var index      = 0;

        var sql = ParamPattern.Replace(sqlSource, match => {
            var propertyPath = match.Groups[1].Value;
            var paramName    = $"{paramPrefix}p{index++}";
            var value        = parameter is not null
                ? ResolvePropertyValue(parameter, propertyPath)
                : DBNull.Value;

            DbParameter dbParam;
            if (dbFactory is not null) {
                dbParam       = dbFactory.CreateParameter()!;
                dbParam.ParameterName = paramName;
                dbParam.Value         = value ?? DBNull.Value;
            } else {
                dbParam = new GenericDbParameter(paramName, value ?? DBNull.Value);
            }

            parameters.Add(dbParam);
            return paramName;
        });

        return (sql, parameters);
    }

    [UnconditionalSuppressMessage("AOT", "IL2070",
        Justification = "런타임 파라미터 바인딩. AOT 환경에서는 SG가 빌드타임에 바인딩 코드를 생성한다.")]
    private static object? ResolvePropertyValue(object obj, string propertyPath) {
        var parts   = propertyPath.Split('.');
        var current = obj;

        foreach (var part in parts) {
            if (current is null) return null;

            var type = current.GetType();
            var key  = (type, part);

            var prop = PropertyCache.GetOrAdd(key, k =>
                k.Item1.GetProperty(k.Item2, BindingFlags.Public | BindingFlags.Instance));

            if (prop is null) return null;
            current = prop.GetValue(current);
        }

        return current;
    }

    /**
     * DbProviderFactory 없이 사용 가능한 범용 DbParameter.
     */
    private sealed class GenericDbParameter : DbParameter {
        public override DbType DbType                 { get; set; }
        public override ParameterDirection Direction   { get; set; } = ParameterDirection.Input;
        public override bool IsNullable                { get; set; } = true;

        [AllowNull]
        public override string ParameterName           { get; set; }

        public override int Size                       { get; set; }

        [AllowNull]
        public override string SourceColumn            { get; set; } = string.Empty;

        public override bool SourceColumnNullMapping   { get; set; }
        public override object? Value                  { get; set; }

        public GenericDbParameter(string name, object value) {
            ParameterName = name;
            Value         = value;
        }

        public override void ResetDbType() { }
    }
}
