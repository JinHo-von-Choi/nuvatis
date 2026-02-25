using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NuVatis.Configuration;
using NuVatis.Provider;
using NuVatis.Session;
using NuVatis.Statement;

namespace NuVatis.Extensions.DependencyInjection;

/**
 * IServiceCollection 확장 메서드. NuVatis DI 통합 진입점.
 *
 * 사용 예:
 *   services.AddNuVatis(options => {
 *       options.ConnectionString = "Host=localhost;...";
 *       options.Provider = new PostgreSqlProvider();
 *   });
 *
 * @author 최진호
 * @date   2026-02-24
 */
public static class NuVatisServiceCollectionExtensions {

    /**
     * NuVatis 서비스를 DI 컨테이너에 등록한다.
     *
     * - ISqlSessionFactory: Singleton
     * - ISqlSession: Scoped (요청당 하나)
     * - Mapper 인터페이스: Scoped (ISqlSession 생명주기에 종속)
     *
     * @param services  IServiceCollection
     * @param configure NuVatisOptions 구성 액션
     * @returns IServiceCollection (fluent)
     */
    public static IServiceCollection AddNuVatis(
        this IServiceCollection services,
        Action<NuVatisOptions> configure) {

        var options = new NuVatisOptions();
        configure(options);
        options.Validate();

        var configuration = new NuVatisConfiguration {
            DataSource = new DataSourceConfig {
                ProviderName     = options.Provider!.Name,
                ConnectionString = options.ConnectionString!
            }
        };

        var factory = new SqlSessionFactory(
            configuration,
            options.Provider!,
            options.LoggerFactory);

        var mapperFactories = new Dictionary<Type, Func<ISqlSession, object>>();

        factory.SetMapperFactory((type, session) => {
            if (mapperFactories.TryGetValue(type, out var creator)) {
                return creator(session);
            }
            throw new InvalidOperationException(
                $"Mapper '{type.FullName}'이 등록되지 않았습니다.");
        });

        options.RegistryAction?.Invoke(factory, (type, creator) => {
            mapperFactories[type] = creator;
        });

        options.StatementRegistryAction?.Invoke(configuration.Statements);

        var healthStatement = new MappedStatement {
            Id        = NuVatisHealthCheckConstants.HealthStatementId,
            Namespace = "",
            Type      = StatementType.Select,
            SqlSource = NuVatisHealthCheckConstants.HealthSql
        };
        configuration.Statements[healthStatement.FullId] = healthStatement;

        foreach (var interceptor in options.Interceptors) {
            factory.AddInterceptor(interceptor);
        }

        services.TryAddSingleton<ISqlSessionFactory>(factory);

        services.TryAddScoped<ISqlSession>(sp => {
            var f = sp.GetRequiredService<ISqlSessionFactory>();
            return f.OpenSession(options.DefaultAutoCommit);
        });

        foreach (var (interfaceType, _) in mapperFactories) {
            services.TryAddScoped(interfaceType, sp => {
                var session = sp.GetRequiredService<ISqlSession>();
                return session.GetMapper(interfaceType);
            });
        }

        return services;
    }
}

/**
 * ISqlSession에 타입 파라미터 없는 GetMapper 확장.
 */
internal static class SqlSessionExtensions {
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "DI 확장은 런타임 경로. AOT 환경에서는 SG 생성 코드가 직접 Mapper를 생성한다.")]
    internal static object GetMapper(this ISqlSession session, Type mapperType) {
        var method = typeof(ISqlSession)
            .GetMethod(nameof(ISqlSession.GetMapper))!
            .MakeGenericMethod(mapperType);
        return method.Invoke(session, null)!;
    }
}
