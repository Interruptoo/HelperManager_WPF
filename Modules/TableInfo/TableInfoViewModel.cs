using HelperManager.Common;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelperManager.Modules.ComnCode.Models;
using HelperManager.Modules.ComnCode.Services;
using HelperManager.Modules.TableInfo.Models;
using HelperManager.Modules.TableInfo.Services;
using HelperManager.Settings;
using Microsoft.Win32;

namespace HelperManager.Modules.TableInfo;

/// <summary>
/// TableInfo 화면(테이블 명세 뷰어)의 ViewModel.
///
/// 화면 동작 흐름:
///  1) 네 개의 JSON 파일(테이블 / 컬럼 / 인덱스 / 사용 오브젝트)을 [파일 열기]로 고르거나,
///     Settings 에 미리 등록해둔 경로로 앱 시작 시 자동으로 읽어온다. 네 파일은 서로 독립적이라
///     있는 것만 먼저 불러도 되고, 나중에 하나씩 추가로 열어도 된다.
///  2) 왼쪽 목록에 테이블(OWNER / TABLE_NAME / TABLE_COMMENTS)이 표시되고,
///     검색어를 입력하면 이 세 필드 중 하나라도 부분 일치하는 테이블만 남는다.
///  3) 왼쪽에서 테이블을 선택하면 -> 오른쪽의 컬럼 / 인덱스 / 사용 오브젝트 목록이
///     그 테이블(OWNER + TABLE_NAME)에 해당하는 것만으로 한 번에 갱신된다.
///
/// 성능: 컬럼 목록은 스키마 전체를 내보내면 수십만 건이 될 수 있어, 테이블을 고를 때마다 전체를
/// 훑으면 느리다. 그래서 파일을 읽는 시점에 조인 키(OWNER+TABLE_NAME) 기준 Lookup 을 한 번만
/// 만들어두고, 선택할 때는 Lookup 에서 바로 꺼내 쓴다.
/// </summary>
public sealed partial class TableInfoViewModel : ObservableObject
{
    private readonly ITableInfoParserService _parserService;

    private IReadOnlyList<TableInfoEntry> _allTables = [];
    private ILookup<string, ColumnInfoEntry> _columnsByTable = EmptyLookup<ColumnInfoEntry>();
    private ILookup<string, IndexInfoEntry> _indexesByTable = EmptyLookup<IndexInfoEntry>();
    private ILookup<string, TableObjectEntry> _objectsByTable = EmptyLookup<TableObjectEntry>();

    /// <summary>
    /// 인덱스 목록을 OWNER 까지 맞춰서 이어줄지, 테이블 이름만으로 이어줄지. 현재 인덱스 추출 쿼리는
    /// OWNER 를 내보내지 않으므로 보통 false 가 되고, 나중에 OWNER 가 추가되면 자동으로 true 가 된다.
    /// </summary>
    private bool _indexesHaveOwner;

    /// <summary>
    /// 사용 오브젝트 목록도 마찬가지로, 참조 대상 테이블의 소유자(referenced_owner)가 파일에 들어
    /// 있을 때만 OWNER 까지 맞춘다. 자세한 사정은 <see cref="TableObjectEntry.UsedTableOwner"/> 참고.
    /// </summary>
    private bool _objectsHaveOwner;

    // 공통코드 파일은 Common Code 화면과 한 벌을 나눠 쓴다. (ComnCdDetail 은 16만 건 / 약 500MB 라
    // 화면마다 따로 읽으면 메모리가 두 배가 된다)
    private readonly IComnCodeDataProvider _comnCodeDataProvider;

    public TableInfoViewModel(
        ITableInfoParserService parserService,
        IComnCodeDataProvider comnCodeDataProvider,
        ISettingsService settingsService,
        IJsonDataRefreshNotifier refreshNotifier)
    {
        _parserService = parserService;
        _comnCodeDataProvider = comnCodeDataProvider;

        OpenTableInfoFileCommand = new RelayCommand(() => OpenFile("TableInfo", path => TableInfoFilePath = path));
        OpenColumnInfoFileCommand = new RelayCommand(() => OpenFile("ColumnInfo", path => ColumnInfoFilePath = path));
        OpenIndexInfoFileCommand = new RelayCommand(() => OpenFile("IndexInfo", path => IndexInfoFilePath = path));
        OpenTableObjectFileCommand = new RelayCommand(() => OpenFile("TableUseObjectList", path => TableObjectFilePath = path));
        RefreshCommand = new RelayCommand(Load, CanRefresh);

        // Settings 화면에서 JSON 을 새로 추출하면 화면에 [새로고침] 버튼 없이도 알아서 다시 읽는다.
        refreshNotifier.Refreshed += Load;

        var settings = settingsService.Current;
        tableInfoFilePath = settings.TableInfoJsonPath;
        columnInfoFilePath = settings.ColumnInfoJsonPath;
        indexInfoFilePath = settings.IndexInfoJsonPath;
        tableObjectFilePath = settings.TableUseObjectJsonPath;

        if (CanRefresh())
        {
            Load();
        }
    }

