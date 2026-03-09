namespace NuVatis.Configuration;

/**
 * 데이터소스 설정 모델.
 * nuvatis-config.xml의 <dataSource> 요소에 대응한다.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public sealed class DataSourceConfig {
    /// <summary>DB Provider 이름. DbProviderRegistry에 등록된 이름과 일치해야 한다.</summary>
    public required string ProviderName     { get; init; }
    /// <summary>DB 연결 문자열.</summary>
    public required string ConnectionString { get; init; }
}
