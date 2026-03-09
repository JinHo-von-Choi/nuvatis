using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
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
    /// <c>and</c>/<c>or</c> 복합 표현식과 비교 연산자(<c>==</c>, <c>!=</c>, <c>&gt;</c>, 등)를 지원한다.
    /// </summary>
    /// <param name="testExpression">평가할 표현식 문자열. <see langword="null"/> 또는 공백이면 항상 <see langword="true"/>.</param>
    /// <param name="parameter">파라미터 객체. 표현식의 프로퍼티 참조 기준.</param>
    /// <returns>표현식이 참이면 <see langword="true"/>.</returns>
    public static bool Evaluate(string? testExpression, object? parameter) {
        if (string.IsNullOrWhiteSpace(testExpression)) return true;
        if (parameter is null) return false;

        var orParts = testExpression.Split(new[] { " or ", " OR " }, StringSplitOptions.RemoveEmptyEntries);
        if (orParts.Length > 1) {
            return orParts.Any(p => Evaluate(p, parameter));
        }

        var andParts = testExpression.Split(new[] { " and ", " AND " }, StringSplitOptions.RemoveEmptyEntries);
        if (andParts.Length > 1) {
            return andParts.All(p => Evaluate(p, parameter));
        }

        return EvaluateSubExpression(testExpression.Trim(), parameter);
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
        } catch {
            try {
                var convertedLeft = Convert.ChangeType(left, right.GetType(), CultureInfo.InvariantCulture);
                return right.Equals(convertedLeft);
            } catch {
                return string.Equals(left.ToString(), right.ToString(), StringComparison.Ordinal);
            }
        }
    }
}