    /// <summary>현재 불러온 TableInfo JSON 파일 경로.</summary>
    [ObservableProperty]
    private string? tableInfoFilePath;

    /// <summary>현재 불러온 ColumnInfo JSON 파일 경로.</summary>
    [ObservableProperty]
    private string? columnInfoFilePath;

    /// <summary>현재 불러온 IndexInfo JSON 파일 경로.</summary>
    [ObservableProperty]
    private string? indexInfoFilePath;

    /// <summary>현재 불러온 TableUseObjectList JSON 파일 경로.</summary>
    [ObservableProperty]
    private string? tableObjectFilePath;

    /// <summary>왼쪽 DataGrid에 표시되는, 검색어가 적용된 테이블 목록.</summary>
    [ObservableProperty]
    private IReadOnlyList<TableInfoEntry> filteredTables = [];

    /// <summary>왼쪽에서 선택된 테이블. 여기서 조인 키를 뽑아 오른쪽 세 목록을 채운다.</summary>
    [ObservableProperty]
    private TableInfoEntry? selectedTable;

    /// <summary>선택한 테이블의 컬럼 목록.</summary>
    [ObservableProperty]
    private IReadOnlyList<ColumnInfoEntry> columns = [];

    /// <summary>선택한 테이블의 인덱스 목록.</summary>
    [ObservableProperty]
    private IReadOnlyList<IndexInfoEntry> indexes = [];

    /// <summary>선택한 테이블을 사용 중인 오브젝트 목록.</summary>
    [ObservableProperty]
    private IReadOnlyList<TableObjectEntry> objects = [];

    /// <summary>컬럼 목록에서 선택된 컬럼. 이 컬럼의 COMMENTS 로 공통코드 그룹을 찾는다.</summary>
    [ObservableProperty]
    private ColumnInfoEntry? selectedColumn;

    /// <summary>선택한 컬럼의 주석으로 찾은 공통코드 그룹에 묶여 있는 상세 코드 목록.</summary>
    [ObservableProperty]
    private IReadOnlyList<ComnCodeRecord> matchedComnCodes = [];

    /// <summary>
    /// 공통코드 패널에 덧붙여 보여줄 안내. 무엇으로 찾았는지(정확히 일치 / 부분 일치),
    /// 또는 왜 비어 있는지를 알려준다.
    /// </summary>
    [ObservableProperty]
    private string comnCodeHint = "컬럼을 선택하면 그 주석과 이름이 같은 공통코드 그룹을 찾아 보여줍니다.";

    /// <summary>
    /// 사용 오브젝트 파일에 "어떤 테이블을 참조하는지"가 들어있지 않아 테이블별로 걸러낼 수 없을 때
    /// true. 이때는 목록 대신 추출 쿼리를 어떻게 고치면 되는지 안내 문구를 보여준다.
    /// </summary>
    [ObservableProperty]
    private bool isObjectJoinUnavailable;

    /// <summary>검색어. OWNER / TABLE_NAME / TABLE_COMMENTS 중 하나라도 부분 일치하면 그 테이블을 보여준다.</summary>
    [ObservableProperty]
    private string filterKeyword = string.Empty;

    /// <summary>화면 하단 상태 메시지.</summary>
    [ObservableProperty]
    private string statusMessage = "TableInfo / ColumnInfo / IndexInfo / TableUseObjectList JSON 파일을 열어주세요.";

    public IRelayCommand OpenTableInfoFileCommand { get; }

    public IRelayCommand OpenColumnInfoFileCommand { get; }

    public IRelayCommand OpenIndexInfoFileCommand { get; }

    public IRelayCommand OpenTableObjectFileCommand { get; }

    public IRelayCommand RefreshCommand { get; }

    partial void OnFilterKeywordChanged(string value) => ApplyFilter();

    partial void OnSelectedTableChanged(TableInfoEntry? value) => UpdateDetails();

    partial void OnSelectedColumnChanged(ColumnInfoEntry? value) => UpdateComnCodes();

