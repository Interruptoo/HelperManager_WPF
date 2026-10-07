using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelperManager.Modules.QueryStore.Models;
using HelperManager.Modules.QueryStore.Services;
using HelperManager.Settings;
using Microsoft.Win32;

namespace HelperManager.Modules.QueryStore;

/// <summary>
/// Query Store 화면(XML로 추출한 쿼리 모음 뷰어)의 ViewModel.
///
/// 트리/검색/파일 열기는 이 화면 전체에서 "공용"으로 하나만 존재하고, 쿼리를 열람하는 부분만
/// 탭으로 여러 개 띄울 수 있다 (IDE의 탐색기 트리 하나 + 여러 개의 편집 탭과 같은 구조).
///
/// 화면 동작 흐름:
///  1) [파일 열기] 버튼으로 쿼리 모음 XML 파일을 고르면 -> 파일 안의 모든 &lt;sql id="..."&gt; 블록을
///     읽어 쿼리 목록을 만들고, id 의 점(.) 구조에 따라 트리(RootNodes)를 구성한다.
///  2) 트리에서 쿼리(리프) 노드를 더블클릭하면 -> 그 쿼리를 위한 탭(QueryTabViewModel)을 새로 열거나,
///     이미 열려 있으면 그 탭으로 전환한다. 탭마다 쿼리 본문/파라미터/클립보드 복사 결과가
///     독립적으로 유지된다.
///  3) 검색창에 쿼리 id 또는 SQL 본문 일부를 입력하고 [검색]을 누르면(라디오 버튼으로 둘 중 하나만
///     대상으로 검색) -> 일치하는 쿼리를 찾아 트리에서 상위 폴더를 모두 펼치고 선택 상태로 만든 뒤,
///     더블클릭과 마찬가지로 탭을 열어준다. 일치 항목이 여러 개면 다시 눌러서 다음 항목으로 이동한다.
/// </summary>
public sealed partial class QueryStoreViewModel : ObservableObject
{
    private readonly IQueryStoreParserService _parserService;

    // 검색 결과(일치하는 리프 노드) 목록과, 그중 현재 몇 번째를 보고 있는지 가리키는 인덱스.
    private List<QueryTreeNode> _searchMatches = [];
    private int _searchMatchIndex = -1;

    // 탭을 새로 열어도, 같은 이름의 바인드 변수(예: HIS_HSP_TP_CD, HIS_STF_NO 처럼 여러 쿼리에
    // 공통으로 쓰이는 값)는 다시 입력하지 않도록 모든 탭이 공유하는 사전. (세션 동안만, 파일 저장 없음)
    private readonly Dictionary<string, string> _rememberedParameterValues = new(StringComparer.OrdinalIgnoreCase);

    public QueryStoreViewModel(IQueryStoreParserService parserService, ISettingsService settingsService)
    {
        _parserService = parserService;

        OpenFileCommand = new RelayCommand(OpenFile);
        SearchCommand = new RelayCommand(SearchNext, () => !string.IsNullOrWhiteSpace(SearchKeyword) && RootNodes.Count > 0);

        // Settings 화면에 미리 등록해둔 XML 경로가 있으면, 매번 파일 찾기 대화상자를 열 필요 없이
        // 앱을 시작할 때 바로 읽어서 트리를 만들어둔다.
        var savedXmlPath = settingsService.Current.QueryStoreXmlPath;
        if (!string.IsNullOrWhiteSpace(savedXmlPath) && File.Exists(savedXmlPath))
        {
            LoadFile(savedXmlPath);
        }
    }

    /// <summary>현재 불러온 XML 파일 경로.</summary>
    [ObservableProperty]
    private string? loadedFilePath;

    /// <summary>트리 최상위 노드 목록.</summary>
    [ObservableProperty]
    private IReadOnlyList<QueryTreeNode> rootNodes = [];

    /// <summary>검색창 입력값. 쿼리 id 또는 SQL 본문 일부를 입력해 트리에서 찾아 선택한다.</summary>
    [ObservableProperty]
    private string searchKeyword = string.Empty;

