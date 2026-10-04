using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace NuVatis.Generators.Analysis;

/**
 * 파라미터 객체의 정적 타입이 해석되는 statement에 대해,
 * 리플렉션 대신 직접 접근을 방출할 수 있는 프로퍼티 집합.
 *
 * 안전 계약: 해석되지 않거나 존재하지 않는 프로퍼티는 이 집합에 들어가지 않으므로
 * 호출자는 기존의 __getprop_ 리플렉션 경로로 되돌린다. 따라서 오타가 컴파일 에러로
 * 바뀌는 등 기존 동작이 달라지지 않는다.
 */
internal sealed class TypedPropertyAccess {

    /** 생성 코드에서 사용하는 지역 변수의 접두사. */
    internal const string LocalName = "__typed_";

    private readonly Dictionary<string, string> _declaredNames;

    internal TypedPropertyAccess(string typeFqn, Dictionary<string, string> declaredNames) {
        TypeFqn = typeFqn;
        _declaredNames = declaredNames;
    }

    /** global:: 접두사를 붙인 전체 타입 이름. */
    internal string TypeFqn { get; }

    /** 방출 대상이 하나라도 존재하는가 (로컬 변수 선언 여부 결정). */
    internal bool IsEmpty => _declaredNames.Count == 0;

    /**
     * 선언된 정확한 대소문자의 프로퍼티 이름을 반환한다.
     * 조회는 대소문자를 구분하지 않는다 — __getprop_가 BindingFlags.IgnoreCase를 쓰므로
     * 같은 의미로 맞추기 위해서다.
     */
    internal bool TryGetDeclaredName(string referencedName, out string declaredName) {
        return _declaredNames.TryGetValue(referencedName, out declaredName!);
    }

    /**
     * __param_를 정적 타입으로 한 번만 좁힌 지역 변수를 선언하는 코드를 생성한다.
     * 타입이 맞지 않거나 null이면 null이 되므로 __getprop_의 "없으면 null" 의미가 보존된다.
     */
    internal string BuildLocalDeclaration(string paramVar) {
        return $"var {LocalName} = {paramVar} is {TypeFqn} ? ({TypeFqn}){paramVar} : ({TypeFqn}?)null;";
    }

    /** 직접 접근 표현식. */
    internal string BuildAccess(string declaredName) => $"{LocalName}?.{declaredName}";
}

/**
 * TypedPropertyAccess를 컴파일 심볼에서 계산한다.
 */
internal static class TypedPropertyAccessResolver {

    /**
     * statement가 참조하는 프로퍼티 중, 파라미터 타입에서 실제로 읽을 수 있는 것만 담긴
     * TypedPropertyAccess를 반환한다. 하나도 해석되지 않으면 null을 반환해
     * 호출자가 리플렉션 경로만 방출하도록 한다.
     *
     * @param compilation Roslyn 컴파일 (TypeResolver.TypeResolver 등 심볼 조회에 사용)
     * @param typeFqn     매퍼 메서드의 데이터 파라미터 타입 FQN. null/비어 있음/Object/dynamic면 null
     * @param statement   프로퍼티 참조를 수집할 노드 트리
     */
    public static TypedPropertyAccess? Resolve(
        Compilation? compilation,
        string? typeFqn,
        NuVatis.Generators.Models.ParsedSqlNode statement) {

        if (string.IsNullOrEmpty(typeFqn) || compilation is null) return null;

        // System.Object / dynamic 은 정적 타입 정보를 주지 못한다 — 리플렉션에 맡긴다
        if (typeFqn == "object" || typeFqn == "dynamic") return null;

        var metadataName = typeFqn!;
        var symbol = compilation.GetTypeByMetadataName(metadataName);
        if (symbol is null || symbol.TypeKind is not (TypeKind.Class or TypeKind.Struct or TypeKind.Interface)) {
            return null;
        }
        if (symbol.SpecialType == SpecialType.System_Object) return null;

        var referenced = ParameterPropertyCollector.Collect(statement);
        if (referenced.Count == 0) return null;

        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in referenced) {
            var declared = FindReadableProperty(symbol, name);
            if (declared is not null) resolved[name] = declared;
        }

        if (resolved.Count == 0) return null;

