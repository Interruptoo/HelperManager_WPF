using System.Text.RegularExpressions;
using System.Windows.Documents;
using System.Windows.Media;

namespace HelperManager.Common;

/// <summary>
/// 들여쓰기된 JSON 문자열을 색깔이 입혀진 RichTextBox용 FlowDocument로 변환합니다.
/// key(필드명), 문자열 값, 숫자, true/false/null, 괄호류를 서로 다른 색으로 표시해서
/// 로그 body를 한눈에 훑어보기 쉽게 만드는 것이 목적입니다.
/// 실제 렌더링(개행 처리 등)은 <see cref="TokenizedTextRenderer"/> 가 공통으로 담당한다.
/// </summary>
public static partial class JsonSyntaxHighlighter
{
    // 정규식 하나로 JSON 토큰들을 순서대로 매칭한다. 그룹 이름으로 토큰 종류를 구분한다.
    // - key       : "필드명" 뒤에 콜론(:)이 오는 경우 (lookahead 로 콜론 자체는 소비하지 않음)
    // - string    : 그 외의 순수 문자열 값
    // - number    : 정수/실수/지수 표기
    // - bool      : true / false
    // - null      : null
    // - punct     : { } [ ] , :  같은 구두점
    [GeneratedRegex(
        """(?<key>"(?:\\.|[^"\\])*")\s*(?=:)|(?<string>"(?:\\.|[^"\\])*")|(?<number>-?\d+(\.\d+)?([eE][+-]?\d+)?)|(?<bool>\btrue\b|\bfalse\b)|(?<null>\bnull\b)|(?<punct>[{}\[\],:])""")]
    private static partial Regex TokenPattern();

    private static readonly Brush KeyBrush = TokenizedTextRenderer.FreezeBrush(0x0B, 0x61, 0xA4);      // 필드명 - 파란색 계열
    private static readonly Brush StringBrush = TokenizedTextRenderer.FreezeBrush(0x0A, 0x82, 0x0A);   // 문자열 값 - 초록색 계열
    private static readonly Brush NumberBrush = TokenizedTextRenderer.FreezeBrush(0xB5, 0x56, 0x00);   // 숫자 - 주황색 계열
    private static readonly Brush KeywordBrush = TokenizedTextRenderer.FreezeBrush(0x79, 0x3D, 0xC7);  // true/false/null - 보라색 계열
    private static readonly Brush PunctBrush = Brushes.DimGray;                                        // 괄호/콤마 - 회색

    /// <summary>주어진 텍스트를 토큰별로 색칠한 FlowDocument 를 생성한다.</summary>
    public static FlowDocument Build(string text) => TokenizedTextRenderer.Build(text, TokenPattern(), ResolveStyle);

    /// <summary>정규식 명명 그룹(key/string/number/bool/null/punct) 기준으로 색상을 결정한다.</summary>
    private static (Brush? Brush, bool Bold) ResolveStyle(Match match) => match switch
    {
        _ when match.Groups["key"].Success => (KeyBrush, false),
        _ when match.Groups["string"].Success => (StringBrush, false),
        _ when match.Groups["number"].Success => (NumberBrush, false),
        _ when match.Groups["bool"].Success => (KeywordBrush, false),
        _ when match.Groups["null"].Success => (KeywordBrush, false),
        _ => (PunctBrush, false),
    };
}
