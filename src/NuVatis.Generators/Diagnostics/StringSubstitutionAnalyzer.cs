#nullable enable
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using NuVatis.Generators.Models;

namespace NuVatis.Generators.Diagnostics;

/**
 * ParsedMapper 내 ${}(문자열 치환) 사용을 탐지하여 SQL Injection 경고를 수집한다.
 * 파싱된 SQL 노드 트리를 재귀 순회하며 ParameterNode.IsStringSubstitution == true인
 * 노드를 발견하면 NV004 경고 대상으로 보고한다.
 *
 * [SqlConstant] 어트리뷰트가 부착된 필드/프로퍼티는 안전한 것으로 간주하여
 * NV004 경고에서 제외한다.
 *
 * @author   최진호
 * @date     2026-02-25
 * @modified 2026-02-26 [SqlConstant] 기반 NV004 억제 추가
 */
public static class StringSubstitutionAnalyzer {

    private const string SqlConstantAttributeName = "NuVatis.Attributes.SqlConstantAttribute";

    /**
     * 단일 ParsedMapper 내 모든 statement를 검사하여
     * ${} 사용 위치를 (statementId, paramName) 쌍으로 반환한다.
     */
    public static ImmutableArray<StringSubstitutionUsage> Analyze(ParsedMapper mapper) {
        return Analyze(mapper, null);
    }

    /**
     * [SqlConstant] 화이트리스트를 적용하여 ${} 사용을 분석한다.
     * sqlConstantNames에 포함된 파라미터명은 NV004 경고에서 제외된다.
     */
    public static ImmutableArray<StringSubstitutionUsage> Analyze(
        ParsedMapper mapper, ISet<string>? sqlConstantNames) {

        var results = ImmutableArray.CreateBuilder<StringSubstitutionUsage>();

        foreach (var statement in mapper.Statements) {
            CollectFromNode(statement.RootNode, mapper.Namespace, statement.Id,
                            sqlConstantNames, results);
        }

        return results.ToImmutable();
    }

    /**
     * Compilation에서 [SqlConstant] 어트리뷰트가 부착된 모든 필드/프로퍼티 이름을 수집한다.
     * 대소문자 구분 없이 매칭하기 위해 원본 이름과 camelCase 변형을 모두 포함한다.
     */
    public static ISet<string> CollectSqlConstantNames(Compilation compilation) {
        var names          = new HashSet<string>();
        var attributeType  = compilation.GetTypeByMetadataName(SqlConstantAttributeName);
        if (attributeType is null) return names;

        CollectFromNamespace(compilation.GlobalNamespace, attributeType, names);
        return names;
    }

    private static void CollectFromNamespace(
        INamespaceSymbol ns, INamedTypeSymbol attributeType, HashSet<string> names) {

        foreach (var member in ns.GetMembers()) {
            if (member is INamespaceSymbol childNs) {
                CollectFromNamespace(childNs, attributeType, names);
            } else if (member is INamedTypeSymbol type) {
                CollectFromType(type, attributeType, names);
            }
        }
    }

    private static void CollectFromType(
        INamedTypeSymbol type, INamedTypeSymbol attributeType, HashSet<string> names) {

        foreach (var member in type.GetMembers()) {
            var hasAttribute = member.GetAttributes()
                .Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeType));

            if (!hasAttribute) continue;

            names.Add(member.Name);

            if (member.Name.Length > 0) {
                var camel = char.ToLowerInvariant(member.Name[0]) + member.Name.Substring(1);
                names.Add(camel);
            }
        }

        foreach (var nested in type.GetTypeMembers()) {
            CollectFromType(nested, attributeType, names);
        }
    }

    private static void CollectFromNode(
        ParsedSqlNode node,
        string ns,
        string statementId,
        ISet<string>? sqlConstantNames,
        ImmutableArray<StringSubstitutionUsage>.Builder results) {

        switch (node) {
            case ParameterNode { IsStringSubstitution: true } paramNode:
                if (sqlConstantNames is null || !sqlConstantNames.Contains(paramNode.Name)) {
                    results.Add(new StringSubstitutionUsage(ns, statementId, paramNode.Name));
                }
                break;

            case MixedNode mixedNode:
                foreach (var child in mixedNode.Children) {
                    CollectFromNode(child, ns, statementId, sqlConstantNames, results);
                }
                break;

            case IfNode ifNode:
                foreach (var child in ifNode.Children) {
                    CollectFromNode(child, ns, statementId, sqlConstantNames, results);
                }
                break;

            case ChooseNode chooseNode:
                foreach (var when in chooseNode.Whens) {
                    foreach (var child in when.Children) {
                        CollectFromNode(child, ns, statementId, sqlConstantNames, results);
                    }
                }
                if (chooseNode.Otherwise is { } otherwise) {
                    foreach (var child in otherwise) {
                        CollectFromNode(child, ns, statementId, sqlConstantNames, results);
                    }
                }
                break;

            case WhereNode whereNode:
                foreach (var child in whereNode.Children) {
                    CollectFromNode(child, ns, statementId, sqlConstantNames, results);
                }
                break;

            case SetNode setNode:
                foreach (var child in setNode.Children) {
                    CollectFromNode(child, ns, statementId, sqlConstantNames, results);
                }
                break;

            case ForEachNode forEachNode:
                foreach (var child in forEachNode.Children) {
                    CollectFromNode(child, ns, statementId, sqlConstantNames, results);
                }
                break;

            case BindNode:
                break;
        }
    }
}

/**
 * ${} 문자열 치환이 감지된 위치 정보.
 */
public readonly record struct StringSubstitutionUsage(
    string Namespace,
    string StatementId,
    string ParameterName);
