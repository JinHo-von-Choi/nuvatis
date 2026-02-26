#if NET8_0_OR_GREATER
using System.Data.Common;

namespace NuVatis.Mapping.TypeHandlers;

/**
 * TimeOnly <-> DB time 변환 TypeHandler.
 * .NET 8 이상에서만 사용 가능.
 *
 * @author 최진호
 * @date   2026-02-26
 */
public sealed class TimeOnlyTypeHandler : ITypeHandler {

    public Type TargetType => typeof(TimeOnly);

    public object? GetValue(DbDataReader reader, int ordinal) {
        var value = reader.GetValue(ordinal);
        return value switch {
            TimeSpan ts => TimeOnly.FromTimeSpan(ts),
            DateTime dt => TimeOnly.FromDateTime(dt),
            _           => throw new InvalidOperationException(
                               $"TimeOnly 변환 불가: {value.GetType().Name}")
        };
    }

    public void SetParameter(DbParameter parameter, object? value) {
        if (value is TimeOnly timeOnly) {
            parameter.Value = timeOnly.ToTimeSpan();
        } else {
            parameter.Value = DBNull.Value;
        }
    }
}
#endif
