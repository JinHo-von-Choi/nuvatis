using System.Data.Common;

namespace NuVatis.Mapping.TypeHandlers;

/**
 * Enum 값을 DB 문자열로 저장하고 읽는 TypeHandler.
 * Enum.ToString()으로 저장, Enum.Parse()로 복원한다.
 *
 * @author 최진호
 * @date   2026-02-26
 */
public sealed class EnumStringTypeHandler<TEnum> : ITypeHandler where TEnum : struct, Enum {

    public Type TargetType => typeof(TEnum);

    public object? GetValue(DbDataReader reader, int ordinal) {
        var str = reader.GetString(ordinal);
        if (Enum.TryParse<TEnum>(str, ignoreCase: true, out var result)) {
            return result;
        }
        throw new InvalidOperationException(
            $"'{str}'을(를) {typeof(TEnum).Name}으로 변환할 수 없습니다.");
    }

    public void SetParameter(DbParameter parameter, object? value) {
        parameter.Value = value is null ? DBNull.Value : value.ToString()!;
    }
}
