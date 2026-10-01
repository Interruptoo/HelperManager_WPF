using System.Text;
using System.Text.RegularExpressions;

namespace HelperManager.Modules.QueryStore.Services;

/// <summary>
/// 일부 쿼리에 섞여 있는 iBatis 스타일의 동적 SQL 태그
/// (&lt;IsEqual&gt;, &lt;IsNotNull&gt;, &lt;Dynamic&gt; 등)를, 사용자가 파라미터 패널에
/// 입력한 값을 기준으로 직접 평가해서 조건에 맞는 SQL 조각만 남기고 나머지는 제거합니다.
///
/// 이 태그들은 원래 애플리케이션(iBatis 런타임)이 실행 시점에 해석하는 것이라 DBMS 도구에서는
/// 그대로 실행할 수 없기 때문에, 클립보드로 복사하기 전에 "이미 조건이 반영된 순수 SQL"로
/// 미리 펼쳐주는 역할을 한다.
///
/// 지원 태그 (대소문자 구분하지 않음):
///   - IsEqual / IsNotEqual     (Property, CompareValue 비교)
///   - IsNull / IsNotNull, IsEmpty / IsNotEmpty, IsParameterPresent / IsNotParameterPresent
///     (Property 값이 비어있는지 여부)
///   - IsGreaterThan / IsGreaterEqual / IsLessThan / IsLessEqual (Property, CompareValue 대소 비교)
///   - Dynamic (Prepend)        하위에서 살아남은 조건들을 모아 맨 앞의 AND/OR 하나만 제거하고
///                              자신의 Prepend(보통 "WHERE")를 붙인다.
///   - Iterate                 목록 반복 태그. 값 하나만 입력받는 이 화면에서는 실제 반복을
///                              재현할 수 없어, 내용을 한 번만 펼치고 주석으로 표시해 둔다.
/// </summary>
public static class DynamicSqlResolver
{
    private const string ConditionTagAlternation =
        "IsEqual|IsNotEqual|IsNull|IsNotNull|IsEmpty|IsNotEmpty|" +
        "IsGreaterThan|IsGreaterEqual|IsLessThan|IsLessEqual|" +
        "IsParameterPresent|IsNotParameterPresent";

    private const string AllTagAlternation = ConditionTagAlternation + "|Dynamic|Iterate";

    private static readonly Regex OpeningTagPattern = new(
        $@"<(?<name>{AllTagAlternation})\b(?<attrs>[^>]*?)(?<selfclose>/)?>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AttributePattern = new(
        @"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*""(?<value>[^""]*)""",
        RegexOptions.Compiled);

