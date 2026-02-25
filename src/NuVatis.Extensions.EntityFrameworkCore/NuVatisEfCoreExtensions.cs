using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NuVatis.Extensions.DependencyInjection;
using NuVatis.Session;

namespace NuVatis.Extensions.EntityFrameworkCore;

/**
 * EF Core와 NuVatis 통합을 위한 확장 메서드.
 *
 * 두 가지 사용 패턴을 지원한다:
 *
 * 1. DI 자동 통합:
 *    services.AddNuVatis(options => { ... })
 *            .AddNuVatisEntityFrameworkCore<AppDbContext>();
 *
 *    ISqlSession이 Scoped로 등록될 때 현재 DbContext의 커넥션/트랜잭션을 자동 공유.
 *
 * 2. 수동 사용:
 *    using var session = dbContext.OpenNuVatisSession(factory);
 *
 * @author 최진호
 * @date   2026-02-25
 */
public static class NuVatisEfCoreExtensions {

    /**
     * EF Core DbContext와 NuVatis의 커넥션/트랜잭션 자동 공유를 설정한다.
     * ISqlSession 등록을 DbContext 기반 FromExistingConnection으로 교체한다.
     *
     * @param services AddNuVatis() 이후의 IServiceCollection
     */
    public static IServiceCollection AddNuVatisEntityFrameworkCore<TContext>(
        this IServiceCollection services) where TContext : DbContext {

        services.RemoveAll<ISqlSession>();

        services.AddScoped<ISqlSession>(sp => {
            var factory   = sp.GetRequiredService<ISqlSessionFactory>();
            var dbContext  = sp.GetRequiredService<TContext>();
            var connection = dbContext.Database.GetDbConnection();

            if (connection.State != System.Data.ConnectionState.Open) {
                connection.Open();
            }

            var transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
            return factory.FromExistingConnection(connection, transaction);
        });

        return services;
    }

    /**
     * DbContext에서 NuVatis 세션을 수동으로 생성한다.
     * 반환된 세션은 DbContext의 커넥션/트랜잭션을 공유한다.
     * 세션 Dispose 시 커넥션을 닫지 않는다.
     *
     * @param dbContext EF Core DbContext
     * @param factory  ISqlSessionFactory 인스턴스
     */
    public static ISqlSession OpenNuVatisSession(
        this DbContext dbContext,
        ISqlSessionFactory factory) {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(factory);

        var connection = dbContext.Database.GetDbConnection();

        if (connection.State != System.Data.ConnectionState.Open) {
            connection.Open();
        }

        var transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
        return factory.FromExistingConnection(connection, transaction);
    }

    /**
     * DbContext에서 NuVatis 세션을 비동기로 생성한다.
     *
     * @param dbContext EF Core DbContext
     * @param factory  ISqlSessionFactory 인스턴스
     * @param ct       CancellationToken
     */
    public static async Task<ISqlSession> OpenNuVatisSessionAsync(
        this DbContext dbContext,
        ISqlSessionFactory factory,
        CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(factory);

        var connection = dbContext.Database.GetDbConnection();

        if (connection.State != System.Data.ConnectionState.Open) {
            await connection.OpenAsync(ct).ConfigureAwait(false);
        }

        var transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
        return factory.FromExistingConnection(connection, transaction);
    }
}
