using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HelperManager.Common;

/// <summary>
/// 패널을 "세로 Expander"가 아니라 "가로로" 접었다 펼 수 있도록, bool 값을 GridLength 로
/// 변환해주는 컨버터입니다. 메인 메뉴 사이드바, Query Store 의 파라미터/미리보기 패널 등
/// "토글 버튼으로 가로 폭을 접었다 펼치는" 모든 화면에서 공통으로 재사용한다.
///
/// ConverterParameter 형식:
///   - "220"      : true -> 220px, false -> 0px (완전히 숨김)
///   - "220,56"   : true -> 220px, false -> 56px (VS Code 사이드바처럼 아이콘만 보이는 레일 너비로 축소)
/// </summary>
public sealed class BoolToGridLengthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isExpanded = value is true;
        var (expandedWidth, collapsedWidth) = ParseWidths(parameter);
        return new GridLength(isExpanded ? expandedWidth : collapsedWidth);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("GridLength -> bool 역변환은 지원하지 않습니다.");

    private static (double Expanded, double Collapsed) ParseWidths(object parameter)
    {
        if (parameter is string text)
        {
            var parts = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 2
                && TryParse(parts[0], out var expanded)
                && TryParse(parts[1], out var collapsed))
            {
                return (expanded, collapsed);
            }

            if (parts.Length == 1 && TryParse(parts[0], out var expandedOnly))
            {
                return (expandedOnly, 0d); // 접힘 너비를 안 주면 기존처럼 완전히 숨긴다.
            }
        }

        return (220d, 0d);
    }

    private static bool TryParse(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
