using System.Windows;
using System.Windows.Controls;

namespace HelperManager.Modules.Settings;

/// <summary>
/// SettingsView.xaml 의 코드비하인드.
///
/// 거의 모든 동작은 바인딩으로 처리되지만, DB 비밀번호 입력란만은 예외다.
/// WPF 의 PasswordBox 는 비밀번호가 의존 속성으로 메모리에 남는 것을 막기 위해 Password 속성에
/// 바인딩을 지원하지 않는다. 그래서 여기서 ViewModel 과 값을 직접 주고받는다.
/// </summary>
public partial class SettingsView : UserControl
{
    /// <summary>
    /// ViewModel 의 값을 입력란에 되돌려 넣는 동안 PasswordChanged 가 다시 돌아 ViewModel 을
    /// 덮어쓰는 것을 막기 위한 표시.
    /// </summary>
    private bool _isSyncingPassword;

    public SettingsView()
    {
        InitializeComponent();

        // 저장해둔 비밀번호를 화면이 열릴 때 입력란에 채워 넣는다.
        // (DataContext 는 생성자 시점에 아직 없을 수 있어 Loaded 에서 처리한다)
        Loaded += (_, _) =>
        {
            if (DataContext is SettingsViewModel viewModel && ExtractPasswordBox.Password != viewModel.ExtractPassword)
            {
                _isSyncingPassword = true;
                ExtractPasswordBox.Password = viewModel.ExtractPassword ?? string.Empty;
                _isSyncingPassword = false;
            }
        };
    }

    private void ExtractPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_isSyncingPassword)
        {
            return;
        }

        if (DataContext is SettingsViewModel viewModel)
        {
            viewModel.ExtractPassword = ExtractPasswordBox.Password;
        }
    }
}
