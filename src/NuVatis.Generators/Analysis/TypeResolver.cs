#nullable enable
using System.Linq;
using Microsoft.CodeAnalysis;

namespace NuVatis.Generators.Analysis;

/**
 * Roslyn 타입 심볼 분석 유틸리티.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public static class TypeResolver {
    public static string GetFullyQualifiedName(ITypeSymbol symbol) {
        return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", "");
    }

    public static bool IsTaskType(ITypeSymbol symbol) {
        if (symbol is INamedTypeSymbol namedType) {
            var fullName = GetFullyQualifiedName(namedType);
            return fullName == "System.Threading.Tasks.Task"
                || fullName.StartsWith("System.Threading.Tasks.Task<");
        }
        return false;
    }

    public static ITypeSymbol? UnwrapTask(ITypeSymbol symbol) {
        if (symbol is INamedTypeSymbol { IsGenericType: true } namedType) {
            var fullName = GetFullyQualifiedName(namedType);
            if (fullName.StartsWith("System.Threading.Tasks.Task<")) {
                return namedType.TypeArguments.FirstOrDefault();
            }
        }
        return null;
    }

    public static bool IsCollectionType(ITypeSymbol symbol) {
        if (symbol is IArrayTypeSymbol) return true;
        if (symbol is INamedTypeSymbol namedType) {
            var fullName = GetFullyQualifiedName(namedType);
            return fullName.StartsWith("System.Collections.Generic.IEnumerable<")
                || fullName.StartsWith("System.Collections.Generic.List<")
                || fullName.StartsWith("System.Collections.Generic.IList<")
                || fullName.StartsWith("System.Collections.Generic.ICollection<")
                || fullName.StartsWith("System.Collections.Generic.IReadOnlyList<")
                || fullName.StartsWith("System.Collections.Generic.IReadOnlyCollection<");
        }
        return false;
    }

    public static ITypeSymbol? GetCollectionElementType(ITypeSymbol symbol) {
        if (symbol is IArrayTypeSymbol arrayType) return arrayType.ElementType;
        if (symbol is INamedTypeSymbol { IsGenericType: true } namedType) {
            return namedType.TypeArguments.FirstOrDefault();
        }
        return null;
    }

    public static bool IsNullableType(ITypeSymbol symbol) {
        if (symbol.NullableAnnotation == NullableAnnotation.Annotated) return true;
        if (symbol is INamedTypeSymbol { IsGenericType: true } namedType) {
            return GetFullyQualifiedName(namedType).StartsWith("System.Nullable<");
        }
        return false;
    }

    public static ITypeSymbol? UnwrapNullable(ITypeSymbol symbol) {
        if (symbol is INamedTypeSymbol { IsGenericType: true } namedType
            && GetFullyQualifiedName(namedType).StartsWith("System.Nullable<")) {
            return namedType.TypeArguments.FirstOrDefault();
        }
        return symbol;
    }
}
