using System.Windows;
using System.Windows.Controls;

namespace HelperManager.Common;

/// <summary>
/// <see cref="JsonHighlightBehavior"/> 와 같은 목적으로, Query Store 화면의 RichTextBox에
/// ViewModel의 일반 문자열 속성(쿼리 원본 텍스트)을 바인딩하면서 SQL 색상 강조를 적용해주는
/// Attached Property 입니다.
///
/// 사용 예)
///   &lt;RichTextBox IsReadOnly="True"
///                 common:SqlHighlightBehavior.SqlText="{Binding DetailText}" /&gt;
/// </summary>
public static class SqlHighlightBehavior
{
    public static readonly DependencyProperty SqlTextProperty =
        DependencyProperty.RegisterAttached(
            "SqlText",
            typeof(string),
            typeof(SqlHighlightBehavior),
            new PropertyMetadata(string.Empty, OnSqlTextChanged));

    public static string GetSqlText(DependencyObject obj) => (string)obj.GetValue(SqlTextProperty);

    public static void SetSqlText(DependencyObject obj, string value) => obj.SetValue(SqlTextProperty, value);

    /// <summary>바인딩된 문자열이 바뀔 때마다 RichTextBox 의 Document 를 새로 그려준다.</summary>
    private static void OnSqlTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RichTextBox richTextBox)
        {
            return;
        }

        var text = e.NewValue as string ?? string.Empty;
        richTextBox.Document = SqlSyntaxHighlighter.Build(text);
    }
}
