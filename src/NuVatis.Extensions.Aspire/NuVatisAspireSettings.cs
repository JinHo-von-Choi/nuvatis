namespace NuVatis.Extensions.Aspire;

/**
 * Aspire 컴포넌트 설정. IConfiguration 바인딩용.
 * appsettings.json 예:
 *   "NuVatis": {
 *     "ConnectionString": "Host=localhost;Database=mydb;...",
 *     "ProviderName": "PostgreSql",
 *     "DisableHealthChecks": false,
 *     "DisableTracing": false
 *   }
 *
 * @author 최진호
 * @date   2026-02-26
 */
public sealed class NuVatisAspireSettings {
    public string? ConnectionString    { get; set; }
    public string  ProviderName        { get; set; } = "PostgreSql";
    public bool    DisableHealthChecks { get; set; }
    public bool    DisableTracing      { get; set; }
}
