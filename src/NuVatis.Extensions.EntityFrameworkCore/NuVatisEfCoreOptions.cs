namespace NuVatis.Extensions.EntityFrameworkCore;

/**
 * EF Core 통합 옵션.
 * UseEntityFrameworkCore<TContext>() 호출 시 내부에서 구성된다.
 *
 * @author 최진호
 * @date   2026-02-25
 */
public sealed class NuVatisEfCoreOptions {

    /**
     * DbContext 타입. DI에서 커넥션 추출 시 사용.
     */
    public Type? DbContextType { get; internal set; }
}
