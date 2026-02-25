using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace NuVatis.Extensions.DependencyInjection;

/**
 * IHealthChecksBuilder 확장 메서드.
 * NuVatis DB 연결 상태를 Health Check에 등록한다.
 *
 * 사용:
 *   builder.Services.AddHealthChecks()
 *       .AddNuVatis("nuvatis-db");
 *
 * @author 최진호
 * @date   2026-02-25
 */
public static class NuVatisHealthCheckExtensions {

    /**
     * NuVatis DB 연결 Health Check를 등록한다.
     *
     * @param builder IHealthChecksBuilder
     * @param name    Health Check 이름 (기본: "nuvatis")
     * @param failureStatus 실패 시 상태 (기본: Unhealthy)
     * @param tags    태그 목록
     * @param timeout 타임아웃
     */
    public static IHealthChecksBuilder AddNuVatis(
        this IHealthChecksBuilder builder,
        string name = "nuvatis",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null) {

        return builder.Add(new HealthCheckRegistration(
            name,
            sp => new NuVatisHealthCheck(
                sp.GetRequiredService<NuVatis.Session.ISqlSessionFactory>()),
            failureStatus,
            tags,
            timeout));
    }
}
