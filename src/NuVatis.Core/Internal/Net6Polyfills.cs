#if !NET7_0_OR_GREATER
// C# 11 'required' 멤버 기능이 의존하는 BCL 어트리뷰트를 net6.0에서 폴리필.
// .NET 7+에서는 런타임 자체에 존재하므로 조건부 컴파일.
namespace System.Runtime.CompilerServices;

[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct |
    AttributeTargets.Field | AttributeTargets.Property,
    AllowMultiple = false,
    Inherited    = false)]
internal sealed class RequiredMemberAttribute : Attribute { }

[AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
internal sealed class CompilerFeatureRequiredAttribute : Attribute {
    public CompilerFeatureRequiredAttribute(string featureName) {
        FeatureName = featureName;
    }

    public string FeatureName { get; }
    public bool   IsOptional  { get; init; }
}
#endif
