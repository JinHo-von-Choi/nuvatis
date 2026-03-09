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
    /// <summary>DbDataReader에서 지정된 ordinal 위치의 값을 읽어 .NET 객체로 변환한다.</summary>
    object? GetValue(DbDataReader reader, int ordinal);
    /// <summary>DbParameter의 Value를 .NET 객체에서 DB 저장 가능한 형태로 변환하여 설정한다.</summary>
    void SetParameter(DbParameter parameter, object? value);
    /// <summary>이 핸들러가 처리하는 .NET 타입.</summary>
    Type TargetType { get; }
}
