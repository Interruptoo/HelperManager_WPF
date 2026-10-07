using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelperManager.Modules;

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

    public MainViewModel(IServiceProvider services, IEnumerable<IFeatureModule> modules)
    {
        _services = services;

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
}
