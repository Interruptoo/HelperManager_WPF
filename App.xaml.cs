using System.Windows;
using HelperManager.Common;
using HelperManager.Modules;
using HelperManager.Modules.ComnCode;
using HelperManager.Modules.ComnCode.Services;
using HelperManager.Modules.MenuInfo;
using HelperManager.Modules.MenuInfo.Services;
using HelperManager.Modules.QueryStore;
using HelperManager.Modules.QueryStore.Services;
using HelperManager.Modules.RequestInfo;
using HelperManager.Modules.RequestInfo.Services;
using HelperManager.Modules.Settings;
using HelperManager.Modules.TableInfo;
using HelperManager.Modules.TableInfo.Services;
using HelperManager.Settings;
using HelperManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace HelperManager;

/// <summary>
/// 애플리케이션 진입점(App.xaml 의 코드비하인드)입니다.
/// 여기서 DI(의존성 주입) 컨테이너를 구성하고, MainWindow 를 직접 생성해서 띄웁니다.
/// (App.xaml 의 StartupUri 방식 대신 직접 생성하는 이유는, MainWindow 의 DataContext 로
///  DI 컨테이너에서 만든 MainViewModel 을 주입해주기 위함입니다.)
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// 앱 전역에서 사용하는 DI 컨테이너입니다.
    /// 화면(모듈)이 늘어나도 이 컨테이너 하나로 모든 서비스/ViewModel 의 생명주기를 관리합니다.
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        Services = serviceCollection.BuildServiceProvider();

        var mainWindow = new MainWindow
        {
            DataContext = Services.GetRequiredService<MainViewModel>(),
        };
        mainWindow.Show();
    }

    /// <summary>
    /// DI 컨테이너에 서비스와 화면(모듈)을 등록하는 곳입니다.
    ///
    /// ★ 새로운 화면을 추가하는 방법 ★
    ///   1. Modules/새화면이름 폴더를 만들고 View/ViewModel/Module 클래스를 작성한다.
    ///   2. 아래에 services.AddSingleton&lt;새ViewModel&gt;() 과
    ///      services.AddSingleton&lt;IFeatureModule, 새Module&gt;() 두 줄을 추가한다.
    ///   3. App.xaml 에 DataTemplate 매핑을 한 줄 추가한다.
    ///   -> MainViewModel/MainWindow 는 전혀 수정하지 않아도 메뉴에 새 화면이 자동으로 나타난다.
    ///
    ///   ※ 화면 ViewModel 을 Singleton 으로 등록하는 이유: 메뉴를 다른 탭으로 옮겼다가 다시 돌아와도
    ///   (MainViewModel.CreateViewModel 이 매번 다시 호출되므로) 불러온 파일/검색어/트리 선택 상태 등이
    ///   초기화되지 않고 그대로 유지되도록 하기 위함이다. Transient 로 등록하면 탭을 옮길 때마다
    ///   ViewModel 이 새로 생성되어 입력했던 내용이 전부 사라진다.
    /// </summary>
    private static void ConfigureServices(IServiceCollection services)
    {
        // ----- 공통 서비스 -----
        services.AddSingleton<ILogFileParserService, LogFileParserService>();
        services.AddSingleton<IQueryStoreParserService, QueryStoreParserService>();
        services.AddSingleton<ITableInfoParserService, TableInfoParserService>();
        services.AddSingleton<IComnCodeParserService, ComnCodeParserService>();
        services.AddSingleton<IMenuInfoParserService, MenuInfoParserService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        // 추출이 끝났을 때 각 화면이 JSON 을 다시 읽도록 알려주는 통로.
        // (각 화면에 [새로고침] 버튼을 두지 않기 때문에 필요하다)
        services.AddSingleton<IJsonDataRefreshNotifier, JsonDataRefreshNotifier>();

        // ----- 메인 셸(Shell) -----
        services.AddSingleton<MainViewModel>();

        // ----- 화면(모듈) 등록 영역 -----
        // RequestInfo: 백엔드 요청/응답 JSON 로그 뷰어 화면.
        // 로그 생성 경로가 여러 곳일 수 있어 탭(Tab1, Tab2, ...)으로 여러 개 띄울 수 있으므로,
        // 탭 하나하나(RequestInfoViewModel)는 탭을 추가할 때마다 새로 만들어야 해서 Transient 로,
        // 탭 목록을 들고 있는 호스트(RequestInfoHostViewModel)는 메뉴를 옮겨도 탭 구성이 유지되도록
        // Singleton 으로 등록한다.
        services.AddTransient<RequestInfoViewModel>();
        services.AddSingleton<RequestInfoHostViewModel>();
        services.AddSingleton<IFeatureModule, RequestInfoModule>();

        // Query Store: XML로 추출된 쿼리 모음을 트리로 보여주는 화면.
        // 트리/검색/파일 열기는 화면 전체에서 공용으로 하나만 쓰므로 QueryStoreViewModel 은
        // Singleton 으로 등록한다. (쿼리별 탭은 QueryTabViewModel 로, DI 등록 없이 QueryStoreViewModel
        // 이 쿼리를 열 때마다 직접 new 로 만든다 — 탭마다 다른 쿼리를 다루므로 DI로 캐시할 이유가 없다)
        services.AddSingleton<QueryStoreViewModel>();
        services.AddSingleton<IFeatureModule, QueryStoreModule>();

        // Table Info: 테이블 목록(좌) + 선택한 테이블의 컬럼/인덱스/사용 오브젝트(우)를 보여주는 화면.
        // 네 개의 JSON 파일을 한 번 읽어 조인용 Lookup 까지 만들어두므로, 메뉴를 옮겨도 다시 읽지
        // 않도록 Singleton 으로 등록한다.
        services.AddSingleton<TableInfoViewModel>();
        services.AddSingleton<IFeatureModule, TableInfoModule>();

        // Common Code: ComnCdInfo.json(좌) + ComnCdDetail.json(우, 선택한 그룹만 필터링)을 보여주는 화면
        services.AddSingleton<ComnCodeViewModel>();
        services.AddSingleton<IFeatureModule, ComnCodeModule>();

        // MenuInfo: 정기적으로 추출되는 메뉴 정보 JSON을 DataGrid로 보여주는 화면
        services.AddSingleton<MenuInfoViewModel>();
        services.AddSingleton<IFeatureModule, MenuInfoModule>();

        // Settings: 자주 쓰는 로그 폴더/쿼리 XML/메뉴·공통코드 JSON 경로를 미리 등록해두는 화면
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<IFeatureModule, SettingsModule>();
    }
}
