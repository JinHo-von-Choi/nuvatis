#nullable enable
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using NuVatis.Generators.Models;

namespace NuVatis.Generators.Diagnostics;

/**
 * ParsedMapper 내 정의된 ResultMap 중 어떤 Statement에서도 참조하지 않는
 * 미사용 ResultMap을 탐지한다 (NV005 경고 → 향후 Info로 완화 가능).
 *
 * @author 최진호
 * @date   2026-02-26
 */
public static class UnusedResultMapAnalyzer {

    /**
     * mapper 내 모든 Statement의 ResultMapId를 수집하고,
     * 정의된 ResultMap.Id와 대조하여 참조되지 않는 것을 반환한다.
     * O(S + R) — S: statement 수, R: resultMap 수
     */
    public static ImmutableArray<UnusedResultMap> Analyze(ParsedMapper mapper) {
        var referencedIds = new HashSet<string>(
            mapper.Statements
                .Where(s => s.ResultMapId is not null)
                .Select(s => s.ResultMapId!));

        var results = ImmutableArray.CreateBuilder<UnusedResultMap>();

        foreach (var resultMap in mapper.ResultMaps) {
            if (!referencedIds.Contains(resultMap.Id)) {
                results.Add(new UnusedResultMap(mapper.Namespace, resultMap.Id));
            }
        }

        return results.ToImmutable();
    }
}

public readonly record struct UnusedResultMap(
    string Namespace,
    string ResultMapId);
