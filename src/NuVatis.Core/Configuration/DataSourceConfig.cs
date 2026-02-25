namespace NuVatis.Configuration;

/**
 * 데이터소스 설정 모델.
 * nuvatis-config.xml의 <dataSource> 요소에 대응한다.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public sealed class DataSourceConfig {
    public required string ProviderName     { get; init; }
    public required string ConnectionString { get; init; }
}
