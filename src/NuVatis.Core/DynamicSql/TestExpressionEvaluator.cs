using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using NuVatis.Internal;

namespace NuVatis.DynamicSql;

/**
 * MyBatis 호환 test 표현식을 C# 런타임에서 평가하는 엔진.
 * "name != null", "age > 0", "type == 'admin'" 등의 표현식을 지원한다.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public static class TestExpressionEvaluator {

    private static readonly string[] Operators = { "!=", "==", ">=", "<=", ">", "<" };

    /// <summary>
    /// MyBatis 호환 test 표현식을 평가하여 조건 참·거짓을 반환한다.
    /// <c>and</c>/<c>or</c> 복합 표현식과 비교 연산자(<c>==</c>, <c>!=</c>, <c>&gt;=</c>, 등)를 지원한다.
    /// <c>and</c>/<c>or</c>는 대소문자를 구분하지 않으며, 괄호로 우선순위를 지정할 수 있다.
    /// </summary>
    /// <param name="testExpression">평가할 표현식 문자열. <see langword="null"/> 또는 공백이면 항상 <see langword="true"/>.</param>
    /// <param name="parameter">파라미터 객체. 표현식의 프로퍼티 참조 기준.</param>
    /// <returns>표현식이 참이면 <see langword="true"/>.</returns>
    public static bool Evaluate(string? testExpression, object? parameter) {
        if (string.IsNullOrWhiteSpace(testExpression)) return true;
        if (parameter is null) return false;

        return EvaluateComposite(testExpression, parameter);
    }

    /**
     * 괄호 → or → and 순서로 분리해 재귀 평가한다.
     * 구분 탐색은 문자열 리터럴과 괄호 내부를 건너뛰므로 'a or b' 같은 리터럴이
     * 논리 연산자로 오인되지 않는다.
     */
    private static bool EvaluateComposite(string expression, object parameter) {
        expression = expression.Trim();

        if (IsWrappedInParens(expression)) {
            return EvaluateComposite(expression[1..^1], parameter);
        }

        var orParts = SplitTopLevel(expression, "or");
        if (orParts.Count > 1) {
            return orParts.Any(p => EvaluateComposite(p, parameter));
        }

        var andParts = SplitTopLevel(expression, "and");
        if (andParts.Count > 1) {
            return andParts.All(p => EvaluateComposite(p, parameter));
        }

        return EvaluateSubExpression(expression, parameter);
    }

    /**
     * 문자열 전체를 괄호로 감싸고 있는지 확인한다.
     * 대응하는 닫는 괄호가 마지막 문자여야 한다 — "(a) and (b)"는 false.
     */
    private static bool IsWrappedInParens(string expression) {
        if (expression.Length < 2 || expression[0] != '(' || expression[^1] != ')') return false;

        var depth = 0;
        for (var i = 0; i < expression.Length; i++) {
            var c = expression[i];
            if (c == '\'') {                       // 문자열 리터럴 안의 괄호는 무시
                i = SkipLiteral(expression, i);
                continue;
            }
            if (c == '(') depth++;
            else if (c == ')') {
                depth--;
                if (depth == 0) return i == expression.Length - 1;
            }
        }
        return false;
    }

    /**
     * 최상위(괄호 밖, 리터럴 밖)의 논리 연산자 기준으로 분리한다.
     * 빈 조각은 버린다 — 종전 Split(RemoveEmptyEntries)와 동일하다.
     */
    private static List<string> SplitTopLevel(string expression, string keyword) {
        var parts = new List<string>();
        var start = 0;

        while (true) {
            var index = FindTopLevelKeyword(expression, keyword, start);
            if (index < 0) break;

            AddIfNotBlank(parts, expression[start..index]);
            start = index + keyword.Length;
        }

        AddIfNotBlank(parts, expression[start..]);
        return parts;
    }

    private static void AddIfNotBlank(List<string> parts, string segment) {
        if (segment.Trim().Length > 0) parts.Add(segment);
    }

    /**
     * from 이후 위치에서 keyword를 찾되, 괄호 깊이 0이고 문자열 리터럴 밖이며
     * 좌우가 식별자 문자가 아닌 경우만 매칭한다(그래서 "or"는 "Order"와 매칭되지 않는다).
     * 찾지 못하면 -1을 반환한다.
     */
    private static int FindTopLevelKeyword(string expression, string keyword, int from) {
        var depth = 0;

        for (var i = from; i <= expression.Length - keyword.Length; i++) {
            var c = expression[i];

            if (c == '\'') {
                i = SkipLiteral(expression, i);
                continue;
            }
            if (c == '(') { depth++; continue; }
            if (c == ')') { depth--; continue; }
            if (depth != 0) continue;

            if (i > 0 && IsIdentifierChar(expression[i - 1])) continue;
            if (string.Compare(expression, i, keyword, 0, keyword.Length, StringComparison.OrdinalIgnoreCase) != 0) continue;

            var after = i + keyword.Length;
            if (after < expression.Length && IsIdentifierChar(expression[after])) continue;

            return i;
        }
        return -1;
    }

    private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    /// <summary>작은따옴표로 시작하는 리터럴의 닫는 따옴표 위치를 반환한다. 닫히지 않으면 끝까지 간다.</summary>
    private static int SkipLiteral(string expression, int openQuoteIndex) {
        for (var i = openQuoteIndex + 1; i < expression.Length; i++) {
            if (expression[i] == '\\') { i++; continue; }   // 이스케이프된 따옴표
            if (expression[i] == '\'') return i;
        }
        return expression.Length;
    }


    /// <summary>
    /// 점(.) 구분 경로로 객체의 중첩 프로퍼티 값을 읽는다.
    /// <c>size</c>/<c>length</c>는 각각 <c>Count</c>/<c>Length</c>로 자동 변환한다.
    /// </summary>
    /// <param name="obj">탐색을 시작할 루트 객체.</param>
    /// <param name="propertyPath">점 구분 프로퍼티 경로 (예: <c>"Address.City"</c>).</param>
    /// <returns>해당 경로의 값. 객체가 <see langword="null"/>이거나 경로를 찾지 못하면 <see langword="null"/>.</returns>
    [UnconditionalSuppressMessage("AOT", "IL2070",
        Justification = "동적 SQL 런타임 평가는 reflection 사용이 불가피. SG 생성 코드 경로에서는 호출되지 않음.")]
    [UnconditionalSuppressMessage("AOT", "IL2026",
        Justification = "PropertyReflectionCache 런타임 폴백 — SG 경로에서는 호출되지 않음.")]
    public static object? GetPropertyValue(object? obj, string propertyPath) {
        if (obj is null) return null;

        var parts   = propertyPath.Split('.');
        var current = obj;

        foreach (var part in parts) {
            if (current is null) return null;

            var memberName = part switch {
                "size"   => "Count",
                "length" => "Length",
                _        => part
            };

            var type  = current.GetType();
            var props = PropertyReflectionCache.GetOrBuild(type, normalizeUnderscore: false);

            if (props.TryGetValue(memberName, out var prop)) {
                current = prop.GetValue(current);
            } else {
                return null;
            }
        }

        return current;
    }

    private static bool EvaluateSubExpression(string expression, object parameter) {
        var selectedOp = Operators.FirstOrDefault(op => expression.Contains(op));

        if (selectedOp is null) {
            var val = GetPropertyValue(parameter, expression.Trim());
            return val switch {
                null           => false,
                string s       => !string.IsNullOrEmpty(s),
                ICollection c  => c.Count > 0,
                _              => true
            };
        }

        var opIndex      = expression.IndexOf(selectedOp, StringComparison.Ordinal);
        var propertyPath = expression[..opIndex].Trim();
        var valueStr     = expression[(opIndex + selectedOp.Length)..].Trim();
        var leftValue    = GetPropertyValue(parameter, propertyPath);
        var rightValue   = ParseValue(valueStr);

        return CompareValues(leftValue, selectedOp, rightValue);
    }

    private static object? ParseValue(string valueStr) {
        if (valueStr.Equals("null", StringComparison.OrdinalIgnoreCase)) return null;
        if (valueStr == "''" || valueStr == "\"\"") return string.Empty;
        if (valueStr.StartsWith('\'') && valueStr.EndsWith('\'') && valueStr.Length >= 2)
            return valueStr[1..^1];
        if (valueStr.StartsWith('"') && valueStr.EndsWith('"') && valueStr.Length >= 2)
            return valueStr[1..^1];
        if (bool.TryParse(valueStr, out var b)) return b;
        if (long.TryParse(valueStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var l)) return l;
        if (decimal.TryParse(valueStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) return d;
        return valueStr;
    }

    private static bool CompareValues(object? left, string op, object? right) {
        if (left is null && right is null) return op is "==" or ">==" or "<=";
        if (left is null || right is null) return op == "!=";

        if (op is "==" or "!=") {
            var areEqual = NormalizedEquals(left, right);
            return op == "==" ? areEqual : !areEqual;
        }

        if (left is IComparable comp) {
            try {
                var convertedRight = Convert.ChangeType(right, left.GetType(), CultureInfo.InvariantCulture);
                var result         = comp.CompareTo(convertedRight);
                return op switch {
                    ">"  => result > 0,
                    "<"  => result < 0,
                    ">=" => result >= 0,
                    "<=" => result <= 0,
                    _    => false
                };
            // Convert.ChangeType 실패 시 비교 불가 → false 반환 (타입 불일치는 정상 흐름)
            } catch {
                return false;
            }
        }

        return false;
    }

    private static bool NormalizedEquals(object left, object right) {
        if (left.Equals(right)) return true;

        try {
            var convertedRight = Convert.ChangeType(right, left.GetType(), CultureInfo.InvariantCulture);
            return left.Equals(convertedRight);
        // 양방향 타입 변환 시도 — 실패 시 역방향 변환 폴백
        } catch {
            try {
                var convertedLeft = Convert.ChangeType(left, right.GetType(), CultureInfo.InvariantCulture);
                return right.Equals(convertedLeft);
            // 역방향 변환도 실패 시 문자열 비교로 최종 폴백
            } catch {
                return string.Equals(left.ToString(), right.ToString(), StringComparison.Ordinal);
            }
        }
    }
}