    /// <summary>네 경로 중 하나라도 지정되어 있으면 불러올 것이 있다고 본다.</summary>
    private bool CanRefresh() =>
        !string.IsNullOrWhiteSpace(TableInfoFilePath) ||
        !string.IsNullOrWhiteSpace(ColumnInfoFilePath) ||
        !string.IsNullOrWhiteSpace(IndexInfoFilePath) ||
        !string.IsNullOrWhiteSpace(TableObjectFilePath);

    /// <summary>파일 열기 대화상자를 띄워 경로를 고르고, 곧바로 전체를 다시 읽어 화면에 반영한다.</summary>
    private void OpenFile(string fileLabel, Action<string> assignPath)
    {
        var dialog = new OpenFileDialog
        {
            Title = $"{fileLabel} JSON 파일을 선택하세요",
            Filter = "JSON 파일 (*.json)|*.json|모든 파일 (*.*)|*.*",
        };

        if (dialog.ShowDialog() == true)
        {
            assignPath(dialog.FileName);
            Load();
        }
    }

    /// <summary>
    /// 지정된 네 경로 중 실제로 존재하는 파일을 모두 다시 읽어 목록과 Lookup 을 새로 만든다.
    /// (정기적으로 갱신되는 파일들을 위한 새로고침에도, 파일을 하나 새로 열었을 때도 같은 경로를 탄다)
    /// </summary>
    private void Load()
    {
        var previousKey = SelectedTable?.JoinKey;

        _allTables = ReadOrEmpty(TableInfoFilePath, _parserService.ParseTables);

        var columnList = ReadOrEmpty(ColumnInfoFilePath, _parserService.ParseColumns);
        var indexList = ReadOrEmpty(IndexInfoFilePath, _parserService.ParseIndexes);
        var objectList = ReadOrEmpty(TableObjectFilePath, _parserService.ParseObjects);

        _columnsByTable = columnList.ToLookup(entry => entry.JoinKey, StringComparer.Ordinal);

        // 인덱스 파일에 OWNER 가 들어있으면 OWNER 까지, 없으면 테이블 이름만으로 이어준다.
        _indexesHaveOwner = indexList.Any(entry => !string.IsNullOrEmpty(entry.Owner));
        _indexesByTable = indexList.ToLookup(
            entry => _indexesHaveOwner ? entry.JoinKey : entry.TableOnlyKey,
            StringComparer.Ordinal);

        // 오브젝트도 참조 대상 테이블의 소유자가 들어있을 때만 OWNER 까지 맞춘다.
        _objectsHaveOwner = objectList.Any(entry => !string.IsNullOrEmpty(entry.UsedTableOwner));
        _objectsByTable = objectList.ToLookup(
            entry => _objectsHaveOwner ? entry.JoinKey : entry.TableOnlyKey,
            StringComparer.Ordinal);

        // 오브젝트 파일에 참조 테이블이 한 건도 없으면, 테이블을 골라도 걸러낼 방법이 없다.
        IsObjectJoinUnavailable = objectList.Count > 0
            && !objectList.Any(entry => !string.IsNullOrEmpty(entry.UsedTableName));

        ApplyFilter();

        // 새로고침 전에 보고 있던 테이블이 그대로 남아 있으면 선택을 유지한다.
        if (previousKey is not null)
        {
            SelectedTable = FilteredTables.FirstOrDefault(table => table.JoinKey == previousKey);
        }

        StatusMessage = $"테이블 {_allTables.Count}건 / 컬럼 {columnList.Count}건 / " +
                        $"인덱스 {indexList.Count}건 / 사용 오브젝트 {objectList.Count}건을 불러왔습니다.";

        RefreshCommand.NotifyCanExecuteChanged();
    }

