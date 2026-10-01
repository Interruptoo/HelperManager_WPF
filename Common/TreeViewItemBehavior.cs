using System.Windows;
using System.Windows.Controls;

namespace HelperManager.Common;

/// <summary>
/// 검색으로 트리의 특정 노드를 찾아 선택 상태(IsSelected)로 만들었을 때, 그 노드가 화면 밖에
/// 있으면 자동으로 스크롤해서 보여주기 위한 Attached Property 입니다.
/// TreeViewItem.IsSelected 가 (마우스 클릭이든, ViewModel 바인딩이든) true 로 바뀌는 순간
/// BringIntoView() 를 호출해 사용자가 "찾은 쿼리"를 바로 눈으로 확인할 수 있게 해준다.
/// </summary>
public static class TreeViewItemBehavior
{
    public static readonly DependencyProperty AutoBringIntoViewProperty =
        DependencyProperty.RegisterAttached(
            "AutoBringIntoView",
            typeof(bool),
            typeof(TreeViewItemBehavior),
            new PropertyMetadata(false, OnAutoBringIntoViewChanged));

    public static bool GetAutoBringIntoView(DependencyObject obj) => (bool)obj.GetValue(AutoBringIntoViewProperty);

    public static void SetAutoBringIntoView(DependencyObject obj, bool value) => obj.SetValue(AutoBringIntoViewProperty, value);

    private static void OnAutoBringIntoViewChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TreeViewItem item)
        {
            return;
        }

        item.Selected -= OnItemSelected; // 중복 등록 방지
        if ((bool)e.NewValue)
        {
            item.Selected += OnItemSelected;
        }
    }

    private static void OnItemSelected(object sender, RoutedEventArgs e)
    {
        // Selected 는 버블링 라우티드 이벤트라서 자식 TreeViewItem의 선택 이벤트가 부모까지 올라온다.
        // OriginalSource 가 바로 이 아이템 자신일 때만(= 이 아이템이 실제로 선택됐을 때만) 스크롤한다.
        if (sender is TreeViewItem item && ReferenceEquals(e.OriginalSource, item))
        {
            item.BringIntoView();
        }
    }
}
