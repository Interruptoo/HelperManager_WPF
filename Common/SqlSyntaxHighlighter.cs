using System.Text.RegularExpressions;
using System.Windows.Documents;
using System.Windows.Media;

namespace HelperManager.Common;

/// <summary>
/// Query Store 화면에서 보여주는 쿼리 원본 텍스트(예: &lt;sql id="..."&gt;&lt;!-- 주석 --&gt;INSERT INTO ... &lt;/sql&gt;)
/// 에 색깔을 입혀 RichTextBox용 FlowDocument로 변환합니다.
/// XML 태그/주석, SQL 키워드, 문자열, 바인드 변수(:NAME), 라인 주석(--)을 구분해서 표시한다.
/// 실제 렌더링(개행 처리 등)은 <see cref="TokenizedTextRenderer"/> 가 공통으로 담당한다.
/// </summary>
public static partial class SqlSyntaxHighlighter
{
    // 자주 쓰이는 Oracle/표준 SQL 키워드. 필요하면 얼마든지 추가할 수 있다.
    private const string KeywordAlternation =
        "INSERT|INTO|VALUES|SELECT|FROM|WHERE|AND|OR|UPDATE|SET|DELETE|DECODE|NVL|NVL2|" +
        "TO_DATE|TO_CHAR|TO_NUMBER|SYSDATE|NULL|IN|EXISTS|INNER|LEFT|RIGHT|OUTER|JOIN|" +
        "ORDER|BY|GROUP|HAVING|UNION|ALL|CASE|WHEN|THEN|ELSE|END|AS|LIKE|NOT|IS|DISTINCT|" +
        "COUNT|SUM|MAX|MIN|AVG|BEGIN|COMMIT|ROLLBACK|DECLARE|FUNCTION|PROCEDURE|PACKAGE|" +
        "CREATE|ALTER|DROP|TABLE|VIEW|INDEX|TRIGGER|REPLACE|MERGE|USING|ROWNUM|DUAL";

    // 그룹 순서(= 매칭 우선순위)가 중요하다: 블록 주석/태그/문자열을 먼저 떼어내야
    // 그 내부에 있는 글자들이 키워드/구두점 등으로 잘못 다시 매칭되지 않는다.
    // - comment  : <!-- ... --> 블록 주석 (줄바꿈 포함 가능)
    // - tag      : <sql id="..."> , </sql> 같은 XML 태그
    // - string   : 'abc''def' 형태의 SQL 문자열 리터럴 ('' 는 이스케이프된 작은따옴표)
    // - bindvar  : :IN_DTE 같은 바인드 변수
    // - linecmt  : -- 로 시작해서 줄 끝까지 이어지는 한 줄 주석
    // - number   : 정수/실수
    // - keyword  : SQL 예약어
    // - punct    : ( ) , . ; * = < >  같은 구두점
    [GeneratedRegex(
        $"""(?<comment><!--[\s\S]*?-->)|(?<tag></?[A-Za-z][^>]*>)|(?<string>'(?:[^']|'')*')|(?<bindvar>:[A-Za-z_][A-Za-z0-9_]*)|(?<linecmt>--[^\r\n]*)|(?<number>\b\d+(\.\d+)?\b)|(?<keyword>\b(?:{KeywordAlternation})\b)|(?<punct>[(),.;*=<>])""",
        RegexOptions.IgnoreCase)]
    private static partial Regex TokenPattern();

    private static readonly Brush CommentBrush = TokenizedTextRenderer.FreezeBrush(0x6A, 0x99, 0x55);   // 주석 - 초록색 계열
    private static readonly Brush TagBrush = TokenizedTextRenderer.FreezeBrush(0x80, 0x00, 0x80);        // XML 태그 - 보라색 계열
    private static readonly Brush StringBrush = TokenizedTextRenderer.FreezeBrush(0xA3, 0x15, 0x15);     // 문자열 - 붉은 갈색 계열
    private static readonly Brush BindVarBrush = TokenizedTextRenderer.FreezeBrush(0xB5, 0x56, 0x00);    // 바인드 변수 - 주황색 계열
    private static readonly Brush KeywordBrush = TokenizedTextRenderer.FreezeBrush(0x0B, 0x61, 0xA4);    // 키워드 - 파란색 계열
    private static readonly Brush NumberBrush = TokenizedTextRenderer.FreezeBrush(0x79, 0x3D, 0xC7);     // 숫자 - 보라색 계열
    private static readonly Brush PunctBrush = Brushes.DimGray;                                          // 구두점 - 회색

    /// <summary>주어진 쿼리 원본 텍스트를 토큰별로 색칠한 FlowDocument 를 생성한다.</summary>
    public static FlowDocument Build(string text) => TokenizedTextRenderer.Build(text, TokenPattern(), ResolveStyle);

    /// <summary>정규식 명명 그룹 기준으로 색상과 굵게 여부를 결정한다. (키워드는 굵게 표시)</summary>
    private static (Brush? Brush, bool Bold) ResolveStyle(Match match) => match switch
    {
        _ when match.Groups["comment"].Success => (CommentBrush, false),
        _ when match.Groups["tag"].Success => (TagBrush, false),
        _ when match.Groups["string"].Success => (StringBrush, false),
        _ when match.Groups["bindvar"].Success => (BindVarBrush, false),
        _ when match.Groups["linecmt"].Success => (CommentBrush, false),
        _ when match.Groups["number"].Success => (NumberBrush, false),
        _ when match.Groups["keyword"].Success => (KeywordBrush, true),
        _ => (PunctBrush, false),
    };
}
