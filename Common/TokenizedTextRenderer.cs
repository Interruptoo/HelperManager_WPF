using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace HelperManager.Common;

/// <summary>
/// 정규식으로 찾은 토큰에 색상(및 굵게 표시 여부)을 입혀 RichTextBox용 FlowDocument로 변환하는
/// 공용 렌더러입니다. JSON 하이라이터와 SQL 하이라이터가 이 로직을 공유한다.
/// 외부 라이브러리(AvalonEdit 등) 없이 순수 WPF + 정규식만으로 가볍게 구현했습니다.
/// </summary>
internal static class TokenizedTextRenderer
{
    /// <summary>
    /// 텍스트 전체를 <paramref name="tokenPattern"/> 으로 매칭하고, 매칭된 토큰마다
    /// <paramref name="resolveStyle"/> 이 돌려주는 색/굵기를 적용해 FlowDocument 를 만든다.
    /// 매칭되지 않은 나머지 텍스트(공백, 구분자 등)는 기본 색(검정)으로 그대로 표시한다.
    /// </summary>
    public static FlowDocument Build(string text, Regex tokenPattern, Func<Match, (Brush? Brush, bool Bold)> resolveStyle)
    {
        var document = new FlowDocument
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            PagePadding = new Thickness(4),
        };

        var paragraph = new Paragraph { Margin = new Thickness(0) };

        var lastIndex = 0;
        foreach (Match match in tokenPattern.Matches(text))
        {
            // 이전 매칭 끝과 현재 매칭 시작 사이에 있는 일반 텍스트(공백, 줄바꿈 등)를 그대로 추가한다.
            if (match.Index > lastIndex)
            {
                AppendText(paragraph, text[lastIndex..match.Index], brush: null, bold: false);
            }

            var (brush, bold) = resolveStyle(match);
            AppendText(paragraph, match.Value, brush, bold);

            lastIndex = match.Index + match.Length;
        }

        // 마지막 매칭 이후 남은 텍스트를 추가한다.
        if (lastIndex < text.Length)
        {
            AppendText(paragraph, text[lastIndex..], brush: null, bold: false);
        }

        document.Blocks.Add(paragraph);
        return document;
    }

    /// <summary>
    /// RichTextBox(FlowDocument)의 Run 은 Text 안에 포함된 개행문자를 화면 줄바꿈으로 그려주지 않는다.
    /// 여러 줄로 된 원본 텍스트(스택 트레이스, SQL 본문 등)를 그대로 보여줄 때 모든 내용이 한 줄로
    /// 뭉쳐 보이는 문제가 생기므로, 개행 문자마다 명시적으로 LineBreak 를 삽입한다.
    /// </summary>
    private static void AppendText(Paragraph paragraph, string text, Brush? brush, bool bold)
    {
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                paragraph.Inlines.Add(new LineBreak());
            }

            // "\r\n" 개행을 쓰는 파일도 있으므로 남은 \r 은 잘라낸다.
            var line = lines[i].EndsWith('\r') ? lines[i][..^1] : lines[i];
            if (line.Length == 0)
            {
                continue;
            }

            var run = new Run(line);
            if (brush is not null)
            {
                run.Foreground = brush;
            }

            if (bold)
            {
                run.FontWeight = FontWeights.Bold;
            }

            paragraph.Inlines.Add(run);
        }
    }

    /// <summary>여러 Run 에서 재사용할 브러시를 Freeze 해서 돌려준다. (성능/스레드 안전성 목적)</summary>
    public static SolidColorBrush FreezeBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
