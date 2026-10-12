using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace HelperManager.Common;

/// <summary>
/// 화면이 열릴 때 특정 입력란에 자동으로 커서를 놓아주기 위한 Attached Property 입니다.
///
/// 각 화면에서 제일 먼저 하는 일이 검색어 입력이라, 메뉴를 옮길 때마다 검색창을 마우스로 한 번
/// 클릭해야 하는 번거로움을 없애기 위한 것이다. (F1~F12 로 메뉴를 옮겼을 때도 마찬가지로 동작한다)
///
/// 사용법:
///   &lt;TextBox common:FocusOnLoadBehavior.IsEnabled="True" ... /&gt;
///
/// 메뉴를 옮기면 MainViewModel.CurrentContent 가 바뀌고, 그때마다 DataTemplate 이 화면(View)을
/// 새로 만들기 때문에 Loaded 가 다시 발생한다. 그래서 "메뉴를 선택할 때마다" 포커스가 잡힌다.
/// </summary>
public static class FocusOnLoadBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(FocusOnLoadBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        element.Loaded -= OnElementLoaded; // 중복 등록 방지
        if ((bool)e.NewValue)
        {
            element.Loaded += OnElementLoaded;
        }
    }

    private static void OnElementLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        // Loaded 시점에는 아직 화면 배치가 끝나지 않아 포커스가 먹지 않는 경우가 있다.
        // 배치가 끝난 뒤(Input 우선순위)로 미뤄서 확실하게 커서를 잡는다.
        element.Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (!element.IsLoaded)
            {
                return;
            }

            element.Focus();
            Keyboard.Focus(element);

            // 검색어가 남아 있으면 이어서 고치기 쉽도록 커서를 글자 끝에 둔다.
            // (전체 선택이 아니라서, 기존 검색어에 단어를 덧붙이는 사용에도 방해되지 않는다)
            if (element is TextBox textBox)
            {
                textBox.CaretIndex = textBox.Text.Length;
            }
        });
    }
}
