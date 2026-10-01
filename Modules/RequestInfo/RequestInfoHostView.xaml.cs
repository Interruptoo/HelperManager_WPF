using System.Windows.Controls;

namespace HelperManager.Modules.RequestInfo;

/// <summary>
/// RequestInfoHostView.xaml 의 코드비하인드.
/// 탭 추가/삭제/선택 등 모든 동작은 바인딩으로 처리되므로 별도 로직이 필요 없다.
/// </summary>
public partial class RequestInfoHostView : UserControl
{
    public RequestInfoHostView()
    {
        InitializeComponent();
    }
}
