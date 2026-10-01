using System.Text.RegularExpressions;

namespace HelperManager.Modules.QueryStore.Services;

/// <summary>
/// Query Store 화면에서 "쿼리 모음 XML 원본"을 DBMS 도구에 바로 붙여넣어 실행할 수 있는
/// 순수 SQL 텍스트로 가공하기 위한 보조 기능 모음입니다.
///   1) &lt;sql id="..."&gt;, &lt;!-- 주석 --&gt;, &lt;/sql&gt; 같은 XML 겉포장을 제거하고,
///   2) 본문에 남은 :NAME 형태의 바인드 변수 이름을 모두 찾아내고,
///   3) 사용자가 입력한 값으로 바인드 변수를 실제 값으로 치환한다.
/// DB 연결/실행 기능 없이도, 파라미터만 채워서 바로 실행 가능한 SQL을 클립보드로 넘기기 위함이다.
/// </summary>
public static partial class SqlTextHelper
{
    [GeneratedRegex("""<sql\s+id\s*=\s*"[^"]*"[^>]*>""", RegexOptions.IgnoreCase)]
    private static partial Regex OpeningTagPattern();

    [GeneratedRegex("""</sql\s*>""", RegexOptions.IgnoreCase)]
    private static partial Regex ClosingTagPattern();

    [GeneratedRegex("""<!--[\s\S]*?-->""")]
    private static partial Regex CommentPattern();

    [GeneratedRegex(@"(?<!\w):([A-Za-z_][A-Za-z0-9_]*)")]
    private static partial Regex BindVariablePattern();

    /// <summary>
    /// &lt;sql id="..."&gt;...&lt;/sql&gt; 원본 텍스트에서 XML 태그와 주석 헤더를 제거해,
    /// DBMS 도구에 바로 붙여넣어 실행할 수 있는 순수 SQL 본문만 남긴다.
    /// </summary>
    public static string StripXmlWrapper(string rawContent)
    {
        var withoutComments = CommentPattern().Replace(rawContent, string.Empty);
        var withoutOpenTag = OpeningTagPattern().Replace(withoutComments, string.Empty);
        var withoutCloseTag = ClosingTagPattern().Replace(withoutOpenTag, string.Empty);
        return withoutCloseTag.Trim();
    }

    /// <summary>SQL 본문에서 :NAME 형태의 바인드 변수 이름을 중복 없이(처음 등장 순서대로) 모두 찾는다.</summary>
    public static IReadOnlyList<string> ExtractBindVariableNames(string sqlText) =>
        BindVariablePattern()
            .Matches(sqlText)
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// SQL 본문에 있는 :NAME 바인드 변수를 지정된 치환 값으로 전부 바꾼다.
    /// (값이 없는 변수는 애초에 <paramref name="substitutions"/> 에 포함시키지 않으면 그대로 남는다.)
    /// </summary>
    public static string SubstituteBindVariables(string sqlText, IEnumerable<(string Name, string Replacement)> substitutions)
    {
        var result = sqlText;

        foreach (var (name, replacement) in substitutions)
        {
            var pattern = $@"(?<!\w):{Regex.Escape(name)}(?!\w)";

            // Regex.Replace 의 치환 문자열에서 '$' 는 특수 의미(그룹 참조)를 가지므로,
            // 사용자가 입력한 값에 '$' 가 들어있어도 글자 그대로 들어가도록 이스케이프($$)한다.
            result = Regex.Replace(result, pattern, replacement.Replace("$", "$$"));
        }

        return result;
    }
}
