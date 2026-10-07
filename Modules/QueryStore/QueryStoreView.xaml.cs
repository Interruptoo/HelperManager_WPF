using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HelperManager.Modules.QueryStore.Models;

namespace HelperManager.Modules.QueryStore;

/// <summary>
/// QueryStoreView.xaml 의 코드비하인드.
/// TreeView 는 표준 WPF 컨트롤 중 SelectedItem/더블클릭을 바로 Command 로 바인딩할 방법이 없어서
/// (ListBox.SelectedItem 과 달리) 이벤트를 받아 ViewModel 에 직접 전달하는 최소한의 연결 코드만 둔다.
/// 그 외 모든 동작은 MVVM 바인딩으로 처리된다.
/// </summary>
public partial class QueryStoreView : UserControl
{
    public QueryStoreView()
    {
        InitializeComponent();
    }

    private void QueryTree_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is QueryStoreViewModel viewModel)
        {
            viewModel.SelectedNode = e.NewValue as QueryTreeNode;
        }
    }

    /// <summary>트리에서 쿼리(리프 노드)를 더블클릭하면 그 쿼리를 탭으로 연다. (폴더 노드는 기본 펼침/접힘 동작 그대로 둔다)</summary>
    private void QueryTree_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not QueryStoreViewModel viewModel)
        {
            return;
        }

        // 실제로 더블클릭된 TreeViewItem을 찾기 위해, 클릭 지점에서 비주얼 트리를 거슬러 올라간다.
        var element = e.OriginalSource as DependencyObject;
        while (element is not null and not TreeViewItem)
        {
            element = VisualTreeHelper.GetParent(element);
        }

        if (element is TreeViewItem { DataContext: QueryTreeNode { IsLeaf: true, Query: not null } node })
        {
            viewModel.OpenTab(node.Query);
            e.Handled = true;
        }
    }
}
