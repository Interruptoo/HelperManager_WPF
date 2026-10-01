using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelperManager.Settings;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace HelperManager.Modules.RequestInfo;

/// <summary>
/// "Request LogViewer" 메뉴의 바깥 틀(Shell) ViewModel.
///
/// 로그가 쌓이는 경로가 한 곳이 아니라 여러 곳일 수 있으므로, 탭을 여러 개 띄워서 각 탭이
/// 서로 다른 폴더를 독립적으로 조회할 수 있게 한다(Tab1, Tab2, ... 처럼 필요한 만큼 추가).
/// 탭 하나하나는 그 자체로 완전한 <see cref="RequestInfoViewModel"/> 인스턴스이고,
/// 이 호스트는 그 탭들의 목록과 "현재 선택된 탭"만 관리한다.
///
/// 앱을 시작할 때는 Settings 화면에 미리 등록해둔 폴더 경로들을 읽어, 그 경로마다 탭을 하나씩
/// 자동으로 만들어 바로 로그를 볼 수 있게 한다. 등록된 경로가 하나도 없으면 빈 탭 하나를 기본으로 띄운다.
///
/// 이 호스트 자체는 DI 컨테이너에 Singleton 으로 등록되어 있어서(메뉴를 옮겼다가 돌아와도
/// 탭 구성이 그대로 유지된다), 반대로 탭 하나하나(RequestInfoViewModel)는 Transient 로 등록해서
/// 탭을 추가할 때마다 독립적인 새 인스턴스를 만든다.
/// </summary>
public sealed partial class RequestInfoHostViewModel : ObservableObject
{
    private readonly IServiceProvider _services;
    private int _tabSequence;

    public RequestInfoHostViewModel(IServiceProvider services, ISettingsService settingsService)
    {
        _services = services;

        AddTabCommand = new RelayCommand(AddTab);

        var savedFolderPaths = settingsService.Current.RequestLogViewerFolderPaths;
        if (savedFolderPaths.Count == 0)
        {
            AddTab(); // 등록해둔 경로가 없으면 바로 작업할 수 있도록 빈 탭 하나는 기본으로 만들어둔다.
        }
        else
        {
            foreach (var folderPath in savedFolderPaths)
            {
                AddTabForFolder(folderPath);
            }
        }
    }

    /// <summary>현재 열려 있는 탭 목록. 각 항목이 독립적인 RequestInfoViewModel 이다.</summary>
    public ObservableCollection<RequestInfoViewModel> Tabs { get; } = [];

    /// <summary>현재 선택된(화면에 보이는) 탭.</summary>
    [ObservableProperty]
    private RequestInfoViewModel? selectedTab;

    public IRelayCommand AddTabCommand { get; }

    /// <summary>"+ 새 탭" 버튼으로 빈 탭을 하나 만들어 목록 끝에 추가하고, 바로 선택 상태로 만든다.</summary>
    private void AddTab()
    {
        var tab = CreateTab();
        tab.Title = $"Tab {_tabSequence}";

        Tabs.Add(tab);
        SelectedTab = tab;
    }

    /// <summary>Settings 에 저장된 폴더 경로로 탭을 만들어 바로 그 폴더를 불러온다.</summary>
    private void AddTabForFolder(string folderPath)
    {
        var tab = CreateTab();

        Tabs.Add(tab);
        tab.LoadFolder(folderPath); // 탭 제목은 LoadFolder 내부에서 폴더 이름으로 자동 설정된다.

        SelectedTab ??= tab; // 맨 처음 등록된 경로의 탭을 기본으로 보여준다.
    }

    /// <summary>탭 하나(RequestInfoViewModel)를 DI 컨테이너에서 새로 만들고, 닫기 이벤트를 연결한다.</summary>
    private RequestInfoViewModel CreateTab()
    {
        _tabSequence++;

        var tab = _services.GetRequiredService<RequestInfoViewModel>();
        tab.CloseRequested += OnTabCloseRequested;
        return tab;
    }

    /// <summary>탭의 닫기(✕) 버튼이 눌렸을 때, 목록에서 제거하고 다른 탭을 대신 선택한다.</summary>
    private void OnTabCloseRequested(object? sender, EventArgs e)
    {
        if (sender is not RequestInfoViewModel tab)
        {
            return;
        }

        tab.CloseRequested -= OnTabCloseRequested;

        var closedIndex = Tabs.IndexOf(tab);
        if (closedIndex < 0)
        {
            return;
        }

        Tabs.RemoveAt(closedIndex);

        if (Tabs.Count == 0)
        {
            // 탭이 하나도 없는 빈 화면 대신, 항상 최소 한 개의 탭은 유지한다.
            AddTab();
            return;
        }

        if (ReferenceEquals(SelectedTab, tab))
        {
            var nextIndex = Math.Min(closedIndex, Tabs.Count - 1);
            SelectedTab = Tabs[nextIndex];
        }
    }
}
