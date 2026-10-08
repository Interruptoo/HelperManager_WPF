using HelperManager.Common;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelperManager.Modules.MenuInfo.Models;
using HelperManager.Modules.MenuInfo.Services;
using HelperManager.Settings;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace HelperManager.Modules.MenuInfo;

/// <summary>
/// MenuInfo 화면(정기적으로 추출되는 메뉴 정보 JSON 뷰어)의 ViewModel.
///
/// 화면 동작 흐름:
///  1) [파일 열기] 버튼(또는 Settings에 미리 등록해둔 경로로 시작 시 자동 로드)으로 메뉴 정보
///     JSON 파일을 읽으면 -> 필요한 8개 필드(USE_YN/DISP_YN/MENU_NM/MENU_CD/ASSEMBLY_NM/
///     APP_URL/DEPTH1/DEPTH2)만 뽑아 DataGrid에 보여준다.
///  2) 필터 입력창에 값을 입력하면 -> MENU_NM / MENU_CD / APP_URL 셋 중 하나라도 부분 일치하면
///     (OR 조건) 그 행을 FilteredEntries 에 담는다. (조건 하나당 검사만 하므로 같은 행이 여러 번
///     나오는 일 없이 한 줄로만 나온다.)
/// </summary>
public sealed partial class MenuInfoViewModel : ObservableObject
{
    private readonly IMenuInfoParserService _parserService;

    public MenuInfoViewModel(IMenuInfoParserService parserService, ISettingsService settingsService, IJsonDataRefreshNotifier refreshNotifier)
    {
        _parserService = parserService;

        OpenFileCommand = new RelayCommand(OpenFile);
        RefreshCommand = new RelayCommand(Refresh, () => !string.IsNullOrWhiteSpace(LoadedFilePath));

        // Settings 화면에서 JSON 을 새로 추출하면 화면에 [새로고침] 버튼 없이도 알아서 다시 읽는다.
        refreshNotifier.Refreshed += Refresh;

        var savedJsonPath = settingsService.Current.MenuInfoJsonPath;
        if (!string.IsNullOrWhiteSpace(savedJsonPath) && File.Exists(savedJsonPath))
        {
            LoadFile(savedJsonPath);
        }
    }

    /// <summary>현재 불러온 JSON 파일 경로.</summary>
    [ObservableProperty]
    private string? loadedFilePath;

    /// <summary>파일에서 읽은 전체 메뉴 목록.</summary>
    [ObservableProperty]
    private IReadOnlyList<MenuInfoEntry> allEntries = [];

    /// <summary>필터가 적용된 후 DataGrid에 실제로 표시되는 목록.</summary>
    [ObservableProperty]
    private IReadOnlyList<MenuInfoEntry> filteredEntries = [];

    /// <summary>
    /// 검색어 하나로 MENU_NM / MENU_CD / APP_URL 세 필드를 동시에 찾는 필터.
    /// 세 필드 중 하나라도 부분 일치하면(OR 조건) 그 행을 보여준다.
    /// </summary>
    [ObservableProperty]
    private string filterKeyword = string.Empty;

    /// <summary>화면 하단 상태 메시지.</summary>
    [ObservableProperty]
    private string statusMessage = "메뉴 정보 JSON 파일을 열어주세요.";

    public IRelayCommand OpenFileCommand { get; }

    public IRelayCommand RefreshCommand { get; }

    partial void OnFilterKeywordChanged(string value) => ApplyFilter();

    /// <summary>Windows 파일 열기 대화상자를 띄워 메뉴 정보 JSON 파일을 고른다.</summary>
    private void OpenFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "메뉴 정보 JSON 파일을 선택하세요",
            Filter = "JSON 파일 (*.json)|*.json|모든 파일 (*.*)|*.*",
            InitialDirectory = LoadedFilePath,
        };

        

        if (dialog.ShowDialog() == true)
        {
            LoadFile(dialog.FileName);
        }
    }

    /// <summary>현재 불러온 파일을 디스크에서 다시 읽어온다. (정기적으로 갱신되는 파일을 위한 새로고침)</summary>
    private void Refresh()
    {
        if (!string.IsNullOrWhiteSpace(LoadedFilePath))
        {
            LoadFile(LoadedFilePath);
        }
    }

    /// <summary>
    /// 지정한 JSON 파일을 대화상자 없이 바로 읽어 목록을 구성한다. Settings 화면에 미리 등록해둔
    /// 경로를 앱 시작 시 자동으로 불러올 때, [파일 열기]/[새로고침] 직후에도 쓰인다.
    /// </summary>
    private void LoadFile(string filePath)
    {
        LoadedFilePath = filePath;

        AllEntries = _parserService.ParseFile(filePath);
        ApplyFilter();

        StatusMessage = AllEntries.Count == 0
            ? "이 파일에서 메뉴 정보를 찾지 못했습니다."
            : $"{AllEntries.Count}개의 메뉴 정보를 불러왔습니다.";

        RefreshCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 검색어를 MENU_NM / MENU_CD / APP_URL 세 필드와 각각 비교해서, 하나라도 부분 일치하면
    /// (OR 조건) 그 행을 포함시킨다. Where 는 행 하나당 한 번만 평가되므로 같은 행이 중복으로
    /// 나오는 일은 없다 — 매칭된 필드가 몇 개든 그 행은 결과에 한 번만 나온다.
    /// </summary>
    private void ApplyFilter()
    {
        IEnumerable<MenuInfoEntry> query = AllEntries.Where(w=> !string.IsNullOrEmpty(w.FolderYn) && !w.FolderYn.Equals("Y"));

        if (!string.IsNullOrWhiteSpace(FilterKeyword))
        {
            var keyword = FilterKeyword.Trim();
            query = query.Where(entry =>
                Contains(entry.MenuNm, keyword) ||
                Contains(entry.MenuCd, keyword) ||
                Contains(entry.AppUrl, keyword));
        }

        FilteredEntries = query.ToList();
    }

    private static bool Contains(string? source, string keyword) =>
        !string.IsNullOrEmpty(source) && source.Contains(keyword, StringComparison.OrdinalIgnoreCase);
}
