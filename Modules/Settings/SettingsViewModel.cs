using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelperManager.Settings;
using Microsoft.Win32;

namespace HelperManager.Modules.Settings;

/// <summary>
/// Settings 화면의 ViewModel.
/// 매번 폴더/파일을 다시 찾는 수고를 덜기 위해, 자주 쓰는 경로를 미리 등록해두는 화면이다.
///   - Request LogViewer : 앱을 시작할 때 탭으로 자동으로 열어줄 로그 폴더 목록 (여러 개 가능)
///   - Query Store        : 앱을 시작할 때 자동으로 읽어줄 쿼리 모음 XML 파일 하나
///   - MenuInfo            : 앱을 시작할 때 자동으로 읽어줄 메뉴 정보 JSON 파일 하나
///   - Common Code         : 앱을 시작할 때 자동으로 읽어줄 ComnCdInfo.json / ComnCdDetail.json 파일
///   - Table Info          : 앱을 시작할 때 자동으로 읽어줄 테이블/컬럼/인덱스/사용오브젝트 JSON 파일
/// [저장]을 누르면 JSON 설정 파일에 기록되고, 다음 실행부터 반영된다.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;

        RequestLogViewerFolderPaths = new ObservableCollection<string>(settingsService.Current.RequestLogViewerFolderPaths);
        queryStoreXmlPath = settingsService.Current.QueryStoreXmlPath;
        menuInfoJsonPath = settingsService.Current.MenuInfoJsonPath;
        comnCdInfoPath = settingsService.Current.ComnCdInfoPath;
        comnCdDetailPath = settingsService.Current.ComnCdDetailPath;
        tableInfoJsonPath = settingsService.Current.TableInfoJsonPath;
        columnInfoJsonPath = settingsService.Current.ColumnInfoJsonPath;
        indexInfoJsonPath = settingsService.Current.IndexInfoJsonPath;
        tableUseObjectJsonPath = settingsService.Current.TableUseObjectJsonPath;

        AddLogFolderCommand = new RelayCommand(AddLogFolder);
        RemoveLogFolderCommand = new RelayCommand<string>(RemoveLogFolder);
        BrowseQueryStoreXmlCommand = new RelayCommand(BrowseQueryStoreXml);
        ClearQueryStoreXmlCommand = new RelayCommand(() => QueryStoreXmlPath = null);
        BrowseMenuInfoJsonCommand = new RelayCommand(BrowseMenuInfoJson);
        ClearMenuInfoJsonCommand = new RelayCommand(() => MenuInfoJsonPath = null);
        BrowseComnCdInfoCommand = new RelayCommand(BrowseComnCdInfo);
        ClearComnCdInfoCommand = new RelayCommand(() => ComnCdInfoPath = null);
        BrowseComnCdDetailCommand = new RelayCommand(BrowseComnCdDetail);
        ClearComnCdDetailCommand = new RelayCommand(() => ComnCdDetailPath = null);
        BrowseTableInfoJsonCommand = new RelayCommand(() => BrowseJson("Table Info가 시작 시 자동으로 불러올 테이블 목록 JSON 파일을 선택하세요", path => TableInfoJsonPath = path));
        ClearTableInfoJsonCommand = new RelayCommand(() => TableInfoJsonPath = null);
        BrowseColumnInfoJsonCommand = new RelayCommand(() => BrowseJson("Table Info가 시작 시 자동으로 불러올 컬럼 목록 JSON 파일을 선택하세요", path => ColumnInfoJsonPath = path));
        ClearColumnInfoJsonCommand = new RelayCommand(() => ColumnInfoJsonPath = null);
        BrowseIndexInfoJsonCommand = new RelayCommand(() => BrowseJson("Table Info가 시작 시 자동으로 불러올 인덱스 목록 JSON 파일을 선택하세요", path => IndexInfoJsonPath = path));
        ClearIndexInfoJsonCommand = new RelayCommand(() => IndexInfoJsonPath = null);
        BrowseTableUseObjectJsonCommand = new RelayCommand(() => BrowseJson("Table Info가 시작 시 자동으로 불러올 사용 오브젝트 목록 JSON 파일을 선택하세요", path => TableUseObjectJsonPath = path));
        ClearTableUseObjectJsonCommand = new RelayCommand(() => TableUseObjectJsonPath = null);
        SaveCommand = new RelayCommand(SaveSettings);
    }

    /// <summary>앱 시작 시 탭으로 자동으로 열어줄 로그 폴더 목록.</summary>
    public ObservableCollection<string> RequestLogViewerFolderPaths { get; }

    /// <summary>앱 시작 시 자동으로 읽어줄 쿼리 모음 XML 파일 경로.</summary>
    [ObservableProperty]
    private string? queryStoreXmlPath;

    /// <summary>앱 시작 시 자동으로 읽어줄 메뉴 정보 JSON 파일 경로.</summary>
    [ObservableProperty]
    private string? menuInfoJsonPath;

    /// <summary>앱 시작 시 자동으로 읽어줄 ComnCdInfo.json 파일 경로.</summary>
    [ObservableProperty]
    private string? comnCdInfoPath;

    /// <summary>앱 시작 시 자동으로 읽어줄 ComnCdDetail.json 파일 경로.</summary>
    [ObservableProperty]
    private string? comnCdDetailPath;

    /// <summary>앱 시작 시 자동으로 읽어줄 테이블 목록 JSON 파일 경로.</summary>
    [ObservableProperty]
    private string? tableInfoJsonPath;

    /// <summary>앱 시작 시 자동으로 읽어줄 컬럼 목록 JSON 파일 경로.</summary>
    [ObservableProperty]
    private string? columnInfoJsonPath;

    /// <summary>앱 시작 시 자동으로 읽어줄 인덱스 목록 JSON 파일 경로.</summary>
    [ObservableProperty]
    private string? indexInfoJsonPath;

    /// <summary>앱 시작 시 자동으로 읽어줄 사용 오브젝트 목록 JSON 파일 경로.</summary>
    [ObservableProperty]
    private string? tableUseObjectJsonPath;

    /// <summary>저장 결과 등을 알려주는 안내 메시지.</summary>
    [ObservableProperty]
    private string statusMessage = string.Empty;

    public IRelayCommand AddLogFolderCommand { get; }

    public IRelayCommand<string> RemoveLogFolderCommand { get; }

    public IRelayCommand BrowseQueryStoreXmlCommand { get; }

    public IRelayCommand ClearQueryStoreXmlCommand { get; }

    public IRelayCommand BrowseMenuInfoJsonCommand { get; }

    public IRelayCommand ClearMenuInfoJsonCommand { get; }

    public IRelayCommand BrowseComnCdInfoCommand { get; }

    public IRelayCommand ClearComnCdInfoCommand { get; }

    public IRelayCommand BrowseComnCdDetailCommand { get; }

    public IRelayCommand ClearComnCdDetailCommand { get; }

    public IRelayCommand BrowseTableInfoJsonCommand { get; }

    public IRelayCommand ClearTableInfoJsonCommand { get; }

    public IRelayCommand BrowseColumnInfoJsonCommand { get; }

    public IRelayCommand ClearColumnInfoJsonCommand { get; }

    public IRelayCommand BrowseIndexInfoJsonCommand { get; }

    public IRelayCommand ClearIndexInfoJsonCommand { get; }

    public IRelayCommand BrowseTableUseObjectJsonCommand { get; }

    public IRelayCommand ClearTableUseObjectJsonCommand { get; }

    public IRelayCommand SaveCommand { get; }

    private void AddLogFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Request LogViewer가 시작 시 자동으로 열 로그 폴더를 선택하세요",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (!RequestLogViewerFolderPaths.Contains(dialog.FolderName, StringComparer.OrdinalIgnoreCase))
        {
            RequestLogViewerFolderPaths.Add(dialog.FolderName);
        }
    }

    private void RemoveLogFolder(string? path)
    {
        if (path is not null)
        {
            RequestLogViewerFolderPaths.Remove(path);
        }
    }

    private void BrowseQueryStoreXml()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Query Store가 시작 시 자동으로 불러올 쿼리 모음 XML 파일을 선택하세요",
            Filter = "XML 파일 (*.xml)|*.xml|모든 파일 (*.*)|*.*",
        };

        if (dialog.ShowDialog() == true)
        {
            QueryStoreXmlPath = dialog.FileName;
        }
    }

    private void BrowseMenuInfoJson()
    {
        var dialog = new OpenFileDialog
        {
            Title = "MenuInfo가 시작 시 자동으로 불러올 메뉴 정보 JSON 파일을 선택하세요",
            Filter = "JSON 파일 (*.json)|*.json|모든 파일 (*.*)|*.*",
        };

        if (dialog.ShowDialog() == true)
        {
            MenuInfoJsonPath = dialog.FileName;
        }
    }

    private void BrowseComnCdInfo()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Common Code가 시작 시 자동으로 불러올 ComnCdInfo.json 파일을 선택하세요",
            Filter = "JSON 파일 (*.json)|*.json|모든 파일 (*.*)|*.*",
        };

        if (dialog.ShowDialog() == true)
        {
            ComnCdInfoPath = dialog.FileName;
        }
    }

    private void BrowseComnCdDetail()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Common Code가 시작 시 자동으로 불러올 ComnCdDetail.json 파일을 선택하세요",
            Filter = "JSON 파일 (*.json)|*.json|모든 파일 (*.*)|*.*",
        };

        if (dialog.ShowDialog() == true)
        {
            ComnCdDetailPath = dialog.FileName;
        }
    }

    /// <summary>
    /// JSON 파일 하나를 고르는 공용 대화상자. Table Info 처럼 등록할 파일이 여러 개인 경우
    /// 같은 코드를 반복하지 않도록, 제목과 "고른 경로를 어디에 넣을지"만 받아서 처리한다.
    /// </summary>
    private static void BrowseJson(string title, Action<string> assignPath)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = "JSON 파일 (*.json)|*.json|모든 파일 (*.*)|*.*",
        };

        if (dialog.ShowDialog() == true)
        {
            assignPath(dialog.FileName);
        }
    }

    private void SaveSettings()
    {
        var settings = new AppSettings
        {
            RequestLogViewerFolderPaths = RequestLogViewerFolderPaths.ToList(),
            QueryStoreXmlPath = string.IsNullOrWhiteSpace(QueryStoreXmlPath) ? null : QueryStoreXmlPath,
            MenuInfoJsonPath = string.IsNullOrWhiteSpace(MenuInfoJsonPath) ? null : MenuInfoJsonPath,
            ComnCdInfoPath = string.IsNullOrWhiteSpace(ComnCdInfoPath) ? null : ComnCdInfoPath,
            ComnCdDetailPath = string.IsNullOrWhiteSpace(ComnCdDetailPath) ? null : ComnCdDetailPath,
            TableInfoJsonPath = string.IsNullOrWhiteSpace(TableInfoJsonPath) ? null : TableInfoJsonPath,
            ColumnInfoJsonPath = string.IsNullOrWhiteSpace(ColumnInfoJsonPath) ? null : ColumnInfoJsonPath,
            IndexInfoJsonPath = string.IsNullOrWhiteSpace(IndexInfoJsonPath) ? null : IndexInfoJsonPath,
            TableUseObjectJsonPath = string.IsNullOrWhiteSpace(TableUseObjectJsonPath) ? null : TableUseObjectJsonPath,
        };

        _settingsService.Save(settings);

        StatusMessage = "설정을 저장했습니다. 다음 실행부터 적용됩니다.";
    }
}