        return new TypedPropertyAccess("global::" + typeFqn, resolved);
    }

    /**
     * 타입과 그 모든 기반 타입에서 이름(대소문자 무시)으로 읽을 수 있는 공개 인스턴스
     * 프로퍼티를 찾는다. 못 찾으면 null — 호출자가 리플렉션으로 폴백한다.
     */
    private static string? FindReadableProperty(INamedTypeSymbol type, string name) {
        for (var current = type; current is not null; current = current.BaseType) {
            foreach (var member in current.GetMembers(name)) {
                if (member is not IPropertySymbol prop) continue;
                if (prop.IsStatic || prop.IsIndexer) continue;
                if (prop.DeclaredAccessibility != Accessibility.Public) continue;
                if (prop.GetMethod is null) continue;
                return prop.Name;
            }

            // GetMembers(name)은 선언된 타입의 멤버만 돌려준다. 상속분도 봐야 한다.
            foreach (var member in current.GetMembers().OfType<IPropertySymbol>()) {
                if (member.IsStatic || member.IsIndexer) continue;
                if (member.DeclaredAccessibility != Accessibility.Public) continue;
                if (member.GetMethod is null) continue;
                if (string.Equals(member.Name, name, System.StringComparison.OrdinalIgnoreCase)) {
                    return member.Name;
                }
            }
        }
        return null;
    }
}

/**
 * statement 노드 트리가 참조하는 최상위 파라미터 프로퍼티 이름을 모은다.
 *
 * 이 목록은 방출기가 실제로 접근하는 이름과 정확히 일치해야 하므로,
 * test 표현식 추출 로직을 ParameterEmitter와 공유한다.
 */
internal static class ParameterPropertyCollector {

    private static readonly char[] Dots = { '.' };

    public static HashSet<string> Collect(NuVatis.Generators.Models.ParsedSqlNode root) {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Walk(root, result);
        return result;
    }

    private static void Walk(NuVatis.Generators.Models.ParsedSqlNode node, HashSet<string> into) {
        switch (node) {
            case NuVatis.Generators.Models.TextNode:
                break;

            case NuVatis.Generators.Models.ParameterNode param:
                AddRoot(param.Name, into);
                break;

            case NuVatis.Generators.Models.IfNode ifNode:
                AddRoot(TestExpressionRoot(ifNode.Test), into);
                foreach (var child in ifNode.Children) Walk(child, into);
                break;

            case NuVatis.Generators.Models.ChooseNode choose:
                foreach (var when in choose.Whens) {
                    AddRoot(TestExpressionRoot(when.Test), into);
                    foreach (var child in when.Children) Walk(child, into);
                }
                if (choose.Otherwise is not null) {
                    foreach (var child in choose.Otherwise) Walk(child, into);
                }
                break;

            case NuVatis.Generators.Models.WhereNode where:
                foreach (var child in where.Children) Walk(child, into);
                break;

            case NuVatis.Generators.Models.SetNode set:
                foreach (var child in set.Children) Walk(child, into);
                break;

            case NuVatis.Generators.Models.ForEachNode forEach:
                AddRoot(forEach.Collection, into);
                foreach (var child in forEach.Children) Walk(child, into);
                break;

            case NuVatis.Generators.Models.MixedNode mixed:
                foreach (var child in mixed.Children) Walk(child, into);
                break;
        }
    }

    /**
     * 중첩 경로("user.Name")는 최상위 "user"만 수집한다.
     * 세그먼트별 타입을 잇는 심볼 해석은 범위 밖이므로 이런 경로는 리플렉션에 남는다.
     */
    private static void AddRoot(string path, HashSet<string> into) {
        if (string.IsNullOrWhiteSpace(path)) return;
        var root = path.Split(Dots)[0];
        if (!string.IsNullOrWhiteSpace(root)) into.Add(root);
    }

    /**
     * test 표현식의 첫 토큰을 프로퍼티명으로 본다.
     * ParameterEmitter.ExtractPropertyName과 동일한 규칙이어야 한다.
     */
    private static string TestExpressionRoot(string testExpression) {
        var trimmed = testExpression.Trim();
        var spaceIdx = trimmed.IndexOf(' ');
        return spaceIdx > 0 ? trimmed.Substring(0, spaceIdx) : trimmed;
    }
}
