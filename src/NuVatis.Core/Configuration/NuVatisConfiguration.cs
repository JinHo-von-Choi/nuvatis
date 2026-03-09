using NuVatis.Cache;
using NuVatis.Mapping;
using NuVatis.Statement;

namespace NuVatis.Configuration;

/**
 * NuVatis 프레임워크의 전체 설정을 보관하는 루트 모델.
 * SqlSessionFactory 생성 시 이 객체가 완성되어 전달된다.
 *
 * @author 최진호
 * @date   2026-02-24
 * @modified 2026-02-25 CacheProvider, CacheConfigs 추가 (Phase 6.4 A-4)
 */
/// <summary>NuVatis 프레임워크의 전체 설정을 보관하는 루트 모델. SqlSessionFactory 생성 시 이 객체가 완성되어 전달된다.</summary>
public sealed class NuVatisConfiguration {
    /// <summary>Gets the data source configuration used to create database connections.</summary>
    public required DataSourceConfig DataSource           { get; init; }
    /// <summary>Gets the type alias registry that maps short names to fully-qualified CLR types.</summary>
    public TypeAliasRegistry TypeAliases                  { get; init; } = new();
    /// <summary>Gets the dictionary of all parsed mapped statements, keyed by fully-qualified statement id.</summary>
    public Dictionary<string, MappedStatement> Statements { get; init; } = new();
    /// <summary>Gets the dictionary of all parsed result map definitions, keyed by fully-qualified result map id.</summary>
    public Dictionary<string, ResultMapDefinition> ResultMaps { get; init; } = new();
    /// <summary>Gets the list of embedded resource paths from which mapper XML files are loaded.</summary>
    public List<string> MapperResources                   { get; init; } = new();
    /// <summary>Gets the default command timeout in seconds applied to all statements, or null to use the driver default.</summary>
    public int? DefaultCommandTimeout                     { get; init; }

    /**
     * 2차 캐시 제공자. null이면 캐시를 사용하지 않는다.
     * MemoryCacheProvider가 기본 구현이며, Redis 등으로 교체 가능하다.
     */
    public ICacheProvider? CacheProvider                  { get; set; }

    /**
     * Namespace별 캐시 설정. XML의 &lt;cache&gt; 엘리먼트에서 파싱된다.
     * 키: namespace, 값: CacheConfig.
     */
    public Dictionary<string, CacheConfig> CacheConfigs   { get; init; } = new();
}
