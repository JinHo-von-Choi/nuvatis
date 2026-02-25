#nullable enable
using System.Collections.Immutable;

namespace NuVatis.Generators.Models;

public sealed record ParsedResultMap(
    string Id,
    string Type,
    string? Extends,
    ImmutableArray<ParsedResultMapping> Mappings,
    ImmutableArray<ParsedAssociation> Associations,
    ImmutableArray<ParsedCollection> Collections
);