    /// <summary>true면 쿼리 id를 대상으로, false면 쿼리 내용(본문)을 대상으로 검색한다. (라디오 버튼)</summary>
    [ObservableProperty]
    private bool searchById = true;

    /// <summary>true면 쿼리 내용(본문)을 대상으로 검색한다. SearchById 와 라디오 버튼 그룹으로 묶여 배타적으로 동작한다.</summary>
    [ObservableProperty]
    private bool searchByContent;

    /// <summary>검색 결과 안내 ("3 / 12" 또는 "일치하는 쿼리가 없습니다." 등).</summary>
    [ObservableProperty]
    private string searchStatusText = string.Empty;

    /// <summary>현재 선택된(하이라이트된) 트리 노드. 코드비하인드의 TreeView.SelectedItemChanged 에서 채워진다.</summary>
    [ObservableProperty]
    private QueryTreeNode? selectedNode;

    /// <summary>화면 하단 상태 메시지.</summary>
    [ObservableProperty]
    private string statusMessage = "쿼리 모음 XML 파일을 열어주세요.";

    /// <summary>현재 열려 있는 쿼리 탭 목록. 트리에서 쿼리를 더블클릭(또는 검색)할 때마다 하나씩 추가된다.</summary>
    public ObservableCollection<QueryTabViewModel> OpenTabs { get; } = [];

    /// <summary>현재 선택된(화면에 보이는) 탭.</summary>
    [ObservableProperty]
    private QueryTabViewModel? selectedTab;

    public IRelayCommand OpenFileCommand { get; }

    public IRelayCommand SearchCommand { get; }

    partial void OnSearchKeywordChanged(string value)
    {
        SearchCommand.NotifyCanExecuteChanged();
        ResetSearchState();
    }

    partial void OnSearchByIdChanged(bool value) => ResetSearchState();

    partial void OnSearchByContentChanged(bool value) => ResetSearchState();

    /// <summary>검색어/검색 대상(라디오 버튼)이 바뀌면 이전 검색의 "몇 번째 결과" 순번은 의미가 없어지므로 초기화한다.</summary>
    private void ResetSearchState()
    {
        _searchMatches = [];
        _searchMatchIndex = -1;
        SearchStatusText = string.Empty;
    }

