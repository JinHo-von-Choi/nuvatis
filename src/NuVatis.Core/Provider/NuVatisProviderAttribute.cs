namespace NuVatis.Provider;

/**
 * DB Provider 구현 클래스에 부착하여 SG가 자동 발견할 수 있게 한다.
 *
 * @author 최진호
 * @date   2026-02-24
 */
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class NuVatisProviderAttribute : Attribute {
    public string ProviderName { get; }

    public NuVatisProviderAttribute(string providerName) {
        ProviderName = providerName;
    }
}
