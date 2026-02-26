using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NuVatis.Extensions.DependencyInjection;
using NuVatis.Extensions.OpenTelemetry;
using NuVatis.Provider;

namespace NuVatis.Extensions.Aspire;

/**
 * .NET Aspire 통합 확장 메서드.
 * IHostApplicationBuilder에 NuVatis 서비스를 Aspire 패턴으로 등록한다.
 *
 * - 자동 ConnectionString 바인딩 (Aspire connectionName 또는 설정 키)
 * - 자동 Health Check 등록
 * - 자동 OpenTelemetry Tracing 등록
 *
 * 사용 예:
 *   builder.AddNuVatis("nuvatis-db", new PostgreSqlProvider());
 *
 * @author 최진호
 * @date   2026-02-26
 */
public static class NuVatisAspireExtensions {

    private const string DefaultConfigSection = "NuVatis";

    /**
     * NuVatis를 Aspire 컴포넌트 패턴으로 등록한다.
     *
     * 1. ConnectionStrings:{connectionName} 또는 NuVatis:ConnectionString에서 연결 문자열 로드
     * 2. DisableTracing=false이면 OpenTelemetryInterceptor 자동 등록
     * 3. DisableHealthChecks=false이면 Health Check 자동 등록
     *
     * @param builder        IHostApplicationBuilder
     * @param connectionName Aspire 리소스 연결 이름 (ConnectionStrings 섹션 키)
     * @param provider       IDbProvider 인스턴스 (PostgreSqlProvider, MySqlProvider 등)
     * @param configureSettings 추가 설정 콜백
     * @param configureOptions NuVatisOptions 추가 구성
     */
    [UnconditionalSuppressMessage("AOT", "IL2026",
        Justification = "Configuration binding은 Aspire 호스팅 환경에서 보장됨")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Configuration binding은 Aspire 호스팅 환경에서 보장됨")]
    public static IHostApplicationBuilder AddNuVatis(
        this IHostApplicationBuilder builder,
        string connectionName,
        IDbProvider provider,
        Action<NuVatisAspireSettings>? configureSettings = null,
        Action<NuVatisOptions>? configureOptions = null) {

        var settings = new NuVatisAspireSettings { ProviderName = provider.Name };
        builder.Configuration.GetSection(DefaultConfigSection).Bind(settings);
        configureSettings?.Invoke(settings);

        var connStr = settings.ConnectionString
            ?? builder.Configuration.GetConnectionString(connectionName);

        if (string.IsNullOrWhiteSpace(connStr)) {
            throw new InvalidOperationException(
                $"ConnectionString을 찾을 수 없습니다. " +
                $"ConnectionStrings:{connectionName} 또는 NuVatis:ConnectionString을 설정하세요.");
        }

        builder.Services.AddNuVatis(options => {
            options.ConnectionString = connStr;
            options.Provider         = provider;

            if (!settings.DisableTracing) {
                options.AddInterceptor(new OpenTelemetryInterceptor());
            }

            configureOptions?.Invoke(options);
        });

        if (!settings.DisableHealthChecks) {
            builder.Services.AddHealthChecks()
                .AddNuVatis($"nuvatis-{connectionName}");
        }

        return builder;
    }
}
