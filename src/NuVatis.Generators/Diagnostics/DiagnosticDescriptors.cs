#nullable enable
using Microsoft.CodeAnalysis;

namespace NuVatis.Generators.Diagnostics;

/**
 * NuVatis Source Generator 진단 코드 정의 (NV001~008).
 *
 * @author 최진호
 * @date   2026-02-24
 * @modified 2026-02-26 NV007 미사용 ResultMap, NV008 ResultMap Property 불일치 추가
 * @modified 2026-02-27 NV004 Warning → Error 승격
 */
public static class DiagnosticDescriptors {
    private const string Category = "NuVatis";

    public static readonly DiagnosticDescriptor ResultMapNotFound = new(
        id:                 "NV001",
        title:              "ResultMap Not Found",
        messageFormat:      "ResultMap '{0}'을 찾을 수 없습니다",
        category:           Category,
        defaultSeverity:    DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor StatementNotFound = new(
        id:                 "NV002",
        title:              "Statement Not Found",
        messageFormat:      "'{0}' 인터페이스의 '{1}' 메서드와 매칭되는 statement가 없습니다",
        category:           Category,
        defaultSeverity:    DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ParameterNotFound = new(
        id:                 "NV003",
        title:              "Parameter Not Found",
        messageFormat:      "파라미터 '{0}'이 '{1}' 타입에 존재하지 않습니다",
        category:           Category,
        defaultSeverity:    DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SqlInjectionWarning = new(
        id:                 "NV004",
        title:              "SQL Injection Warning",
        messageFormat:      "${{{0}}} in '{1}.{2}' uses string substitution which is vulnerable to SQL injection; use #{{{0}}} instead",
        category:           Category,
        defaultSeverity:    DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri:        "https://nuvatis.dev/docs/security/string-substitution");

    public static readonly DiagnosticDescriptor TestExpressionCompilationFailed = new(
        id:                 "NV005",
        title:              "Test Expression Compilation Failed",
        messageFormat:      "test 표현식 컴파일 실패: '{0}'",
        category:           Category,
        defaultSeverity:    DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ResultMapColumnMismatch = new(
        id:                 "NV006",
        title:              "ResultMap Column Mismatch",
        messageFormat:      "ResultMap '{0}'의 column '{1}'이 '{2}' 타입의 프로퍼티와 매칭되지 않습니다",
        category:           Category,
        defaultSeverity:    DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnusedResultMap = new(
        id:                 "NV007",
        title:              "Unused ResultMap",
        messageFormat:      "ResultMap '{0}'이 namespace '{1}'의 어떤 statement에서도 참조되지 않습니다",
        category:           Category,
        defaultSeverity:    DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ResultMapPropertyNotFound = new(
        id:                 "NV008",
        title:              "ResultMap Property Not Found",
        messageFormat:      "ResultMap '{0}'의 property '{1}'이 대상 타입 '{2}'에 존재하지 않습니다",
        category:           Category,
        defaultSeverity:    DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
