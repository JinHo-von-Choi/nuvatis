#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace NuVatis.Generators.Analysis;

/**
 * Compilation에서 Mapper 인터페이스 후보를 찾아 분석한다.
 *
 * [NuVatisMapper] 어트리뷰트가 부착된 인터페이스, 또는 메서드에
 * NuVatis SQL 어트리뷰트([Select], [Insert], [Update], [Delete])가
 * 있는 인터페이스만 대상으로 한다.
 *
 * "Mapper" 접미사 관례 기반 스캔은 외부 라이브러리(AutoMapper 등)와의
 * 네이밍 충돌을 유발하므로 제거되었다.
 *
 * @author 최진호
 * @date   2026-02-25
 */
public static class InterfaceAnalyzer {

    private const string NuVatisMapperAttributeName = "NuVatis.Attributes.NuVatisMapperAttribute";
    private const string NuVatisAttributeNamespace   = "NuVatis.Attributes";

    public static ImmutableArray<MapperInterfaceInfo> FindMapperInterfaces(
        Compilation compilation, CancellationToken ct) {

        var results = ImmutableArray.CreateBuilder<MapperInterfaceInfo>();

        foreach (var symbol in GetAllTypes(compilation.GlobalNamespace)) {
            ct.ThrowIfCancellationRequested();

            if (symbol is not INamedTypeSymbol { TypeKind: TypeKind.Interface } typeSymbol)
                continue;

            var hasNuVatisMapperAttribute = HasAttribute(typeSymbol, NuVatisMapperAttributeName);

            var methods = typeSymbol.GetMembers()
                .OfType<IMethodSymbol>()
                .Select(AnalyzeMethod)
                .ToImmutableArray();

            var hasNuVatisSqlAttributes = methods.Any(m => m.SqlAttributeType != null);

            if (hasNuVatisMapperAttribute || hasNuVatisSqlAttributes) {
                results.Add(new MapperInterfaceInfo(
                    TypeResolver.GetFullyQualifiedName(typeSymbol),
                    typeSymbol.Name,
                    typeSymbol.ContainingNamespace?.ToDisplayString() ?? "",
                    methods
                ));
            }
        }

        return results.ToImmutable();
    }

    private static bool HasAttribute(INamedTypeSymbol typeSymbol, string fullyQualifiedAttributeName) {
        foreach (var attr in typeSymbol.GetAttributes()) {
            var attrClass = attr.AttributeClass;
            if (attrClass is null) continue;

            var fullName = attrClass.ContainingNamespace?.ToDisplayString() + "." + attrClass.Name;
            if (fullName == fullyQualifiedAttributeName) return true;
        }
        return false;
    }

    private static MapperMethodInfo AnalyzeMethod(IMethodSymbol method) {
        var returnType     = method.ReturnType;
        var isAsync        = TypeResolver.IsTaskType(returnType);
        var effectiveType  = isAsync ? TypeResolver.UnwrapTask(returnType) : returnType;

        string? unwrappedReturnType = isAsync
            ? (effectiveType is null ? "void" : TypeResolver.GetFullyQualifiedName(effectiveType))
            : null;

        var returnsList = false;
        string? elementType = null;

        if (effectiveType is not null) {
            returnsList = TypeResolver.IsCollectionType(effectiveType);
            if (returnsList) {
                var element = TypeResolver.GetCollectionElementType(effectiveType);
                if (element is not null) {
                    elementType = TypeResolver.GetFullyQualifiedName(element);
                }
            }
        }

        var parameters = method.Parameters
            .Select(p => new MapperParameterInfo(
                p.Name,
                TypeResolver.GetFullyQualifiedName(p.Type),
                TypeResolver.GetFullyQualifiedName(p.Type) == "System.Threading.CancellationToken"
            ))
            .ToImmutableArray();

        string? sqlAttrType    = null;
        string? sqlAttrValue   = null;
        string? resultMapValue = null;

        foreach (var attr in method.GetAttributes()) {
            var attrName = attr.AttributeClass?.Name;
            var attrNs   = attr.AttributeClass?.ContainingNamespace?.ToDisplayString();

            if (attrNs != "NuVatis.Attributes") continue;

            switch (attrName) {
                case "SelectAttribute" or "InsertAttribute" or "UpdateAttribute" or "DeleteAttribute":
                    sqlAttrType  = attrName.Replace("Attribute", "");
                    sqlAttrValue = attr.ConstructorArguments.FirstOrDefault().Value?.ToString();
                    break;
                case "ResultMapAttribute":
                    resultMapValue = attr.ConstructorArguments.FirstOrDefault().Value?.ToString();
                    break;
            }
        }

        return new MapperMethodInfo(
            method.Name,
            TypeResolver.GetFullyQualifiedName(returnType),
            isAsync,
            unwrappedReturnType,
            returnsList,
            elementType,
            parameters,
            sqlAttrType,
            sqlAttrValue,
            resultMapValue
        );
    }

    private static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol ns) {
        foreach (var member in ns.GetTypeMembers()) {
            yield return member;
        }
        foreach (var childNs in ns.GetNamespaceMembers()) {
            foreach (var type in GetAllTypes(childNs)) {
                yield return type;
            }
        }
    }
}

public sealed record MapperInterfaceInfo(
    string FullyQualifiedName,
    string Name,
    string Namespace,
    ImmutableArray<MapperMethodInfo> Methods);

public sealed record MapperMethodInfo(
    string Name,
    string ReturnType,
    bool IsAsync,
    string? UnwrappedReturnType,
    bool ReturnsList,
    string? ElementType,
    ImmutableArray<MapperParameterInfo> Parameters,
    string? SqlAttributeType,
    string? SqlAttributeValue,
    string? ResultMapAttributeValue);

public sealed record MapperParameterInfo(
    string Name,
    string Type,
    bool IsCancellationToken);