    /// <summary>경로가 비어 있거나 파일이 없으면 빈 목록을, 있으면 파서로 읽은 목록을 반환한다.</summary>
    private static IReadOnlyList<T> ReadOrEmpty<T>(string? filePath, Func<string, IReadOnlyList<T>> parse) =>
        !string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath) ? parse(filePath) : [];

    /// <summary>검색어(OWNER / TABLE_NAME / TABLE_COMMENTS 부분 일치, OR 조건)로 왼쪽 목록을 다시 구성한다.</summary>
    private void ApplyFilter()
    {
        IEnumerable<TableInfoEntry> query = _allTables;

        if (!string.IsNullOrWhiteSpace(FilterKeyword))
        {
            var keyword = FilterKeyword.Trim();
            query = query.Where(table =>
                Contains(table.Owner, keyword) ||
                Contains(table.TableName, keyword) ||
                Contains(table.TableComments, keyword));
        }

        FilteredTables = query.ToList();

        // 목록이 다시 그려지면 이전 선택은 더 이상 유효하지 않을 수 있으므로 비운다.
        SelectedTable = null;
    }

    /// <summary>선택된 테이블의 조인 키로 컬럼 / 인덱스 / 사용 오브젝트 세 목록을 한 번에 갱신한다.</summary>
    private void UpdateDetails()
    {
        if (SelectedTable is not { } selected)
        {
            Columns = [];
            Indexes = [];
            Objects = [];
            return;
        }

        var key = selected.JoinKey;
        Columns = _columnsByTable[key].ToList();
        Indexes = _indexesByTable[_indexesHaveOwner ? key : selected.TableOnlyKey].ToList();
        Objects = _objectsByTable[_objectsHaveOwner ? key : selected.TableOnlyKey].ToList();

        // 다른 테이블로 옮기면 이전 컬럼 선택은 더 이상 유효하지 않다.
        SelectedColumn = null;
    }

    /// <summary>
    /// 선택한 컬럼의 COMMENTS(예: "병원구분코드")로 공통코드를 찾아 보여준다.
    ///
    /// 두 단계로 찾는다:
    ///   1) ComnCdInfo 에서 COMN_GRP_CD_NM 이 컬럼 주석과 "정확히 일치"하는 그룹을 찾아 COMN_GRP_CD 를 얻고
    ///   2) ComnCdDetail 에서 그 그룹에 묶여 있는 상세 코드(COMN_CD ...)를 모아서 보여준다.
    ///
    /// 같은 이름의 그룹이 기본(CCCCCSTE)과 병원별(CCCMCSTE) 양쪽에 있을 수 있어 그룹이 여러 개
    /// 나올 수 있고, 그때는 각 그룹의 상세 코드를 모두 합쳐서 보여준다.
    /// 그룹에서 상세로 내려가는 조인 규칙은 Common Code 화면과 같은 것을 쓴다
    /// (<see cref="ComnCodeGroupMatch"/> — HSP_TP_CD 를 느슨하게 비교해야 하는 사정이 있다).
    /// </summary>
    private void UpdateComnCodes()
    {
        if (SelectedColumn?.Comments is not { } comment || string.IsNullOrWhiteSpace(comment))
        {
            MatchedComnCodes = [];
            ComnCodeHint = SelectedColumn is null
                ? "컬럼을 선택하면 그 주석과 이름이 같은 공통코드 그룹의 코드 목록을 보여줍니다."
                : "선택한 컬럼에 주석(COMMENTS)이 없어 찾을 수 없습니다.";
            return;
        }

        var groups = _comnCodeDataProvider.Groups;
        if (groups.Count == 0)
        {
            MatchedComnCodes = [];
            ComnCodeHint = "ComnCdInfo.json 을 불러오지 못했습니다. Settings 에서 경로를 확인해 주세요.";
            return;
        }

        var keyword = comment.Trim();

        // 1) 컬럼 주석과 그룹명이 정확히 일치하는 그룹.
        var matchedGroups = groups
            .Where(group => string.Equals(group.ComnGrpCdNm?.Trim(), keyword, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matchedGroups.Count == 0)
        {
            MatchedComnCodes = [];
            ComnCodeHint = $"\"{keyword}\" 와 이름이 같은 공통코드 그룹이 없습니다.";
            return;
        }

        // 2) 그 그룹들에 묶여 있는 상세 코드.
        var details = _comnCodeDataProvider.Details;
        MatchedComnCodes = matchedGroups
            .SelectMany(group => ComnCodeGroupMatch.DetailsOf(details, group))
            .ToList();

        var groupCodes = string.Join(", ", matchedGroups.Select(group => group.ComnGrpCd).Distinct());
        ComnCodeHint = MatchedComnCodes.Count > 0
            ? $"\"{keyword}\" -> 그룹 {groupCodes} 의 공통코드 {MatchedComnCodes.Count}건"
            : $"\"{keyword}\" -> 그룹 {groupCodes} 을 찾았지만 묶여 있는 공통코드가 없습니다.";
    }

    private static bool Contains(string? source, string keyword) =>
        !string.IsNullOrEmpty(source) && source.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    /// <summary>파일을 아직 읽지 않은 상태에서도 Lookup 조회가 그대로 동작하도록 쓰는 빈 Lookup.</summary>
    private static ILookup<string, T> EmptyLookup<T>() =>
        Enumerable.Empty<T>().ToLookup(_ => string.Empty, StringComparer.Ordinal);
}
