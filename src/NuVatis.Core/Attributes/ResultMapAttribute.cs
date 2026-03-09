namespace NuVatis.Attributes;

/** Mapper 메서드에 사용할 resultMap ID를 지정한다. */
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ResultMapAttribute : Attribute {
    /// <summary>XML에 정의된 resultMap의 ID.</summary>
    public string ResultMapId { get; }

    /// <summary>
    /// <see cref="ResultMapAttribute"/>를 초기화한다.
    /// </summary>
    /// <param name="resultMapId">참조할 resultMap ID.</param>
    public ResultMapAttribute(string resultMapId) => ResultMapId = resultMapId;
}
