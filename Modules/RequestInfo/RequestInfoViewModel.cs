using System.ComponentModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelperManager.Common;
using HelperManager.Modules.RequestInfo.Models;
using HelperManager.Modules.RequestInfo.Services;
using Microsoft.Win32;

namespace HelperManager.Modules.RequestInfo;

/// <summary>
/// RequestInfo 화면(로그 뷰어)의 ViewModel.
///
/// 화면 동작 흐름:
///  1) 사용자가 [폴더 선택] 버튼으로 로그 폴더를 고르면 -> 폴더 내 로그 파일 목록을 채운다.
///  2) 파일 하나를 선택하면 -> 형식(JSON 라인 / ERROR 블록 / EQS_Trace 블록)을 자동 판별해서
///     파싱하고, 항목 목록(AllEntries)을 만든다.
///  3) 항목 목록에서 검색어로 다시 걸러 FilteredEntries 를 만든다. (좌측 목록에 표시)
///  4) 목록에서 항목 하나를 선택하면 상세 내용을 보여준다.
///     - JSON 라인 로그(요청/응답)는, 오른쪽 필드 체크박스에서 체크된 필드만 골라
///       예쁘게(들여쓰기 + 색상) 조립해서 SelectedEntryPrettyJson 에 담는다.
///     - ERROR/EQS_Trace 블록 로그는 파싱 시점에 이미 가공해 둔 PlainDetail 을 그대로 보여준다.
/// </summary>
public sealed partial class RequestInfoViewModel : ObservableObject
{
    private readonly ILogFileParserService _parserService;

    public RequestInfoViewModel(ILogFileParserService parserService)
    {
        _parserService = parserService;

        SelectFolderCommand = new RelayCommand(SelectFolder);
        RefreshFilesCommand = new RelayCommand(RefreshFiles, () => !string.IsNullOrWhiteSpace(SelectedFolderPath));
        CheckAllFieldsCommand = new RelayCommand(() => SetAllFieldFilters(true));
        UncheckAllFieldsCommand = new RelayCommand(() => SetAllFieldFilters(false));
    }

    // ===== 폴더/파일 선택 =====

    /// <summary>현재 선택된 로그 폴더 경로.</summary>
    [ObservableProperty]
    private string? selectedFolderPath;

    /// <summary>
    /// 폴더 안에서 확장자 조건만으로 찾은 "전체" 로그 파일 목록 (날짜 필터 적용 전).
    /// 날짜 범위를 바꿀 때마다 디스크를 다시 뒤지지 않고 이 목록을 재사용하기 위해 캐시해둔다.
    /// </summary>
    private IReadOnlyList<LogFileInfo> _allLogFilesInFolder = [];

    /// <summary>생성일 필터가 적용된 후, 콤보박스에 실제로 표시되는 로그 파일 목록.</summary>
    [ObservableProperty]
    private IReadOnlyList<LogFileInfo> logFiles = [];

    /// <summary>현재 선택된 로그 파일.</summary>
    [ObservableProperty]
    private LogFileInfo? selectedLogFile;

    /// <summary>
    /// 생성일 필터 - 시작일. 로그가 쌓여 콤보박스에서 원하는 파일을 찾기 어려워지는 것을 막기 위한 기능.
    /// 기본값은 "오늘 - 1일"(어제)이다.
    /// </summary>
    [ObservableProperty]
    private DateTime filterFromDate = DateTime.Today.AddDays(-1);

    /// <summary>생성일 필터 - 종료일(포함). 기본값은 오늘(sysdate)이다.</summary>
    [ObservableProperty]
    private DateTime filterToDate = DateTime.Today;

    public IRelayCommand SelectFolderCommand { get; }

    public IRelayCommand RefreshFilesCommand { get; }

    // ===== 파싱 결과 / 목록 =====

    /// <summary>선택한 파일을 파싱해서 얻은 전체 로그 항목.</summary>
    [ObservableProperty]
    private IReadOnlyList<LogEntry> allEntries = [];

    /// <summary>검색어로 필터링된 후 화면(좌측 목록)에 표시되는 항목.</summary>
    [ObservableProperty]
    private IReadOnlyList<LogEntry> filteredEntries = [];

    /// <summary>목록에서 현재 선택된 로그 항목(상세 화면에 표시할 대상).</summary>
    [ObservableProperty]
    private LogEntry? selectedEntry;

    /// <summary>목록 상단 검색어 입력값. Source/ContextId/원본 텍스트를 대상으로 부분 일치 검색한다.</summary>
    [ObservableProperty]
    private string searchKeyword = string.Empty;

    /// <summary>파일 로드/파싱 결과를 사용자에게 알려주는 상태 메시지.</summary>
    [ObservableProperty]
    private string statusMessage = "로그 파일이 있는 폴더를 선택해주세요.";

    // ===== 필드 필터 (body만 골라보기) =====

    /// <summary>현재 로드된 파일에서 발견된 최상위 필드명 체크박스 목록.</summary>
    [ObservableProperty]
    private IReadOnlyList<FieldFilterItem> fieldFilters = [];

