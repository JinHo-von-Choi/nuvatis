using Microsoft.Extensions.Logging;
using NuVatis.Interceptor;
using NuVatis.Provider;
using NuVatis.Session;
using NuVatis.Statement;

namespace NuVatis.Extensions.DependencyInjection;

/**
 * AddNuVatis() 설정 옵션.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public sealed class NuVatisOptions {

    public string? ConnectionString   { get; set; }
    public IDbProvider? Provider      { get; set; }
    public ILoggerFactory? LoggerFactory { get; set; }
    public bool DefaultAutoCommit     { get; set; }

    /**
     * 등록된 인터셉터 목록.
     */
    internal List<ISqlInterceptor> Interceptors { get; } = new();

    /**
     * SG 생성 NuVatisMapperRegistry.RegisterAll 연결용.
     * 사용 예: options.RegisterMappers(NuVatisMapperRegistry.RegisterAll);
     */
    public Action<ISqlSessionFactory, Action<Type, Func<ISqlSession, object>>>? RegistryAction { get; private set; }

    /**
     * SG 생성 NuVatisMapperRegistry.RegisterAttributeStatements 연결용.
     */
    public Action<Dictionary<string, MappedStatement>>? StatementRegistryAction { get; private set; }

    public NuVatisOptions RegisterMappers(
        Action<ISqlSessionFactory, Action<Type, Func<ISqlSession, object>>> registryAction) {
        RegistryAction = registryAction;
        return this;
    }

    public NuVatisOptions RegisterAttributeStatements(
        Action<Dictionary<string, MappedStatement>> statementAction) {
        StatementRegistryAction = statementAction;
        return this;
    }

    /**
     * SQL 인터셉터를 등록한다.
     */
    public NuVatisOptions AddInterceptor(ISqlInterceptor interceptor) {
        ArgumentNullException.ThrowIfNull(interceptor);
        Interceptors.Add(interceptor);
        return this;
    }

    internal void Validate() {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            throw new InvalidOperationException("ConnectionString은 필수입니다.");
        if (Provider is null)
            throw new InvalidOperationException("Provider는 필수입니다.");
    }
}
