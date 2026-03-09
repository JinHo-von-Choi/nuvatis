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

    /// <summary>JSON 직렬화 옵션을 지정하여 JsonTypeHandler를 초기화한다.</summary>
    /// <param name="options">JSON 직렬화 옵션. null이면 기본값을 사용한다.</param>
    public JsonTypeHandler(JsonSerializerOptions? options = null) {
        _options = options;
    }

    /// <inheritdoc />
    public Type TargetType => typeof(T);

    /// <inheritdoc />
    public object? GetValue(DbDataReader reader, int ordinal) {
        if (reader.IsDBNull(ordinal)) return null;
        var json = reader.GetString(ordinal);
        if (string.IsNullOrEmpty(json)) return null;
        return JsonSerializer.Deserialize<T>(json, _options);
    }

    /// <inheritdoc />
    public void SetParameter(DbParameter parameter, object? value) {
        parameter.Value = value is null
            ? DBNull.Value
            : JsonSerializer.Serialize((T)value, _options);
    }
}
