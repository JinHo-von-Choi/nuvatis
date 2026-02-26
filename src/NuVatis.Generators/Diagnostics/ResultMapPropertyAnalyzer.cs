#nullable enable
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using NuVatis.Generators.Models;

namespace NuVatis.Generators.Diagnostics;

/**
 * ResultMap의 Property 매핑이 대상 타입에 실제로 존재하는지 검증한다.
 * 존재하지 않는 프로퍼티 매핑은 런타임에 무시되어 데이터 누락을 야기하므로
 * 컴파일 타임에 경고(NV008)를 발생시킨다.
 *
 * @author 최진호
 * @date   2026-02-26
 */
public static class ResultMapPropertyAnalyzer {

    /**
     * mapper 내 모든 ResultMap에 대해 Type을 Compilation에서 찾고,
     * 각 Mapping.Property가 해당 타입의 public instance property인지 검증한다.
     *
     * @param mapper 분석 대상 ParsedMapper
     * @param compilation 타입 검색에 사용할 Compilation
     * @return 불일치 프로퍼티 매핑 목록
     */
    public static ImmutableArray<ResultMapPropertyMismatch> Analyze(
        ParsedMapper mapper, Compilation compilation) {

        var results = ImmutableArray.CreateBuilder<ResultMapPropertyMismatch>();

        foreach (var resultMap in mapper.ResultMaps) {
            var typeSymbol = compilation.GetTypeByMetadataName(resultMap.Type);
            if (typeSymbol is null) continue;

            var propertyNames = GetPublicPropertyNames(typeSymbol);

            foreach (var mapping in resultMap.Mappings) {
                if (!propertyNames.Contains(mapping.Property)) {
                    results.Add(new ResultMapPropertyMismatch(
                        mapper.Namespace,
                        resultMap.Id,
                        mapping.Property,
                        resultMap.Type));
                }
            }
        }

        return results.ToImmutable();
    }

    private static HashSet<string> GetPublicPropertyNames(INamedTypeSymbol typeSymbol) {
        var names = new HashSet<string>();
        var current = typeSymbol;

        while (current is not null) {
            foreach (var member in current.GetMembers()) {
                if (member is IPropertySymbol { DeclaredAccessibility: Accessibility.Public, IsStatic: false } prop) {
                    names.Add(prop.Name);
                }
            }
            current = current.BaseType;
        }

        return names;
    }
}

public readonly record struct ResultMapPropertyMismatch(
    string Namespace,
    string ResultMapId,
    string PropertyName,
    string TypeName);
