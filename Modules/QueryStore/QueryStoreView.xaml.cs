using System.Windows;
using System.Windows.Controls;
using HelperManager.Modules.QueryStore.Models;

namespace HelperManager.Modules.QueryStore;

/// <summary>
/// QueryStoreView.xaml 의 코드비하인드.
/// TreeView.SelectedItem 은 표준 WPF TreeView 에 바인딩 가능한 의존 속성이 없기 때문에
/// (ListBox.SelectedItem 과 달리) SelectedItemChanged 이벤트를 받아 ViewModel 에 직접 전달하는
/// 최소한의 연결 코드만 둔다. 그 외 모든 동작은 MVVM 바인딩으로 처리된다.
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
}
