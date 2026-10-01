using System.Windows;
using System.Windows.Controls;

namespace HelperManager.Common;

/// <summary>
/// RichTextBox는 표준 WPF 컨트롤 중 Text 를 직접 바인딩할 수 없는 특이한 컨트롤입니다.
/// (내부적으로 FlowDocument 를 사용하기 때문입니다.)
/// 이 Attached Property 는 ViewModel의 일반 문자열 속성을 그대로 바인딩할 수 있게 해주면서,
/// 내부적으로는 JsonSyntaxHighlighter 를 이용해 색깔이 입혀진 FlowDocument로 변환해줍니다.
/// 덕분에 View의 코드비하인드를 건드리지 않고 순수 XAML 바인딩만으로 MVVM 을 유지할 수 있습니다.
///
/// 사용 예)
///   &lt;RichTextBox IsReadOnly="True"
///                 common:JsonHighlightBehavior.JsonText="{Binding SelectedEntryPrettyJson}" /&gt;
/// </summary>
public static class JsonHighlightBehavior
{
    public static readonly DependencyProperty JsonTextProperty =
        DependencyProperty.RegisterAttached(
            "JsonText",
            typeof(string),
            typeof(JsonHighlightBehavior),
            new PropertyMetadata(string.Empty, OnJsonTextChanged));

    public static string GetJsonText(DependencyObject obj) => (string)obj.GetValue(JsonTextProperty);

    public static void SetJsonText(DependencyObject obj, string value) => obj.SetValue(JsonTextProperty, value);

    /// <summary>바인딩된 문자열이 바뀔 때마다 RichTextBox 의 Document 를 새로 그려준다.</summary>
    private static void OnJsonTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RichTextBox richTextBox)
        {
            return;
        }

        var text = e.NewValue as string ?? string.Empty;
        richTextBox.Document = JsonSyntaxHighlighter.Build(text);
    }
}
