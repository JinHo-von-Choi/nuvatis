#nullable enable
using System;
using System.Collections.Immutable;
using System.Xml.Linq;

namespace NuVatis.Generators.Parsing;

public record ParsedConfig(
    string? Provider,
    string? ConnectionString,
    ImmutableArray<(string Alias, string Type)> TypeAliases,
    ImmutableArray<string> MapperResources
);

/**
 * nuvatis-config.xml 파서. SG 빌드타임에 사용.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public static class XmlConfigParser {
    public static ParsedConfig Parse(string xmlContent) {
        var doc  = XDocument.Parse(xmlContent);
        var root = doc.Element("configuration")
            ?? throw new InvalidOperationException("Root element <configuration> not found.");

        var dataSource       = root.Element("dataSource");
        var provider         = dataSource?.Attribute("provider")?.Value;
        var connectionString = dataSource?.Attribute("connectionString")?.Value;

        var aliasesBuilder = ImmutableArray.CreateBuilder<(string Alias, string Type)>();
        var aliasesElement = root.Element("typeAliases");
        if (aliasesElement != null) {
            foreach (var alias in aliasesElement.Elements("typeAlias")) {
                var name = alias.Attribute("alias")?.Value
                    ?? throw new InvalidOperationException("Attribute 'alias' missing in <typeAlias>.");
                var type = alias.Attribute("type")?.Value
                    ?? throw new InvalidOperationException("Attribute 'type' missing in <typeAlias>.");
                aliasesBuilder.Add((name, type));
            }
        }

        var mappersBuilder = ImmutableArray.CreateBuilder<string>();
        var mappersElement = root.Element("mappers");
        if (mappersElement != null) {
            foreach (var mapper in mappersElement.Elements("mapper")) {
                var resource = mapper.Attribute("resource")?.Value
                    ?? throw new InvalidOperationException("Attribute 'resource' missing in <mapper>.");
                mappersBuilder.Add(resource);
            }
        }

        return new ParsedConfig(
            provider,
            connectionString,
            aliasesBuilder.ToImmutable(),
            mappersBuilder.ToImmutable()
        );
    }
}
