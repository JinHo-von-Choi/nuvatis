using System.CommandLine;
using NuVatis.QueryBuilder.Tools.Generation;
using NuVatis.QueryBuilder.Tools.Scanning;

var providerOpt = new Option<string>("--provider",   "DB 프로바이더: postgresql | mysql") { IsRequired = true };
var connOpt     = new Option<string>("--connection", "연결 문자열") { IsRequired = true };
var outputOpt   = new Option<string>("--output",     "출력 디렉토리") { IsRequired = true };
var nsOpt       = new Option<string>("--namespace",  "생성 코드 네임스페이스") { IsRequired = true };
var schemaOpt   = new Option<string>("--schema",     "스키마명 (기본: public)");
schemaOpt.SetDefaultValue("public");

var root = new RootCommand("NuVatis QueryBuilder 코드 생성기 — DB 스키마를 타입 안전 C# 클래스로 변환합니다.");
root.AddOption(providerOpt);
root.AddOption(connOpt);
root.AddOption(outputOpt);
root.AddOption(nsOpt);
root.AddOption(schemaOpt);

root.SetHandler(async (provider, conn, output, ns, schema) => {
    ISchemaScanner scanner = provider.ToLowerInvariant() switch {
        "postgresql" => new PostgreSqlSchemaScanner(),
        "mysql"      => new MySqlSchemaScanner(),
        _            => throw new ArgumentException($"알 수 없는 프로바이더: {provider}. postgresql 또는 mysql을 사용하세요.")
    };

    Console.WriteLine($"[nuvatis-gen] 스키마 스캔 중: {schema} ({provider})");
    var tables = await scanner.ScanAsync(conn, schema);
    Console.WriteLine($"[nuvatis-gen] 테이블 {tables.Count}개 발견");

    Directory.CreateDirectory(output);

    foreach (var table in tables) {
        var code     = TableClassGenerator.Generate(table, ns, provider);
        var fileName = Path.Combine(output, $"{TableClassGenerator.ToPascalCase(table.Name)}Table.g.cs");
        await File.WriteAllTextAsync(fileName, code);
        Console.WriteLine($"[nuvatis-gen]   생성: {fileName}");
    }

    var tablesEntry = TableClassGenerator.GenerateTablesEntry(tables, ns);
    await File.WriteAllTextAsync(Path.Combine(output, "Tables.g.cs"), tablesEntry);
    Console.WriteLine("[nuvatis-gen] 완료.");

}, providerOpt, connOpt, outputOpt, nsOpt, schemaOpt);

return await root.InvokeAsync(args);