    public IRelayCommand CheckAllFieldsCommand { get; }

    public IRelayCommand UncheckAllFieldsCommand { get; }

    // ===== 상세 화면 출력 =====

    /// <summary>선택된 항목에서, 체크된 필드만 뽑아 들여쓰기한 최종 표시 텍스트.</summary>
    [ObservableProperty]
    private string selectedEntryPrettyJson = string.Empty;

    /// <summary>
    /// 상세 화면에 처음부터 보여주고 싶은 "body" 성격의 필드 기본값.
    /// 요청/응답의 실제 내용에 해당하는 필드라서 기본으로 체크해둔다.
    /// </summary>
    private static readonly HashSet<string> DefaultCheckedFields = new(StringComparer.Ordinal)
    {
        "RequestContent",
        "ResponseContent",
    };

    // ===== 속성 변경 시 자동 처리 (CommunityToolkit.Mvvm 소스 제너레이터가 자동 호출) =====

    partial void OnSelectedFolderPathChanged(string? value)
    {
        RefreshFilesCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedLogFileChanged(LogFileInfo? value)
    {
        LoadSelectedFile();
    }

    partial void OnSelectedEntryChanged(LogEntry? value)
    {
        UpdateSelectedEntryDisplay();
    }

    partial void OnSearchKeywordChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnFilterFromDateChanged(DateTime value)
    {
        if (value > FilterToDate)
        {
            // 종료일보다 뒤로 이동하면 종료일도 함께 밀어서 항상 유효한 기간을 유지한다.
            // (FilterToDate 대입이 OnFilterToDateChanged 를 호출해 필터를 다시 적용하므로 여기서는 더 하지 않는다)
            FilterToDate = value;
            return;
        }

        ApplyDateFilter();
    }

    partial void OnFilterToDateChanged(DateTime value)
    {
        if (value < FilterFromDate)
        {
            FilterFromDate = value;
            return;
        }

        ApplyDateFilter();
    }

    // ===== 내부 동작 =====

    /// <summary>Windows 폴더 선택 대화상자를 띄워 로그 폴더를 고른다.</summary>
    private void SelectFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "로그 파일이 있는 폴더를 선택하세요",
        };