    /// <summary>Windows 파일 열기 대화상자를 띄워 쿼리 모음 XML 파일을 고른다.</summary>
    private void OpenFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "쿼리 모음 XML 파일을 선택하세요",
            Filter = "XML 파일 (*.xml)|*.xml|모든 파일 (*.*)|*.*",
            InitialDirectory = LoadedFilePath,
        };

        if (dialog.ShowDialog() == true)
        {
            LoadFile(dialog.FileName);
        }
    }

    /// <summary>
    /// 지정한 XML 파일을 대화상자 없이 바로 읽어 트리를 구성한다. Settings 화면에 미리 등록해둔
    /// 경로를 앱 시작 시 자동으로 불러올 때, 그리고 [파일 열기] 대화상자에서 고른 직후에도 쓰인다.
    /// </summary>
    private void LoadFile(string filePath)
    {
        LoadedFilePath = filePath;

        var queries = _parserService.ParseFile(filePath);
        RootNodes = _parserService.BuildTree(queries);

        SelectedNode = null;
        SearchKeyword = string.Empty;
        ResetSearchState();

        // 새 파일을 열면 이전 파일 기준으로 열려 있던 탭은 더 이상 의미가 없으므로 모두 닫는다.
        foreach (var tab in OpenTabs)
        {
            tab.CloseRequested -= OnTabCloseRequested;
        }
        OpenTabs.Clear();
        SelectedTab = null;

        StatusMessage = queries.Count == 0
            ? "이 파일에서 <sql id=\"...\"> 형식의 쿼리를 찾지 못했습니다."
            : $"{queries.Count}개의 쿼리를 불러왔습니다.";

        SearchCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 검색어와 일치하는(라디오 버튼으로 고른 대상: id 또는 본문) 쿼리를 찾아 트리에서 선택 상태로 만들고
    /// 탭으로 연다. 같은 검색어로 다시 실행하면 일치 항목을 순서대로 순환하며 보여준다.
    /// </summary>
    private void SearchNext()
    {
        if (_searchMatches.Count == 0)
        {
            _searchMatches = CollectLeaves(RootNodes)
                .Where(node => Matches(node, SearchKeyword))
                .ToList();
            _searchMatchIndex = -1;
        }

        if (_searchMatches.Count == 0)
        {
            SearchStatusText = "일치하는 쿼리가 없습니다.";
            return;
        }

        _searchMatchIndex = (_searchMatchIndex + 1) % _searchMatches.Count;
        SelectInTree(_searchMatches[_searchMatchIndex]);

        SearchStatusText = $"{_searchMatchIndex + 1} / {_searchMatches.Count}";
    }

    /// <summary>라디오 버튼으로 고른 대상(쿼리 id 또는 쿼리 내용)만을 기준으로 일치 여부를 판단한다.</summary>
    private bool Matches(QueryTreeNode node, string keyword)
    {
        if (node.Query is null)
        {
            return false;
        }

        return SearchById
            ? node.Query.Id.Contains(keyword, StringComparison.OrdinalIgnoreCase)
            : node.Query.Content.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<QueryTreeNode> CollectLeaves(IEnumerable<QueryTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.IsLeaf)
            {
                yield return node;
            }
            else
            {
                foreach (var leaf in CollectLeaves(node.Children))
                {
                    yield return leaf;
                }
            }
        }
    }

    /// <summary>찾은 노드까지 이어지는 모든 상위 폴더를 펼치고, 해당 노드를 선택 상태로 만든 뒤 탭으로 연다.</summary>
    private void SelectInTree(QueryTreeNode target)
    {
        if (SelectedNode is not null)
        {
            SelectedNode.IsSelected = false;
        }

        ExpandAncestors(RootNodes, target);

        target.IsSelected = true;
        SelectedNode = target;

        if (target.Query is not null)
        {
            OpenTab(target.Query);
        }
    }

    /// <summary>target 까지의 경로에 있는 모든 폴더 노드의 IsExpanded 를 true 로 만든다.</summary>
    private static bool ExpandAncestors(IEnumerable<QueryTreeNode> nodes, QueryTreeNode target)
    {
        foreach (var node in nodes)
        {
            if (ReferenceEquals(node, target))
            {
                return true;
            }

            if (!node.IsLeaf && ExpandAncestors(node.Children, target))
            {
                node.IsExpanded = true;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 트리에서 쿼리(리프 노드)를 더블클릭했을 때 코드비하인드에서 호출한다.
    /// 이미 그 쿼리의 탭이 열려 있으면 그 탭으로 전환하고, 아니면 새 탭을 열어 바로 보여준다.
    /// </summary>
    public void OpenTab(QueryDefinition query)
    {
        var existing = OpenTabs.FirstOrDefault(tab => string.Equals(tab.Query.Id, query.Id, StringComparison.Ordinal));
        if (existing is not null)
        {
            SelectedTab = existing;
            return;
        }

        var tab = new QueryTabViewModel(query, _rememberedParameterValues);
        tab.CloseRequested += OnTabCloseRequested;

        OpenTabs.Add(tab);
        SelectedTab = tab;
    }

    /// <summary>탭의 닫기(✕) 버튼이 눌렸을 때, 목록에서 제거하고 다른 탭을 대신 선택한다.</summary>
    private void OnTabCloseRequested(object? sender, EventArgs e)
    {
        if (sender is not QueryTabViewModel tab)
        {
            return;
        }

        tab.CloseRequested -= OnTabCloseRequested;

        var closedIndex = OpenTabs.IndexOf(tab);
        if (closedIndex < 0)
        {
            return;
        }

        OpenTabs.RemoveAt(closedIndex);

        if (!ReferenceEquals(SelectedTab, tab))
        {
            return;
        }

        SelectedTab = OpenTabs.Count == 0 ? null : OpenTabs[Math.Min(closedIndex, OpenTabs.Count - 1)];
    }
}