    /// <summary>
    /// 쿼리 본문에서 Property="..." 로 참조되는 모든 속성명을 찾는다.
    /// (:NAME 바인드 변수 목록과 합쳐서, 화면의 파라미터 입력 목록을 구성하는 데 쓰인다.
    ///  이런 속성은 SQL 안에 직접 바인드로 쓰이지 않고 "어느 조건 블록을 포함할지"만 결정하는
    ///  경우도 있어서, 바인드 변수만으로는 찾을 수 없는 입력 항목이다.)
    /// </summary>
    public static IReadOnlyList<string> ExtractPropertyNames(string sqlText)
    {
        var names = new List<string>();

        foreach (Match match in OpeningTagPattern.Matches(sqlText))
        {
            var attrs = ParseAttributes(match.Groups["attrs"].Value);
            if (attrs.TryGetValue("Property", out var property) && !string.IsNullOrWhiteSpace(property))
            {
                names.Add(property);
            }
        }

        return names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>입력된 파라미터 값을 기준으로 동적 SQL 태그를 전부 평가/제거하고, 조건에 맞는 SQL 조각만 남긴다.</summary>
    public static string Resolve(string sqlText, IReadOnlyDictionary<string, string> parameterValues) =>
        ResolveInternal(sqlText, parameterValues);

    private static string ResolveInternal(string text, IReadOnlyDictionary<string, string> parameterValues)
    {
        var result = new StringBuilder();
        var position = 0;

        while (true)
        {
            var openMatch = OpeningTagPattern.Match(text, position);
            if (!openMatch.Success)
            {
                result.Append(text, position, text.Length - position);
                break;
            }

            // 태그 앞의 일반 텍스트(SQL 조각)는 손대지 않고 그대로 옮긴다.
            result.Append(text, position, openMatch.Index - position);

            var tagName = openMatch.Groups["name"].Value;
            var attrs = ParseAttributes(openMatch.Groups["attrs"].Value);
            var isSelfClosing = openMatch.Groups["selfclose"].Success;

            string innerRaw;
            int afterTagIndex;

            if (isSelfClosing)
            {
                innerRaw = string.Empty;
                afterTagIndex = openMatch.Index + openMatch.Length;
            }
            else
            {
                var (contentEnd, closeTagEnd) = FindMatchingClose(text, openMatch.Index + openMatch.Length, tagName);
                if (contentEnd < 0)
                {
                    // 짝이 맞는 닫는 태그를 못 찾으면(= 예상과 다른 형식) 더 이상 건드리지 않고 원본 그대로 둔다.
                    result.Append(text, openMatch.Index, text.Length - openMatch.Index);
                    position = text.Length;
                    break;
                }

                innerRaw = text[(openMatch.Index + openMatch.Length)..contentEnd];
                afterTagIndex = closeTagEnd;
            }

            // 안쪽에 또 다른 동적 태그가 중첩되어 있을 수 있으므로 먼저 재귀적으로 처리한다.
            var innerResolved = ResolveInternal(innerRaw, parameterValues);

            result.Append(EvaluateTag(tagName, attrs, innerResolved, parameterValues));

            position = afterTagIndex;
        }

        return result.ToString();
    }

    private static string EvaluateTag(
        string tagName,
        IReadOnlyDictionary<string, string> attrs,
        string innerResolved,
        IReadOnlyDictionary<string, string> parameterValues)
    {
        attrs.TryGetValue("Prepend", out var prepend);
        prepend ??= string.Empty;

        if (tagName.Equals("Dynamic", StringComparison.OrdinalIgnoreCase))
        {
            var trimmed = innerResolved.Trim();
            if (trimmed.Length == 0)
            {
                return string.Empty; // 살아남은 조건이 하나도 없으면 WHERE 자체를 만들지 않는다.
            }

            // 안쪽에서 살아남은 첫 조각이 자기 자신의 AND/OR 를 달고 나오므로(예: "AND X = 1"),
            // Dynamic 의 Prepend(보통 "WHERE")와 중복되지 않도록 맨 앞의 AND/OR 하나만 제거한다.
            var stripped = Regex.Replace(trimmed, @"^(AND|OR)\b\s*", string.Empty, RegexOptions.IgnoreCase);
            return string.IsNullOrWhiteSpace(prepend) ? $" {stripped} " : $" {prepend} {stripped} ";
        }

        if (tagName.Equals("Iterate", StringComparison.OrdinalIgnoreCase))
        {
            // 목록을 순회하며 여러 번 반복해야 하는 태그이지만, 이 화면은 값을 하나씩만 입력받으므로
            // 실제 반복은 재현할 수 없다. 내용을 한 번만 펼치고, 수동 확인이 필요함을 주석으로 남긴다.
            return $" /* ITERATE: 목록 반복이 필요한 구간입니다 - 아래 내용을 직접 확인/수정하세요 */ {innerResolved} ";
        }

        if (!attrs.TryGetValue("Property", out var property) || string.IsNullOrWhiteSpace(property))
        {
            return string.Empty;
        }

        parameterValues.TryGetValue(property, out var actualValue);
        actualValue ??= string.Empty;
        attrs.TryGetValue("CompareValue", out var compareValue);
        compareValue ??= string.Empty;

        if (!EvaluateCondition(tagName, actualValue, compareValue))
        {
            return string.Empty;
        }

        var trimmedInner = innerResolved.Trim();
        return string.IsNullOrWhiteSpace(prepend) ? $" {trimmedInner} " : $" {prepend} {trimmedInner} ";
    }

    private static bool EvaluateCondition(string tagName, string actualValue, string compareValue)
    {
        var hasValue = !string.IsNullOrEmpty(actualValue);

        return tagName.ToUpperInvariant() switch
        {
            "ISNULL" or "ISEMPTY" or "ISNOTPARAMETERPRESENT" => !hasValue,
            "ISNOTNULL" or "ISNOTEMPTY" or "ISPARAMETERPRESENT" => hasValue,
            "ISEQUAL" => string.Equals(actualValue, compareValue, StringComparison.OrdinalIgnoreCase),
            "ISNOTEQUAL" => !string.Equals(actualValue, compareValue, StringComparison.OrdinalIgnoreCase),
            "ISGREATERTHAN" => Compare(actualValue, compareValue) > 0,
            "ISGREATEREQUAL" => Compare(actualValue, compareValue) >= 0,
            "ISLESSTHAN" => Compare(actualValue, compareValue) < 0,
            "ISLESSEQUAL" => Compare(actualValue, compareValue) <= 0,
            _ => false, // 알 수 없는 태그는 안전하게 "조건 불충족"으로 처리해 내용을 제외한다.
        };
    }

    /// <summary>가능하면 숫자로, 아니면 문자열로 비교한다.</summary>
    private static int Compare(string left, string right)
    {
        if (decimal.TryParse(left, out var leftNumber) && decimal.TryParse(right, out var rightNumber))
        {
            return leftNumber.CompareTo(rightNumber);
        }

        return string.CompareOrdinal(left, right);
    }

    private static Dictionary<string, string> ParseAttributes(string attributeText)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in AttributePattern.Matches(attributeText))
        {
            result[match.Groups["name"].Value] = match.Groups["value"].Value;
        }

        return result;
    }

    /// <summary>
    /// openSearchStart 위치부터 같은 이름의 태그 중첩 깊이를 세어, 처음 열린 태그와 짝이 맞는
    /// 닫는 태그를 찾는다. 반환값: (여는 태그 바로 뒤 ~ 닫는 태그 시작 전까지의 내용 끝 위치, 닫는 태그 끝 위치).
    /// 짝을 못 찾으면 (-1, -1).
    /// </summary>
    private static (int ContentEnd, int CloseTagEnd) FindMatchingClose(string text, int openSearchStart, string tagName)
    {
        var tagScanner = new Regex(
            $@"<(?<close>/)?{Regex.Escape(tagName)}\b[^>]*?(?<selfclose>/)?>",
            RegexOptions.IgnoreCase);

        var depth = 1;
        var position = openSearchStart;

        while (position < text.Length)
        {
            var match = tagScanner.Match(text, position);
            if (!match.Success)
            {
                return (-1, -1);
            }

            if (match.Groups["close"].Success)
            {
                depth--;
                if (depth == 0)
                {
                    return (match.Index, match.Index + match.Length);
                }
            }
            else if (!match.Groups["selfclose"].Success)
            {
                depth++;
            }

            position = match.Index + match.Length;
        }

        return (-1, -1);
    }
}
