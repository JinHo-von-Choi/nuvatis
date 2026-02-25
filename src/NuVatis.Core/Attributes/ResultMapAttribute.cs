namespace NuVatis.Attributes;

/** Mapper 메서드에 사용할 resultMap ID를 지정한다. */
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ResultMapAttribute : Attribute {
    public string ResultMapId { get; }
    public ResultMapAttribute(string resultMapId) => ResultMapId = resultMapId;
}
