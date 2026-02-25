#nullable enable
using System.Collections.Immutable;
using NuVatis.Generators.Models;

namespace NuVatis.Generators.Diagnostics;

/**
 * ParsedMapper 내 ${}(문자열 치환) 사용을 탐지하여 SQL Injection 경고를 수집한다.
 * 파싱된 SQL 노드 트리를 재귀 순회하며 ParameterNode.IsStringSubstitution == true인
 * 노드를 발견하면 NV004 경고 대상으로 보고한다.
 *
 * @author   최진호
 * @date     2026-02-25
 */
public static class StringSubstitutionAnalyzer {

    /**
     * 단일 ParsedMapper 내 모든 statement를 검사하여
     * ${} 사용 위치를 (statementId, paramName) 쌍으로 반환한다.
     */
    public static ImmutableArray<StringSubstitutionUsage> Analyze(ParsedMapper mapper) {
        var results = ImmutableArray.CreateBuilder<StringSubstitutionUsage>();

        foreach (var statement in mapper.Statements) {
            CollectFromNode(statement.RootNode, mapper.Namespace, statement.Id, results);
        }

        return results.ToImmutable();
    }

    private static void CollectFromNode(
        ParsedSqlNode node,
        string ns,
        string statementId,
        ImmutableArray<StringSubstitutionUsage>.Builder results) {

        switch (node) {
            case ParameterNode { IsStringSubstitution: true } paramNode:
                results.Add(new StringSubstitutionUsage(ns, statementId, paramNode.Name));
                break;

            case MixedNode mixedNode:
                foreach (var child in mixedNode.Children) {
                    CollectFromNode(child, ns, statementId, results);
                }
                break;

            case IfNode ifNode:
                foreach (var child in ifNode.Children) {
                    CollectFromNode(child, ns, statementId, results);
                }
                break;

            case ChooseNode chooseNode:
                foreach (var when in chooseNode.Whens) {
                    foreach (var child in when.Children) {
                        CollectFromNode(child, ns, statementId, results);
                    }
                }
                if (chooseNode.Otherwise is { } otherwise) {
                    foreach (var child in otherwise) {
                        CollectFromNode(child, ns, statementId, results);
                    }
                }
                break;

            case WhereNode whereNode:
                foreach (var child in whereNode.Children) {
                    CollectFromNode(child, ns, statementId, results);
                }
                break;

            case SetNode setNode:
                foreach (var child in setNode.Children) {
                    CollectFromNode(child, ns, statementId, results);
                }
                break;

            case ForEachNode forEachNode:
                foreach (var child in forEachNode.Children) {
                    CollectFromNode(child, ns, statementId, results);
                }
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
