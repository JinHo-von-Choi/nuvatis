using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace NuVatis.Mapping.TypeHandlers;

/**
 * JSON 열을 C# 객체로 직렬화/역직렬화하는 TypeHandler.
 * System.Text.Json 기반. DB에 JSON 문자열로 저장하고
 * 읽을 때 지정된 타입으로 역직렬화한다.
 *
 * @author 최진호
 * @date   2026-02-26
 */
[UnconditionalSuppressMessage("AOT", "IL2026",
    Justification = "사용자가 직접 등록하는 TypeHandler이므로 런타임 타입 정보가 보장됨")]
[UnconditionalSuppressMessage("AOT", "IL3050",
    Justification = "사용자가 직접 등록하는 TypeHandler이므로 런타임 타입 정보가 보장됨")]
public sealed class JsonTypeHandler<T> : ITypeHandler where T : class {
    private readonly JsonSerializerOptions? _options;

    public JsonTypeHandler(JsonSerializerOptions? options = null) {
        _options = options;
    }

    public Type TargetType => typeof(T);

    public object? GetValue(DbDataReader reader, int ordinal) {
        if (reader.IsDBNull(ordinal)) return null;
        var json = reader.GetString(ordinal);
        if (string.IsNullOrEmpty(json)) return null;
        return JsonSerializer.Deserialize<T>(json, _options);
    }

    public void SetParameter(DbParameter parameter, object? value) {
        parameter.Value = value is null
            ? DBNull.Value
            : JsonSerializer.Serialize((T)value, _options);
    }
}
