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
    private ILoggerFactory? _loggerFactory;
    private readonly DbProviderRegistry _providerRegistry = new();

    /// <summary>사용할 DB 프로바이더를 설정한다.</summary>
    public SqlSessionFactoryBuilder UseProvider(IDbProvider provider) {
        _provider = provider;
        _providerRegistry.Register(provider);
        return this;
    }

    /// <summary>데이터베이스 연결 문자열을 설정한다.</summary>
    public SqlSessionFactoryBuilder ConnectionString(string connectionString) {
        _connectionString = connectionString;
        return this;
    }

    /// <summary>
    /// 지원되지 않는다. XML 매퍼는 빌드타임에 Source Generator가 처리하며 런타임 로드 경로가 없다.
    /// SG 생성 <c>NuVatisMapperRegistry.RegisterAll</c>을
    /// <see cref="SqlSessionFactory.SetMapperFactory"/> 또는 DI의 <c>options.RegisterMappers</c>로 등록하라.
    /// </summary>
    /// <exception cref="NotSupportedException">항상 발생한다.</exception>
    [Obsolete("런타임 XML 설정 로드는 지원되지 않습니다. XML 매퍼는 빌드타임 Source Generator가 처리합니다. " +
              "생성된 NuVatisMapperRegistry.RegisterAll을 SetMapperFactory 또는 DI RegisterMappers로 등록하세요.")]
    public SqlSessionFactoryBuilder AddXmlConfiguration(string path) {
        throw new NotSupportedException(
            "런타임 XML 설정 로드는 지원되지 않습니다. XML 매퍼는 빌드타임에 Source Generator가 처리합니다. " +
            "생성된 NuVatisMapperRegistry.RegisterAll을 SqlSessionFactory.SetMapperFactory 또는 " +
            "DI의 options.RegisterMappers로 등록하세요.");
    }

    /// <summary>로깅에 사용할 ILoggerFactory를 설정한다.</summary>
    public SqlSessionFactoryBuilder UseLoggerFactory(ILoggerFactory loggerFactory) {
        _loggerFactory = loggerFactory;
        return this;
    }

    /// <summary>설정을 완료하고 SqlSessionFactory 인스턴스를 생성한다.</summary>
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

    /// <summary>
    /// 지원되지 않는다. <see cref="AddXmlConfiguration"/>과 동일한 사유로 항상 예외를 발생시킨다.
    /// 인자 없는 <see cref="Build()"/>를 사용하라.
    /// </summary>
    /// <exception cref="NotSupportedException">항상 발생한다.</exception>
    [Obsolete("런타임 XML 설정 로드는 지원되지 않습니다. 인자 없는 Build()를 사용하세요.")]
    public SqlSessionFactory Build(string xmlConfigPath) {
        throw new NotSupportedException(
            "런타임 XML 설정 로드는 지원되지 않습니다. 인자 없는 Build()를 사용하세요. " +
            "XML 매퍼는 빌드타임에 Source Generator가 처리합니다.");
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
