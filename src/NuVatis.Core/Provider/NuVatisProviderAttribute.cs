namespace NuVatis.Provider;

/**
 * DB Provider 구현 클래스에 부착하여 SG가 자동 발견할 수 있게 한다.
 *
 * @author 최진호
 * @date   2026-02-24
 */
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class NuVatisProviderAttribute : Attribute {
    /// <summary>SG가 Provider를 식별하는 데 사용하는 이름 (예: "PostgreSql", "SqlServer").</summary>
    public string ProviderName { get; }

    /// <summary>
    /// <see cref="NuVatisProviderAttribute"/>를 초기화한다.
    /// </summary>
    /// <param name="providerName">Provider 식별 이름.</param>
    public NuVatisProviderAttribute(string providerName) {
        ProviderName = providerName;
    }
}
