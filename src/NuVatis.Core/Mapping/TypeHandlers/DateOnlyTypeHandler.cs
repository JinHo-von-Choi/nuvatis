#if NET8_0_OR_GREATER
using System.Data.Common;

namespace NuVatis.Mapping.TypeHandlers;

/**
 * DateOnly <-> DB date 변환 TypeHandler.
 * .NET 8 이상에서만 사용 가능.
 *
 * @author 최진호
 * @date   2026-02-26
 */
public sealed class DateOnlyTypeHandler : ITypeHandler {

    public Type TargetType => typeof(DateOnly);

    public object? GetValue(DbDataReader reader, int ordinal) {
        var dateTime = reader.GetDateTime(ordinal);
        return DateOnly.FromDateTime(dateTime);
    }

    public void SetParameter(DbParameter parameter, object? value) {
        if (value is DateOnly dateOnly) {
            parameter.Value = dateOnly.ToDateTime(TimeOnly.MinValue);
        } else {
            parameter.Value = DBNull.Value;
        }
    }
}
#endif
