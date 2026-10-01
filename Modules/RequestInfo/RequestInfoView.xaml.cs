using System.Windows.Controls;

namespace HelperManager.Modules.RequestInfo;

/// <summary>
/// RequestInfoView.xaml 의 코드비하인드.
/// 이 화면은 순수 MVVM 바인딩만으로 동작하므로 별도 로직이 필요 없다.
/// (DataContext 는 App.xaml 에 등록된 DataTemplate 을 통해 RequestInfoViewModel 이 자동으로 연결된다.)
/// </summary>
public partial class RequestInfoView : UserControl
{
    public RequestInfoView()
    {
        InitializeComponent();
    }
}
