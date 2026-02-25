#nullable enable
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using NuVatis.Generators.Analysis;
using NuVatis.Generators.Diagnostics;
using NuVatis.Generators.Emitters;
using NuVatis.Generators.Models;
using NuVatis.Generators.Parsing;

namespace NuVatis.Generators;

/**
 * NuVatis Source Generator 진입점.
 * IIncrementalGenerator를 구현하여 빌드타임에 Mapper 프록시,
 * 매핑 코드, Registry를 자동 생성한다.
 *
 * @author   최진호
 * @date     2026-02-24
 * @modified 2026-02-25 ${} 문자열 치환 NV004 경고 방출 추가
 */
[Generator(LanguageNames.CSharp)]
public sealed class NuVatisIncrementalGenerator : IIncrementalGenerator {

    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var xmlMappers = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".xml"))
            .Select(static (file, ct) => {
                var text = file.GetText(ct)?.ToString();
                if (text is null || !text.Contains("<mapper")) return null;
                return XmlMapperParser.Parse(text, ct);
            })
            .Where(static m => m is not null)
            .Collect();

        var compilationAndMappers = context.CompilationProvider.Combine(xmlMappers);

        context.RegisterSourceOutput(compilationAndMappers, static (spc, source) => {
            var (compilation, mappers) = source;
            Execute(spc, compilation, mappers!);
        });
    }

    private static void Execute(
        SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<ParsedMapper?> parsedMappers) {

        var mappers = parsedMappers
            .Where(m => m is not null)
            .Cast<ParsedMapper>()
            .Select(IncludeResolver.ResolveIncludes)
            .ToImmutableArray();

        ReportStringSubstitutionWarnings(context, mappers);

        var interfaces = InterfaceAnalyzer.FindMapperInterfaces(compilation, context.CancellationToken);

        if (interfaces.Length == 0) return;

        foreach (var interfaceInfo in interfaces) {
            context.CancellationToken.ThrowIfCancellationRequested();

            var matchingMapper = mappers
                .FirstOrDefault(m => m.Namespace == interfaceInfo.FullyQualifiedName);

            var proxySource = ProxyEmitter.Emit(interfaceInfo, matchingMapper);
            var hintName    = $"{interfaceInfo.Name}Impl.g.cs";

            context.AddSource(hintName, SourceText.From(proxySource, Encoding.UTF8));
        }

        var registrySource = RegistryEmitter.Emit(interfaces);
        context.AddSource("NuVatisMapperRegistry.g.cs", SourceText.From(registrySource, Encoding.UTF8));
    }

    /**
     * 모든 ParsedMapper에서 ${} 문자열 치환 사용을 탐지하여 NV004 경고를 방출한다.
     */
    private static void ReportStringSubstitutionWarnings(
        SourceProductionContext context,
        ImmutableArray<ParsedMapper> mappers) {

        foreach (var mapper in mappers) {
            var usages = StringSubstitutionAnalyzer.Analyze(mapper);

            foreach (var usage in usages) {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.SqlInjectionWarning,
                    Location.None,
                    usage.ParameterName,
                    usage.Namespace,
                    usage.StatementId));
            }
        }
    }
}
