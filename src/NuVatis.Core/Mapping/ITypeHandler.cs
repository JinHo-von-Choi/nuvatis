using System.Data.Common;

namespace NuVatis.Mapping;

/**
 * DB 타입과 .NET 타입 간의 변환을 담당하는 인터페이스.
 * 커스텀 타입 변환이 필요한 경우 구현한다.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public interface ITypeHandler {
    object? GetValue(DbDataReader reader, int ordinal);
    void SetParameter(DbParameter parameter, object? value);
    Type TargetType { get; }
}
