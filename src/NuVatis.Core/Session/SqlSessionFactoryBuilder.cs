using Microsoft.Extensions.Logging;
using NuVatis.Configuration;
using NuVatis.Provider;

namespace NuVatis.Session;

/**
 * SqlSessionFactory를 생성하는 빌더.
 * Fluent API로 설정을 조합한다.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public sealed class SqlSessionFactoryBuilder {
    private IDbProvider? _provider;
    private string? _connectionString;
    private string? _xmlConfigPath;
    private ILoggerFactory? _loggerFactory;
    private readonly DbProviderRegistry _providerRegistry = new();

    public SqlSessionFactoryBuilder UseProvider(IDbProvider provider) {
        _provider = provider;
        _providerRegistry.Register(provider);
        return this;
    }

    public SqlSessionFactoryBuilder ConnectionString(string connectionString) {
        _connectionString = connectionString;
        return this;
    }

    public SqlSessionFactoryBuilder AddXmlConfiguration(string path) {
        _xmlConfigPath = path;
        return this;
    }

    public SqlSessionFactoryBuilder UseLoggerFactory(ILoggerFactory loggerFactory) {
        _loggerFactory = loggerFactory;
        return this;
    }

    public SqlSessionFactory Build() {
        if (_provider is null) {
            throw new InvalidOperationException(
                "DB Provider가 설정되지 않았습니다. UseProvider()를 호출하세요.");
        }

        if (string.IsNullOrWhiteSpace(_connectionString)) {
            throw new InvalidOperationException(
                "Connection string이 설정되지 않았습니다. ConnectionString()을 호출하세요.");
        }

        var configuration = BuildConfiguration();
        return new SqlSessionFactory(configuration, _provider, _loggerFactory);
    }

    /**
     * XML 설정 파일로부터 SqlSessionFactory를 생성한다.
     * 런타임 설정 파일 로드용 (SG 빌드타임 파싱과 별개).
     */
    public SqlSessionFactory Build(string xmlConfigPath) {
        _xmlConfigPath = xmlConfigPath;
        return Build();
    }

    private NuVatisConfiguration BuildConfiguration() {
        var config = new NuVatisConfiguration {
            DataSource = new DataSourceConfig {
                ProviderName     = _provider!.Name,
                ConnectionString = _connectionString!
            }
        };

        return config;
    }
}
