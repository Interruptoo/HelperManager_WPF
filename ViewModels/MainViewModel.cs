using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelperManager.Modules;
using HelperManager.Settings;

namespace HelperManager.ViewModels;

/// <summary>
/// 메인 윈도우(Shell)의 ViewModel 입니다.
/// 좌측 메뉴 목록(Modules)과, 현재 선택된 메뉴에 해당하는 화면(CurrentContent)을 관리합니다.
///
/// 새로운 화면을 추가하려면:
///   1) Modules 폴더 아래에 IFeatureModule 을 구현한 클래스와 화면(View/ViewModel)을 추가하고
///   2) App.xaml.cs 에서 DI 컨테이너에 등록하기만 하면
/// 이 클래스는 전혀 수정하지 않아도 새 메뉴가 자동으로 나타납니다. (개방-폐쇄 원칙)
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    // 화면 전환 시 ViewModel 을 생성하기 위해 DI 컨테이너를 그대로 보관한다.
    private readonly IServiceProvider _services;

    // 기능키(F1~F12) 매핑은 Settings 화면에서 저장하는 즉시 반영되어야 하므로, 생성자에서 한 번
    // 읽어두지 않고 키를 누를 때마다 서비스에서 최신 설정을 꺼내 본다.
    private readonly ISettingsService _settingsService;

    /// <summary>메인 메뉴에 표시될 전체 화면(모듈) 목록. Order 값 기준으로 정렬되어 있다.</summary>
    public IReadOnlyList<IFeatureModule> Modules { get; }

    /// <summary>현재 좌측 메뉴에서 선택된 화면(모듈).</summary>
    [ObservableProperty]
    private IFeatureModule? selectedModule;

    /// <summary>
    /// 현재 화면 영역(ContentControl)에 표시되어야 하는 ViewModel 인스턴스.
    /// App.xaml 에 등록된 DataTemplate 을 통해 실제 View(XAML)로 자동 매핑된다.
    /// </summary>
    [ObservableProperty]
    private object? currentContent;

    /// <summary>
    /// 좌측 메뉴 사이드바를 펼친 상태인지 여부. 화면(특히 폭이 넓어야 하는 화면)을 더 넓게
    /// 보고 싶을 때 토글 버튼으로 가로 폭을 접었다 펼 수 있도록 하기 위한 상태값이다.
    /// </summary>
    [ObservableProperty]
    private bool isMenuExpanded = false;

    public IRelayCommand ToggleMenuCommand { get; }

    public MainViewModel(IServiceProvider services, IEnumerable<IFeatureModule> modules, ISettingsService settingsService)
    {
        _services = services;
        _settingsService = settingsService;

        ToggleMenuCommand = new RelayCommand(() => IsMenuExpanded = !IsMenuExpanded);

        // Order 값 오름차순으로 정렬해서 메뉴 표시 순서를 결정한다.
        //Modules = modules.OrderBy(module => module.Order).ToList();
        Modules = modules.ToList();

        // 앱 시작 시 첫 번째 메뉴를 기본으로 선택한다.
        SelectedModule = Modules.FirstOrDefault();
    }

    /// <summary>
    /// SelectedModule 이 바뀔 때마다(CommunityToolkit.Mvvm 소스 제너레이터가 자동 호출)
    /// 해당 모듈의 ViewModel 을 새로 생성해서 화면을 갱신한다.
    /// </summary>
    partial void OnSelectedModuleChanged(IFeatureModule? value)
    {
        CurrentContent = value?.CreateViewModel(_services);
    }

    /// <summary>
    /// 기능키(F1~F12)를 눌렀을 때, Settings 에 등록된 메뉴로 전환한다.
    /// 메뉴 순서가 아니라 키마다 지정한 메뉴를 따르므로, 메뉴가 늘어나도 단축키는 그대로다.
    /// </summary>
    /// <param name="number">기능키 번호(1~12). F1 이면 1.</param>
    /// <returns>
    /// 해당 키에 연결된 메뉴로 실제로 전환했으면 true. 지정되지 않았거나 그 이름의 메뉴를
    /// 찾지 못하면 false — 호출한 쪽에서 키 입력을 그대로 흘려보낼지 판단하는 데 쓴다.
    /// </returns>
    public bool SelectByFunctionKey(int number)
    {
        if (!_settingsService.Current.FunctionKeyMenus.TryGetValue($"F{number}", out var title))
        {
            return false;
        }

        var target = Modules.FirstOrDefault(module =>
            string.Equals(module.Title, title, StringComparison.Ordinal));

        if (target is null)
        {
            // 설정에 남아 있지만 지금은 없는 메뉴(이름이 바뀌었거나 제거된 경우).
            return false;
        }

        // 이미 그 화면이면 다시 만들 필요가 없다. (SelectedModule 재대입은 ViewModel 을 새로 만든다)
        if (ReferenceEquals(SelectedModule, target))
        {
            return true;
        }

        SelectedModule = target;
        return true;
    }
}
