using System.Windows;
using HelperManager.Modules;
using HelperManager.Modules.QueryStore;
using HelperManager.Modules.QueryStore.Services;
using HelperManager.Modules.RequestInfo;
using HelperManager.Modules.RequestInfo.Services;
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

        // ----- 메인 셸(Shell) -----
        services.AddSingleton<MainViewModel>();

        // ----- 화면(모듈) 등록 영역 -----
        // RequestInfo: 백엔드 요청/응답 JSON 로그 뷰어 화면
        services.AddSingleton<RequestInfoViewModel>();
        services.AddSingleton<IFeatureModule, RequestInfoModule>();

        // Query Store: XML로 추출된 쿼리 모음을 트리로 보여주는 화면
        services.AddSingleton<QueryStoreViewModel>();
        services.AddSingleton<IFeatureModule, QueryStoreModule>();
    }
}
