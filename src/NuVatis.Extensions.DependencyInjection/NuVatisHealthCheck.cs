using Microsoft.Extensions.Diagnostics.HealthChecks;
using NuVatis.Session;

namespace NuVatis.Extensions.DependencyInjection;

/**
 * NuVatis DB 연결 상태를 확인하는 Health Check.
 * ISqlSessionFactory를 통해 커넥션을 열고 간단한 쿼리(SELECT 1)를 실행하여 건강 상태를 판단.
 *
 * 사용:
 *   builder.Services.AddHealthChecks().AddNuVatis("nuvatis-db");
 *
 * @author 최진호
 * @date   2026-02-25
 */
public sealed class NuVatisHealthCheck : IHealthCheck {
    private readonly ISqlSessionFactory _factory;

    public NuVatisHealthCheck(ISqlSessionFactory factory) {
        _factory = factory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) {
        try {
            await using var session = _factory.OpenReadOnlySession();
            var result = await session.SelectOneAsync<int>(
                NuVatisHealthCheckConstants.HealthFullId,
                ct: cancellationToken);

            return result == 1
                ? HealthCheckResult.Healthy("DB connection is healthy.")
                : HealthCheckResult.Degraded("Unexpected ping result.");
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            return new HealthCheckResult(
                context?.Registration?.FailureStatus ?? HealthStatus.Unhealthy,
                description: $"DB health check failed: {ex.Message}",
                exception: ex);
        }
    }
}

/**
 * Health Check에서 사용하는 내부 상수.
 */
internal static class NuVatisHealthCheckConstants {
    internal const string HealthStatementId = "__nuvatis_health";
    internal const string HealthFullId      = ".__nuvatis_health";
    internal const string HealthSql         = "SELECT 1";
}