        if (dialog.ShowDialog() == true)
        {
            SelectedFolderPath = dialog.FolderName;
            RefreshFiles();
        }
    }

    /// <summary>현재 SelectedFolderPath 기준으로 디스크에서 파일 목록을 다시 읽어온 뒤, 날짜 필터를 적용한다.</summary>
    private void RefreshFiles()
    {
        if (string.IsNullOrWhiteSpace(SelectedFolderPath))
        {
            return;
        }

        _allLogFilesInFolder = _parserService.FindLogFiles(SelectedFolderPath);
        ApplyDateFilter();
    }

    /// <summary>
    /// 캐시해둔 전체 파일 목록(_allLogFilesInFolder)에서 생성일이 [FilterFromDate, FilterToDate] 범위
    /// (양 끝 날짜 포함, 하루 전체)에 해당하는 파일만 골라 LogFiles 를 갱신한다.
    /// 폴더를 다시 읽지 않으므로 날짜만 바꿀 때는 디스크 접근 없이 즉시 반영된다.
    /// </summary>
    private void ApplyDateFilter()
    {
        var fromInclusive = FilterFromDate.Date;
        var toExclusive = FilterToDate.Date.AddDays(1); // 종료일 하루 전체를 포함시키기 위해 다음날 자정을 상한으로 사용

        LogFiles = _allLogFilesInFolder
            .Where(file => file.CreatedTime >= fromInclusive && file.CreatedTime < toExclusive)
            .ToList();

        SelectedLogFile = LogFiles.FirstOrDefault();

        if (LogFiles.Count == 0)
        {
            StatusMessage = _allLogFilesInFolder.Count == 0
                ? "선택한 폴더에서 로그 파일(.log, .txt, .clog)을 찾지 못했습니다."
                : $"생성일 {FilterFromDate:yyyy-MM-dd} ~ {FilterToDate:yyyy-MM-dd} 범위에 해당하는 로그 파일이 없습니다. (전체 {_allLogFilesInFolder.Count}개)";
        }
    }

    /// <summary>선택된 파일을 실제로 읽어서 파싱하고, 필드 필터 목록을 새로 구성한다.</summary>
    private void LoadSelectedFile()
    {
        if (SelectedLogFile is null)
        {
            AllEntries = [];
            FilteredEntries = [];
            ClearFieldFilters();
            SelectedEntryPrettyJson = string.Empty;
            return;
        }

        var result = _parserService.ParseLogFile(SelectedLogFile.FullPath);
        AllEntries = result.Entries;

        if (result.Entries.Count > 0)
        {
            // JSON 라인 로그든, ERROR_*.log/EQS_Trace_*.log 같은 블록형 텍스트 로그든
            // 동일한 목록(그리드) + 검색 UI를 사용한다. 필드 체크박스는 JSON 로그일 때만 채워진다.
            RebuildFieldFilters();
            ApplyFilter();

            StatusMessage = FieldFilters.Count > 0
                ? $"총 {result.TotalLineCount}줄 중 {result.Entries.Count}건을 불러왔습니다. (JSON이 아니어서 건너뛴 줄 {result.SkippedLineCount}개)"
                : $"{result.Entries.Count}건의 로그 항목을 읽었습니다. 목록에서 항목을 선택하면 가독성 좋게 정리된 내용을 보여줍니다.";
        }
        else
        {
            // JSON 라인 / ERROR 블록 / EQS_Trace 블록 중 어디에도 해당하지 않는 파일은
            // 파싱을 포기하고 원본 텍스트를 가공 없이 그대로 보여준다. (목록/필드 필터는 비운다)
            FilteredEntries = [];
            ClearFieldFilters();

            // SelectedEntry 를 비우면 OnSelectedEntryChanged 가 SelectedEntryPrettyJson 을
            // 빈 문자열로 초기화하므로, 원본 텍스트는 반드시 그 다음에 대입해야 덮어써지지 않는다.
            SelectedEntry = null;
            SelectedEntryPrettyJson = _parserService.ReadRawText(SelectedLogFile.FullPath);

            StatusMessage = "알 수 없는 로그 형식입니다. 원본 텍스트를 그대로 표시합니다.";
        }
    }

    /// <summary>필드 필터 체크박스 이벤트 구독을 해제하고 목록을 비운다.</summary>
    private void ClearFieldFilters()
    {
        foreach (var item in FieldFilters)
        {
            item.PropertyChanged -= OnFieldFilterItemChanged;
        }

        FieldFilters = [];
    }

    /// <summary>새로 불러온 파일의 필드 목록으로 체크박스 컬렉션을 재구성한다.</summary>
    private void RebuildFieldFilters()
    {
        // 이전 파일의 체크박스에 걸어둔 이벤트 구독을 해제해서 메모리 누수를 방지한다.
        ClearFieldFilters();

        var fieldNames = _parserService.ExtractFieldNames(AllEntries);

        var newFilters = fieldNames
            .Select(name => new FieldFilterItem(name, DefaultCheckedFields.Contains(name)))
            .ToList();

        foreach (var item in newFilters)
        {
            item.PropertyChanged += OnFieldFilterItemChanged;
        }

        FieldFilters = newFilters;
    }

    /// <summary>체크박스 하나가 토글될 때마다 상세 화면을 다시 그린다.</summary>
    private void OnFieldFilterItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FieldFilterItem.IsChecked))
        {
            UpdateSelectedEntryDisplay();
        }
    }

    private void SetAllFieldFilters(bool isChecked)
    {
        foreach (var item in FieldFilters)
        {
            item.IsChecked = isChecked;
        }
    }

    /// <summary>검색어를 기준으로 AllEntries -> FilteredEntries 를 다시 계산한다.</summary>
    private void ApplyFilter()
    {
        IEnumerable<LogEntry> query = AllEntries;

        if (!string.IsNullOrWhiteSpace(SearchKeyword))
        {
            var keyword = SearchKeyword.Trim();
            query = query.Where(entry => entry.RawText.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        FilteredEntries = query.ToList();

        // 필터링 결과 현재 선택 항목이 목록에서 사라졌다면 선택을 해제한다.
        if (SelectedEntry is not null && !FilteredEntries.Contains(SelectedEntry))
        {
            SelectedEntry = FilteredEntries.FirstOrDefault();
        }
    }

    /// <summary>선택된 항목을 상세 화면에 보여줄 최종 텍스트로 조립한다.</summary>
    private void UpdateSelectedEntryDisplay()
    {
        if (SelectedEntry is null)
        {
            SelectedEntryPrettyJson = string.Empty;
            return;
        }

        if (SelectedEntry.JsonRoot is null)
        {
            // ERROR_*.log / EQS_Trace_*.log 는 파싱 단계에서 이미 사람이 읽기 좋은 형태로
            // 가공해두었으므로 그대로 보여준다. (이 형식에는 필드 체크박스 필터링이 적용되지 않는다)
            SelectedEntryPrettyJson = SelectedEntry.PlainDetail ?? SelectedEntry.RawText;
            return;
        }

        var checkedFieldNames = FieldFilters
            .Where(filter => filter.IsChecked)
            .Select(filter => filter.FieldName)
            .ToHashSet(StringComparer.Ordinal);

        if (checkedFieldNames.Count == 0)
        {
            SelectedEntryPrettyJson = "표시할 필드를 왼쪽 체크박스에서 하나 이상 선택해주세요.";
            return;
        }

        var builder = new StringBuilder();

        // 원본 JSON 의 필드 순서를 그대로 유지하면서, 체크된 필드만 골라서 출력한다.
        foreach (var property in SelectedEntry.JsonRoot)
        {
            if (!checkedFieldNames.Contains(property.Key))
            {
                continue;
            }

            builder.AppendLine($"■ {property.Key}");
            builder.AppendLine(JsonPrettyPrinter.PrettyPrint(property.Value));
            builder.AppendLine();
        }

        SelectedEntryPrettyJson = builder.ToString();
    }
}
