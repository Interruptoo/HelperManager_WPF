using System.Windows;
using System.Windows.Input;
using HelperManager.ViewModels;

namespace HelperManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// F1~F12 로 메뉴를 바로 전환한다. 어떤 키가 어떤 메뉴인지는 Settings 화면에서 지정한다.
        ///
        /// KeyDown 이 아니라 PreviewKeyDown(터널링)에서 처리하는 이유: 포커스가 텍스트 상자나
        /// DataGrid 안에 있어도(예: DataGrid 는 F2 를 편집 시작으로 쓴다) 단축키가 먼저 동작해야
        /// 하기 때문이다. 창이 자식 컨트롤보다 먼저 키를 보게 된다.
        /// </summary>
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // F10 과 Alt 조합은 WPF 가 "메뉴 활성화" 용 시스템 키로 바꿔서 전달하므로,
            // 그런 경우에는 e.Key 가 아니라 e.SystemKey 에 실제 눌린 키가 들어 있다.
            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            if (key < Key.F1 || key > Key.F12)
            {
                return;
            }

            // Ctrl/Alt/Shift 를 함께 누른 경우는 다른 용도일 수 있으므로 건드리지 않는다.
            if (Keyboard.Modifiers != ModifierKeys.None)
            {
                return;
            }

            if (DataContext is MainViewModel viewModel && viewModel.SelectByFunctionKey(key - Key.F1 + 1))
            {
                // 지정된 메뉴로 전환했을 때만 키를 삼킨다. 지정하지 않은 키는 원래 동작
                // (F1 도움말 등)을 막지 않도록 그대로 흘려보낸다.
                e.Handled = true;
            }
        }
    }
}
